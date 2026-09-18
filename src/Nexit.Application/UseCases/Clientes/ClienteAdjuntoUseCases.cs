using Nexit.Application.DTOs.Clientes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Clientes;

public interface IClienteAdjuntosUseCase
{
    Task<IReadOnlyList<ClienteAdjuntoDto>> ListAsync(Guid clienteId, CancellationToken cancellationToken = default);
    Task<ClienteAdjuntoDto> CrearAsync(Guid clienteId, CrearClienteAdjuntoDto input, CancellationToken cancellationToken = default);

    /// <summary>Sube un archivo real (docs/28) -- a diferencia de CrearAsync (que espera un StoragePath ya conocido, o un link), esto recibe el contenido, lo valida (solo PDF/Excel, máximo 20 MB), lo sube a Supabase Storage y crea la fila con tipo "file".</summary>
    Task<ClienteAdjuntoDto> SubirAsync(Guid clienteId, string nombreArchivo, string contentType, long tamanoBytes, Stream contenido, CancellationToken cancellationToken = default);

    /// <summary>Devuelve la URL para descargar un adjunto: el link tal cual si es tipo "link", o una URL firmada temporal de Supabase Storage si es tipo "file".</summary>
    Task<string> ObtenerUrlDescargaAsync(Guid clienteId, Guid id, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid clienteId, Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Mismo contrato y mismas reglas que ProveedorAdjuntosUseCase (docs/28, HU-13) -- solo cambia la
/// entidad dueña. Se agregó 2026-09-10 a pedido de Alicia: el frontend ya tenía la sección "Archivos
/// y enlaces" lista para Clientes desde antes, pero apuntaba a un endpoint que todavía no existía acá.
/// </summary>
public class ClienteAdjuntosUseCase(IClienteRepository clientes, IClienteAdjuntoRepository adjuntos, ISupabaseStorageService storage, IUnitOfWork unitOfWork) : IClienteAdjuntosUseCase
{
    private static readonly Dictionary<string, string> ExtensionesPermitidas = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".xls"] = "application/vnd.ms-excel",
    };

    private const long TamanoMaximoBytes = 20 * 1024 * 1024; // 20 MB, igual que el límite configurado en el bucket de Supabase.

    public async Task<IReadOnlyList<ClienteAdjuntoDto>> ListAsync(Guid clienteId, CancellationToken ct = default)
    {
        await AsegurarCliente(clienteId, ct);
        return (await adjuntos.GetByClienteIdAsync(clienteId, ct)).Select(Map).ToList();
    }

    public async Task<ClienteAdjuntoDto> CrearAsync(Guid clienteId, CrearClienteAdjuntoDto input, CancellationToken ct = default)
    {
        await AsegurarCliente(clienteId, ct); Validar(input);
        var adjunto = new ClienteAdjunto { ClienteId = clienteId, Tipo = input.Tipo, Nombre = input.Nombre.Trim(), Url = input.Url?.Trim(), StoragePath = input.StoragePath?.Trim(), Meta = input.Meta?.Trim(), Fecha = input.Fecha ?? DateTime.UtcNow };
        await adjuntos.AddAsync(adjunto, ct); await unitOfWork.SaveChangesAsync(ct);
        return Map(adjunto);
    }

    public async Task<ClienteAdjuntoDto> SubirAsync(Guid clienteId, string nombreArchivo, string contentType, long tamanoBytes, Stream contenido, CancellationToken ct = default)
    {
        await AsegurarCliente(clienteId, ct);
        if (string.IsNullOrWhiteSpace(nombreArchivo)) throw new BusinessRuleException("El archivo no tiene nombre.");
        if (tamanoBytes <= 0) throw new BusinessRuleException("El archivo está vacío.");
        if (tamanoBytes > TamanoMaximoBytes) throw new BusinessRuleException($"El archivo supera el tamaño máximo permitido ({TamanoMaximoBytes / (1024 * 1024)} MB).");

        var extension = Path.GetExtension(nombreArchivo);
        if (!ExtensionesPermitidas.TryGetValue(extension, out var contentTypeEsperado))
            throw new BusinessRuleException("Solo se permiten archivos PDF (.pdf) o Excel (.xlsx, .xls).");

        var caracteresPermitidos = Path.GetFileNameWithoutExtension(nombreArchivo).Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').ToArray();
        var nombreSaneado = new string(caracteresPermitidos).Trim();
        if (string.IsNullOrWhiteSpace(nombreSaneado)) nombreSaneado = "archivo";
        var storagePath = $"clientes/{clienteId}/{Guid.NewGuid()}-{nombreSaneado}{extension.ToLowerInvariant()}";

        await storage.SubirAsync(storagePath, contenido, contentTypeEsperado, ct);

        var adjunto = new ClienteAdjunto
        {
            ClienteId = clienteId,
            Tipo = "file",
            Nombre = nombreArchivo.Trim(),
            StoragePath = storagePath,
            ContentType = contentTypeEsperado,
            TamanoBytes = tamanoBytes,
            Fecha = DateTime.UtcNow,
        };
        await adjuntos.AddAsync(adjunto, ct); await unitOfWork.SaveChangesAsync(ct);
        return Map(adjunto);
    }

    public async Task<string> ObtenerUrlDescargaAsync(Guid clienteId, Guid id, CancellationToken ct = default)
    {
        await AsegurarCliente(clienteId, ct);
        var adjunto = await adjuntos.GetByIdAsync(id, ct);
        if (adjunto is null || adjunto.ClienteId != clienteId) throw new EntityNotFoundException("ClienteAdjunto", id);

        if (adjunto.Tipo == "link")
            return adjunto.Url ?? throw new BusinessRuleException("Este adjunto de tipo link no tiene una URL guardada.");

        if (string.IsNullOrWhiteSpace(adjunto.StoragePath))
            throw new BusinessRuleException("Este adjunto no tiene un archivo real en Storage para descargar.");
        return await storage.ObtenerUrlFirmadaAsync(adjunto.StoragePath, TimeSpan.FromMinutes(10), ct);
    }

    public async Task EliminarAsync(Guid clienteId, Guid id, CancellationToken ct = default)
    {
        await AsegurarCliente(clienteId, ct);
        var adjunto = await adjuntos.GetByIdAsync(id, ct);
        if (adjunto is null || adjunto.ClienteId != clienteId) throw new EntityNotFoundException("ClienteAdjunto", id);
        await adjuntos.DeleteAsync(id, ct); await unitOfWork.SaveChangesAsync(ct);
        if (adjunto.Tipo == "file" && !string.IsNullOrWhiteSpace(adjunto.StoragePath))
            await storage.EliminarAsync(adjunto.StoragePath, ct);
    }

    private async Task AsegurarCliente(Guid id, CancellationToken ct) { if (await clientes.GetByIdAsync(id, ct) is null) throw new EntityNotFoundException("Cliente", id); }
    private static void Validar(CrearClienteAdjuntoDto input)
    {
        if (string.IsNullOrWhiteSpace(input.Nombre)) throw new BusinessRuleException("El nombre del adjunto es requerido.");
        if (input.Tipo == "link")
        {
            if (string.IsNullOrWhiteSpace(input.Url)) throw new BusinessRuleException("Un adjunto de tipo link requiere una URL.");
            var esUrlValida = Uri.TryCreate(input.Url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!esUrlValida) throw new BusinessRuleException("La URL del adjunto debe ser un enlace http:// o https:// válido.");
        }
        if (input.Tipo == "file" && string.IsNullOrWhiteSpace(input.StoragePath)) throw new BusinessRuleException("Un adjunto de tipo file requiere una ruta de almacenamiento.");
        if (input.Tipo is not ("link" or "file")) throw new BusinessRuleException("El tipo de adjunto debe ser link o file.");
    }
    private static ClienteAdjuntoDto Map(ClienteAdjunto x) => new() { Id = x.Id, ClienteId = x.ClienteId, Tipo = x.Tipo, Nombre = x.Nombre, Url = x.Url, StoragePath = x.StoragePath, Meta = x.Meta, ContentType = x.ContentType, TamanoBytes = x.TamanoBytes, Fecha = x.Fecha, CreatedAt = x.CreatedAt };
}
