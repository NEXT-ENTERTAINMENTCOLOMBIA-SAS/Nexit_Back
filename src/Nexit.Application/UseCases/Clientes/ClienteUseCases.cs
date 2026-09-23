using Nexit.Application.DTOs.Clientes;
using Nexit.Application.UseCases.Historial;
using Nexit.Application.UseCases.Notificaciones;
using Nexit.Application.UseCases.SolicitudesEliminacion;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;
using Nexit.Core.Utils;

namespace Nexit.Application.UseCases.Clientes;

public class CrearClienteUseCase(IClienteRepository repository, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : ICrearClienteUseCase
{
    public async Task<ClienteResponseDto> ExecuteAsync(CreateClienteDto input, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var cliente = ClienteMapper.ToEntity(input);
        cliente.CreatedBy = usuarioId;
        cliente.Telefonos = input.Telefonos.Select(phone => new ClienteTelefono
        {
            Id = phone.Id ?? Guid.NewGuid(), ClienteId = cliente.Id, Telefono = phone.Telefono, Etiqueta = phone.Etiqueta
        }).ToList();
        cliente.Emails = input.Emails.Select(mail => new ClienteEmail
        {
            Id = mail.Id ?? Guid.NewGuid(), ClienteId = cliente.Id, Email = mail.Email, Etiqueta = mail.Etiqueta
        }).ToList();
        await repository.AddAsync(cliente, cancellationToken);
        await HistorialRegistrador.RegistrarCreacionAsync(historial, "cliente", cliente.Id, usuarioId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClienteMapper.ToResponse(cliente);
    }
}

public class ActualizarClienteUseCase(IClienteRepository repository, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : IActualizarClienteUseCase
{
    public async Task<ClienteResponseDto> ExecuteAsync(UpdateClienteDto input, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var cliente = await repository.GetByIdAsync(input.Id, cancellationToken) ?? throw new EntityNotFoundException("Cliente", input.Id);
        var antes = CambioDetector.Snapshot(cliente);
        ClienteMapper.Apply(input, cliente);
        cliente.Telefonos.Clear();
        foreach (var phone in input.Telefonos)
        {
            cliente.Telefonos.Add(new ClienteTelefono
            {
                // Guid.Empty (no Guid.NewGuid()) a propósito para los teléfonos nuevos: "cliente" ya está
                // rastreado por el DbContext (viene de GetByIdAsync arriba), así que estos ClienteTelefono
                // solo se descubren por fixup de navegación, no por un Add() explícito. EF Core solo puede
                // distinguir "entidad nueva" de "entidad existente" para una clave generada por la base
                // (Id tiene HasDefaultValueSql("gen_random_uuid()")) cuando el valor de esa clave es el
                // default de CLR -- con cualquier otro valor asume que ya existe y genera un UPDATE en vez
                // de un INSERT, que falla porque esa fila no existe todavía (ver docs/08-tipos-de-pruebas.md).
                Id = phone.Id ?? Guid.Empty, ClienteId = cliente.Id, Telefono = phone.Telefono, Etiqueta = phone.Etiqueta
            });
        }
        cliente.Emails.Clear();
        foreach (var mail in input.Emails)
        {
            // Mismo motivo que arriba con Telefonos (Guid.Empty, no Guid.NewGuid(), para los correos nuevos):
            // ver el comentario detallado sobre el fixup de navegación de EF Core unas líneas arriba.
            cliente.Emails.Add(new ClienteEmail
            {
                Id = mail.Id ?? Guid.Empty, ClienteId = cliente.Id, Email = mail.Email, Etiqueta = mail.Etiqueta
            });
        }
        cliente.UpdatedAt = DateTime.UtcNow;
        cliente.UpdatedBy = usuarioId;
        // OJO: NO llamar repository.Update(cliente) aquí -- "cliente" ya está siendo rastreado por el
        // DbContext (se obtuvo con GetByIdAsync en este mismo scope), así que la llamada es redundante Y,
        // peor, DbSet.Update() recorre TODO el grafo y marca como Modified cualquier entidad hija con un Id
        // ya asignado (no default) que encuentre -- incluyendo los ClienteTelefono nuevos de arriba, a los
        // que aquí se les asigna un Guid explícito. El resultado es que EF genera un UPDATE en vez de un
        // INSERT para esos teléfonos nuevos, que falla con DbUpdateConcurrencyException (0 filas afectadas,
        // porque esa fila todavía no existe) -- un bug real que solo aparece contra Postgres de verdad, no
        // con los repositorios mockeados de las pruebas unitarias (ver docs/08-tipos-de-pruebas.md).
        await HistorialRegistrador.RegistrarEdicionAsync(historial, "cliente", cliente.Id, usuarioId, antes, cliente, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ClienteMapper.ToResponse(cliente);
    }
}

public class ConsultarClientesUseCase(IClienteRepository repository) : IConsultarClientesUseCase
{
    public async Task<IReadOnlyList<ClienteResponseDto>> ListAsync(CancellationToken cancellationToken = default) => (await repository.GetAllAsync(cancellationToken)).Select(ClienteMapper.ToResponse).ToList();
    public async Task<ClienteResponseDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => ClienteMapper.ToResponse(await repository.GetByIdAsync(id, cancellationToken) ?? throw new EntityNotFoundException("Cliente", id));
}

internal static class ClienteMapper
{
    public static Cliente ToEntity(CreateClienteDto input) => new()
    {
        Nombre = input.Nombre, Sector = input.Sector, PaisId = input.PaisId, RegionId = input.RegionId, CiudadId = input.CiudadId,
        Estado = input.Estado, EtapaId = input.EtapaId, Ciudad = input.Ciudad, Direccion = input.Direccion, Web = input.Web,
        Contacto = input.Contacto, CargoContacto = input.CargoContacto, ValorReferencia = input.ValorReferencia, Notas = input.Notas
    };
    public static void Apply(CreateClienteDto input, Cliente cliente)
    {
        cliente.Nombre = input.Nombre; cliente.Sector = input.Sector; cliente.PaisId = input.PaisId; cliente.RegionId = input.RegionId;
        cliente.CiudadId = input.CiudadId; cliente.Estado = input.Estado; cliente.EtapaId = input.EtapaId; cliente.Ciudad = input.Ciudad; cliente.Direccion = input.Direccion;
        cliente.Web = input.Web; cliente.Contacto = input.Contacto; cliente.CargoContacto = input.CargoContacto;
        cliente.ValorReferencia = input.ValorReferencia; cliente.Notas = input.Notas;
    }
    public static ClienteResponseDto ToResponse(Cliente cliente) => new()
    {
        Id = cliente.Id, Nombre = cliente.Nombre, Sector = cliente.Sector, PaisId = cliente.PaisId, RegionId = cliente.RegionId,
        CiudadId = cliente.CiudadId, Estado = cliente.Estado, EtapaId = cliente.EtapaId, Ciudad = cliente.Ciudad, Direccion = cliente.Direccion,
        Web = cliente.Web, Contacto = cliente.Contacto, CargoContacto = cliente.CargoContacto,
        ValorReferencia = cliente.ValorReferencia, Notas = cliente.Notas, CreatedAt = cliente.CreatedAt, UpdatedAt = cliente.UpdatedAt,
        Telefonos = cliente.Telefonos.Select(phone => new ClienteTelefonoDto { Id = phone.Id, Telefono = phone.Telefono, Etiqueta = phone.Etiqueta }).ToList(),
        Emails = cliente.Emails.Select(mail => new ClienteEmailDto { Id = mail.Id, Email = mail.Email, Etiqueta = mail.Etiqueta }).ToList()
    };
}

public class EliminarClienteUseCase(
    IClienteRepository repository, IHistorialCambioRepository historial,
    IUsuarioRepository usuarios, INotificacionRepository notificaciones, IUnitOfWork unitOfWork) : IEliminarClienteUseCase
{
    public async Task ExecuteAsync(Guid id, Guid usuarioId, string? rol, CancellationToken cancellationToken = default)
    {
        if (await repository.GetByIdAsync(id, cancellationToken) is null) throw new EntityNotFoundException("Cliente", id);
        await repository.DeleteAsync(id, cancellationToken);
        await HistorialRegistrador.RegistrarEliminacionAsync(historial, "cliente", id, usuarioId, cancellationToken);
        // Eliminar aca es SuperAdminOnly (Alicia 2026-09-18, corrigio la decision anterior:
        // antes tambien podian manager/admin/director) -- ya no hace falta notificar a nadie
        // aparte, porque solo hay un rol que puede llegar a este punto.
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static async Task NotificarAdministradoresAsync(IUsuarioRepository usuarios, INotificacionRepository notificaciones, Guid directorId, string tipoEntidad, Guid entidadId, CancellationToken ct)
    {
        var director = await usuarios.GetByIdAsync(directorId, ct);
        var nombreDirector = director is null ? "Un director" : $"{director.Nombre} {director.Apellido}".Trim();
        foreach (var adminId in await SolicitarEliminacionUseCase.IdsAdministradoresAsync(usuarios, ct))
            await notificaciones.AddAsync(NotificacionFactory.EliminacionDirectaDirector(adminId, tipoEntidad, entidadId, nombreDirector), ct);
    }
}

/// <summary>
/// "A qué cliente prestarle atención" (docs/21, docs/24): puntúa con <see cref="PrioridadClienteCalculador"/>
/// todos los clientes (recencia de su último proyecto + cuántos proyectos activos tiene ahora
/// mismo), ordenados de mayor a menor puntaje.
/// </summary>
public class ConsultarPrioridadClientesUseCase(IClienteRepository repository, ICatalogosRepository catalogos) : IConsultarPrioridadClientesUseCase
{
    public async Task<IReadOnlyList<ClientePrioridadResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var estados = await catalogos.GetEstadosAsync(null, ct);
        var idsTerminales = estados.Where(e => EstadosProyectoTerminales.Nombres.Contains(e.Nombre)).Select(e => e.Id).ToHashSet();
        var ahora = DateTime.UtcNow;

        return (await repository.GetAllAsync(ct))
            .Select(c =>
            {
                var ultimoProyecto = c.Proyectos.Count > 0 ? c.Proyectos.Max(p => p.CreatedAt) : (DateTime?)null;
                var proyectosActivos = c.Proyectos.Count(p => !idsTerminales.Contains(p.EstadoId));
                var resultado = PrioridadClienteCalculador.Calcular(c, ultimoProyecto, proyectosActivos, ahora);
                return new ClientePrioridadResponseDto { ClienteId = c.Id, Nombre = c.Nombre, Puntaje = resultado.Puntaje, Razones = resultado.Razones.ToList() };
            })
            .OrderByDescending(x => x.Puntaje).ThenBy(x => x.Nombre)
            .ToList();
    }
}
