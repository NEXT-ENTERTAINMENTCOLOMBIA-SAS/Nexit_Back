using Nexit.Application.DTOs.Proveedores;
using Nexit.Application.UseCases.Clientes;
using Nexit.Application.UseCases.Historial;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;
using Nexit.Core.Utils;

namespace Nexit.Application.UseCases.Proveedores;

public class CrearProveedorUseCase(IProveedorRepository repository, ICatalogosRepository catalogos, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : ICrearProveedorUseCase
{
    public async Task<ProveedorResponseDto> ExecuteAsync(CreateProveedorDto input, Guid usuarioId, CancellationToken ct = default)
    {
        await ProveedorRules.ValidarCatalogos(input, catalogos, ct);
        var proveedor = ProveedorMapper.ToEntity(input); proveedor.CreatedBy = usuarioId;
        await repository.AddAsync(proveedor, ct);
        await HistorialRegistrador.RegistrarCreacionAsync(historial, "proveedor", proveedor.Id, usuarioId, ct);
        await unitOfWork.SaveChangesAsync(ct); return ProveedorMapper.ToResponse(proveedor);
    }
}

public class ActualizarProveedorUseCase(IProveedorRepository repository, ICatalogosRepository catalogos, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : IActualizarProveedorUseCase
{
    public async Task<ProveedorResponseDto> ExecuteAsync(UpdateProveedorDto input, Guid usuarioId, CancellationToken ct = default)
    {
        var proveedor = await repository.GetByIdAsync(input.Id, ct) ?? throw new EntityNotFoundException("Proveedor", input.Id);
        var antes = CambioDetector.Snapshot(proveedor);
        await ProveedorRules.ValidarCatalogos(input, catalogos, ct); ProveedorMapper.Apply(input, proveedor); proveedor.UpdatedAt = DateTime.UtcNow; proveedor.UpdatedBy = usuarioId;
        await HistorialRegistrador.RegistrarEdicionAsync(historial, "proveedor", proveedor.Id, usuarioId, antes, proveedor, ct);
        // No repository.Update(proveedor) -- ya está rastreado (se obtuvo con GetByIdAsync en este mismo
        // scope); llamar DbSet.Update() aquí marcaría como Modified (en vez de Added) los ProveedorTelefono
        // y ProveedorServicio nuevos que ProveedorMapper.Apply acaba de agregar con un Id ya asignado,
        // rompiendo el guardado contra Postgres real -- mismo bug que en ActualizarClienteUseCase, ver ahí.
        await unitOfWork.SaveChangesAsync(ct); return ProveedorMapper.ToResponse(proveedor);
    }
}

public class ConsultarProveedoresUseCase(IProveedorRepository repository) : IConsultarProveedoresUseCase
{
    public async Task<IReadOnlyList<ProveedorResponseDto>> ListAsync(CancellationToken ct = default) => (await repository.GetAllAsync(ct)).Select(ProveedorMapper.ToResponse).ToList();
    public async Task<ProveedorResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) => ProveedorMapper.ToResponse(await repository.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException("Proveedor", id));
}

public class EliminarProveedorUseCase(
    IProveedorRepository repository, IHistorialCambioRepository historial,
    IUsuarioRepository usuarios, INotificacionRepository notificaciones, IUnitOfWork unitOfWork) : IEliminarProveedorUseCase
{
    public async Task ExecuteAsync(Guid id, Guid usuarioId, string? rol, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(id, ct) is null) throw new EntityNotFoundException("Proveedor", id);
        await repository.DeleteAsync(id, ct);
        await HistorialRegistrador.RegistrarEliminacionAsync(historial, "proveedor", id, usuarioId, ct);
        // Eliminar aca es SuperAdminOnly (Alicia 2026-09-18, corrigio la decision anterior:
        // antes tambien podian manager/admin/director) -- ya no hace falta notificar a nadie
        // aparte, porque solo hay un rol que puede llegar a este punto.
        await unitOfWork.SaveChangesAsync(ct);
    }
}

internal static class ProveedorMapper
{
    public static Proveedor ToEntity(CreateProveedorDto dto) { var entity = new Proveedor(); Apply(dto, entity); return entity; }
    public static void Apply(CreateProveedorDto dto, Proveedor entity)
    {
        entity.Nombre = dto.Nombre; entity.PaisId = dto.PaisId; entity.RegionId = dto.RegionId; entity.CiudadId = dto.CiudadId; entity.CategoriaId = dto.CategoriaId; entity.Estado = dto.Estado; entity.Contacto = dto.Contacto; entity.CargoContacto = dto.CargoContacto; entity.Web = dto.Web; entity.Direccion = dto.Direccion; entity.Aforo = dto.Aforo; entity.CostoReferencia = dto.CostoReferencia; entity.Score = dto.Score; entity.Presupuesto = dto.Presupuesto; entity.Cobertura = dto.Cobertura; entity.Notas = dto.Notas;
        // Guid.Empty (no Guid.NewGuid()) para los teléfonos y correos nuevos -- ver el comentario
        // detallado en ActualizarClienteUseCase (ClienteUseCases.cs) sobre por qué un Id ya asignado
        // hace que EF Core confunda una fila nueva con una existente cuando el proveedor padre ya
        // está rastreado.
        entity.Telefonos.Clear(); foreach (var phone in dto.Telefonos) entity.Telefonos.Add(new ProveedorTelefono { Id = phone.Id ?? Guid.Empty, ProveedorId = entity.Id, Telefono = phone.Telefono, Etiqueta = phone.Etiqueta });
        entity.Emails.Clear(); foreach (var mail in dto.Emails) entity.Emails.Add(new ProveedorEmail { Id = mail.Id ?? Guid.Empty, ProveedorId = entity.Id, Email = mail.Email, Etiqueta = mail.Etiqueta });
        entity.Servicios.Clear(); foreach (var servicioId in dto.ServicioIds.Distinct()) entity.Servicios.Add(new ProveedorServicio { ProveedorId = entity.Id, ServicioId = servicioId });
    }
    public static ProveedorResponseDto ToResponse(Proveedor entity) => new()
    {
        Id = entity.Id, Nombre = entity.Nombre, PaisId = entity.PaisId, RegionId = entity.RegionId, CiudadId = entity.CiudadId, CategoriaId = entity.CategoriaId, Estado = entity.Estado, Contacto = entity.Contacto, CargoContacto = entity.CargoContacto, Web = entity.Web, Direccion = entity.Direccion, Aforo = entity.Aforo, CostoReferencia = entity.CostoReferencia, Score = entity.Score, Presupuesto = entity.Presupuesto, Cobertura = entity.Cobertura, Notas = entity.Notas, CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt,
        Telefonos = entity.Telefonos.Select(x => new ProveedorTelefonoDto { Id = x.Id, Telefono = x.Telefono, Etiqueta = x.Etiqueta }).ToList(),
        Emails = entity.Emails.Select(x => new ProveedorEmailDto { Id = x.Id, Email = x.Email, Etiqueta = x.Etiqueta }).ToList(),
        ServicioIds = entity.Servicios.Select(x => x.ServicioId).ToList(),
        // Colaboradores.Usuario puede venir null si el repositorio no lo incluyó (por ejemplo, en pruebas
        // que arman un Proveedor a mano) -- se filtra en vez de lanzar, ver ProveedorRepository.GetByIdAsync/GetAllAsync.
        Colaboradores = entity.Colaboradores.Where(x => x.Usuario is not null)
            .Select(x => new ColaboradorProveedorDto { UsuarioId = x.UsuarioId, Nombre = $"{x.Usuario.Nombre} {x.Usuario.Apellido}".Trim(), Iniciales = x.Usuario.Iniciales }).ToList()
    };
}

internal static class ProveedorRules
{
    public static async Task ValidarCatalogos(CreateProveedorDto dto, ICatalogosRepository catalogos, CancellationToken ct)
    {
        if (await catalogos.GetPaisAsync(dto.PaisId, ct) is null) throw new BusinessRuleException("El país indicado no existe.");
        if (await catalogos.GetCategoriaAsync(dto.CategoriaId, ct) is null) throw new BusinessRuleException("La categoría indicada no existe.");
        Region? region = null;
        if (dto.RegionId.HasValue)
        {
            region = await catalogos.GetRegionAsync(dto.RegionId.Value, ct) ?? throw new BusinessRuleException("La región indicada no existe.");
            if (region.PaisId != dto.PaisId) throw new BusinessRuleException("La región no pertenece al país indicado.");
        }
        if (dto.CiudadId.HasValue)
        {
            var ciudad = await catalogos.GetCiudadAsync(dto.CiudadId.Value, ct) ?? throw new BusinessRuleException("La ciudad indicada no existe.");
            if (region is null || ciudad.RegionId != region.Id) throw new BusinessRuleException("La ciudad no pertenece a la región indicada.");
        }
        foreach (var servicioId in dto.ServicioIds.Distinct())
            if (await catalogos.GetServicioAsync(servicioId, ct) is null) throw new BusinessRuleException("Uno de los servicios indicados no existe.");
    }
}

/// <summary>
/// "A qué proveedor prestarle atención" (docs/21, docs/24): puntúa con <see cref="PrioridadProveedorCalculador"/>
/// todos los proveedores que no estén "Bloqueado" (no tiene sentido priorizar uno que ya no se usa),
/// y los devuelve ordenados de mayor a menor puntaje.
/// </summary>
public class ConsultarPrioridadProveedoresUseCase(IProveedorRepository repository) : IConsultarPrioridadProveedoresUseCase
{
    public async Task<IReadOnlyList<ProveedorPrioridadResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;
        return (await repository.GetAllAsync(ct))
            .Where(p => !string.Equals(p.Estado, "Bloqueado", StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                // Fecha del proyecto más reciente que lo tiene asociado (ProveedorRepository.GetAllAsync
                // ahora incluye Proyectos.Proyecto, ver docs/24), o null si nunca se le asignó ninguno.
                var ultimoProyecto = p.Proyectos.Count > 0 ? p.Proyectos.Max(pp => pp.Proyecto.CreatedAt) : (DateTime?)null;
                var resultado = PrioridadProveedorCalculador.Calcular(p, ultimoProyecto, ahora);
                return new ProveedorPrioridadResponseDto { ProveedorId = p.Id, Nombre = p.Nombre, Puntaje = resultado.Puntaje, Razones = resultado.Razones.ToList() };
            })
            .OrderByDescending(x => x.Puntaje).ThenBy(x => x.Nombre)
            .ToList();
    }
}
