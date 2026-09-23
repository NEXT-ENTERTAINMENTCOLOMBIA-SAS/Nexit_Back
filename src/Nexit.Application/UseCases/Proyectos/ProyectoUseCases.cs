using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.UseCases.Clientes;
using Nexit.Application.UseCases.Historial;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;
using Nexit.Core.Utils;

namespace Nexit.Application.UseCases.Proyectos;

public class CrearProyectoUseCase(IProyectoRepository repository, IClienteRepository clientes, IProveedorRepository proveedores, ICatalogosRepository catalogos, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : ICrearProyectoUseCase
{
    public async Task<ProyectoResponseDto> ExecuteAsync(CrearProyectoDto input, Guid usuarioId, string? usuarioRol, CancellationToken ct = default)
    {
        await ProyectoRules.ValidarReferencias(input, clientes, proveedores, catalogos, ct);
        var proyecto = ProyectoMapper.ToEntity(input); proyecto.CreatedBy = usuarioId;
        // Solo un administrador (o superior) puede asignar explícitamente el gerente responsable al
        // crear. Si quien crea el proyecto ya es gerente y no se especificó uno, se asigna a sí mismo
        // como dueño; en cualquier otro caso, el proyecto queda sin gerente hasta que se asigne.
        proyecto.GerenteId = ProyectoRules.EsAdminOAbove(usuarioRol) ? input.GerenteId
            : usuarioRol == Roles.Manager ? usuarioId
            : null;
        await repository.AddAsync(proyecto, ct);
        await HistorialRegistrador.RegistrarCreacionAsync(historial, "proyecto", proyecto.Id, usuarioId, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ProyectoMapper.ToResponse(proyecto);
    }
}

public class ActualizarProyectoUseCase(IProyectoRepository repository, IClienteRepository clientes, IProveedorRepository proveedores, ICatalogosRepository catalogos, IHistorialCambioRepository historial, IUnitOfWork unitOfWork) : IActualizarProyectoUseCase
{
    public async Task<ProyectoResponseDto> ExecuteAsync(ActualizarProyectoDto input, Guid usuarioId, string? usuarioRol, CancellationToken ct = default)
    {
        var proyecto = await repository.GetByIdAsync(input.Id, ct) ?? throw new EntityNotFoundException("Proyecto", input.Id);
        var antes = CambioDetector.Snapshot(proyecto);
        await ProyectoRules.ValidarReferencias(input, clientes, proveedores, catalogos, ct);
        if (input.GerenteId != proyecto.GerenteId && !ProyectoRules.EsAdminOAbove(usuarioRol))
            throw new ForbiddenOperationException("Solo un administrador puede reasignar el gerente responsable de un proyecto.");
        ProyectoMapper.Apply(input, proyecto); proyecto.UpdatedAt = DateTime.UtcNow; proyecto.UpdatedBy = usuarioId;
        repository.Update(proyecto);
        await HistorialRegistrador.RegistrarEdicionAsync(historial, "proyecto", proyecto.Id, usuarioId, antes, proyecto, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ProyectoMapper.ToResponse(proyecto);
    }
}

public class ConsultarProyectosUseCase(IProyectoRepository repository) : IConsultarProyectosUseCase
{
    public async Task<IReadOnlyList<ProyectoResponseDto>> ListAsync(CancellationToken ct = default) => (await repository.GetAllAsync(ct)).Select(ProyectoMapper.ToResponse).ToList();
    public async Task<ProyectoResponseDto> GetByIdAsync(Guid id, CancellationToken ct = default) => ProyectoMapper.ToResponse(await repository.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException("Proyecto", id));
}

/// <summary>
/// "A qué proyecto atender primero" (docs/21, docs/22) -- Nivel 1 de la propuesta: puntúa con
/// <see cref="PrioridadProyectoCalculador"/> todos los proyectos que no estén ya en un estado
/// terminal (no tiene sentido priorizar algo que ya se finalizó, canceló, no se ejecutó o ya se
/// facturó), y los devuelve ordenados de mayor a menor puntaje.
/// </summary>
public class ConsultarPrioridadProyectosUseCase(IProyectoRepository repository, ICatalogosRepository catalogos) : IConsultarPrioridadProyectosUseCase
{
    public async Task<IReadOnlyList<ProyectoPrioridadResponseDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var estados = await catalogos.GetEstadosAsync(null, ct);
        // Set de nombres centralizado en EstadosProyectoTerminales.Nombres (docs/24) -- lo comparte
        // con ConsultarPrioridadClientesUseCase, antes estaba duplicado acá.
        var idsTerminales = estados.Where(e => EstadosProyectoTerminales.Nombres.Contains(e.Nombre)).Select(e => e.Id).ToHashSet();

        var ahora = DateTime.UtcNow;
        return (await repository.GetAllAsync(ct))
            .Where(p => !idsTerminales.Contains(p.EstadoId))
            .Select(p =>
            {
                // La entrada más reciente de la bitácora de seguimiento es la señal de "última
                // actividad" (docs/21) -- si todavía no tiene ninguna, se usa la fecha de creación,
                // así un proyecto recién creado también puede puntuarse (no queda sin actividad = null).
                var ultimaActividad = p.Seguimiento.Count > 0 ? p.Seguimiento.Max(s => s.Fecha) : p.CreatedAt;
                var resultado = PrioridadProyectoCalculador.Calcular(p, ultimaActividad, ahora);
                return new ProyectoPrioridadResponseDto { ProyectoId = p.Id, Nombre = p.Nombre, Puntaje = resultado.Puntaje, Razones = resultado.Razones.ToList() };
            })
            .OrderByDescending(x => x.Puntaje).ThenBy(x => x.Nombre)
            .ToList();
    }
}

public class EliminarProyectoUseCase(
    IProyectoRepository repository, IHistorialCambioRepository historial,
    IUsuarioRepository usuarios, INotificacionRepository notificaciones, IUnitOfWork unitOfWork) : IEliminarProyectoUseCase
{
    public async Task ExecuteAsync(Guid id, Guid usuarioId, string? rol, CancellationToken ct = default)
    {
        if (await repository.GetByIdAsync(id, ct) is null) throw new EntityNotFoundException("Proyecto", id);
        await repository.DeleteAsync(id, ct);
        await HistorialRegistrador.RegistrarEliminacionAsync(historial, "proyecto", id, usuarioId, ct);
        // Eliminar aca es SuperAdminOnly (Alicia 2026-09-18, corrigio la decision anterior:
        // antes tambien podian manager/admin/director) -- ya no hace falta notificar a nadie
        // aparte, porque solo hay un rol que puede llegar a este punto.
        await unitOfWork.SaveChangesAsync(ct);
    }
}

public class AgregarSeguimientoProyectoUseCase(IProyectoRepository repository, IUnitOfWork unitOfWork) : IAgregarSeguimientoProyectoUseCase
{
    public async Task<SeguimientoProyectoDto> ExecuteAsync(Guid proyectoId, CrearSeguimientoProyectoDto input, Guid usuarioId, CancellationToken ct = default)
    {
        var proyecto = await repository.GetByIdAsync(proyectoId, ct) ?? throw new EntityNotFoundException("Proyecto", proyectoId);
        var seguimiento = new ProyectoSeguimiento { ProyectoId = proyecto.Id, AutorId = usuarioId, Area = input.Area, Fecha = input.Fecha ?? DateTime.UtcNow, Nota = input.Nota };
        proyecto.Seguimiento.Add(seguimiento); await unitOfWork.SaveChangesAsync(ct);
        return ProyectoMapper.ToResponse(seguimiento);
    }
}

/// <summary>Lista la bitácora completa de un proyecto (docs/29 -- gap encontrado al conectar el frontend: antes solo existía el POST para agregar, sin GET para listar lo que ya había).</summary>
public class ConsultarSeguimientoProyectoUseCase(IProyectoRepository repository) : IConsultarSeguimientoProyectoUseCase
{
    public async Task<IReadOnlyList<SeguimientoProyectoDto>> ExecuteAsync(Guid proyectoId, CancellationToken ct = default)
    {
        var proyecto = await repository.GetByIdAsync(proyectoId, ct) ?? throw new EntityNotFoundException("Proyecto", proyectoId);
        return proyecto.Seguimiento.OrderByDescending(s => s.Fecha).Select(ProyectoMapper.ToResponse).ToList();
    }
}

internal static class ProyectoMapper
{
    public static Proyecto ToEntity(CrearProyectoDto dto) { var entity = new Proyecto(); Apply(dto, entity); return entity; }
    public static void Apply(CrearProyectoDto dto, Proyecto entity)
    {
        entity.Nombre = dto.Nombre; entity.ClienteId = dto.ClienteId; entity.ContactoProyecto = dto.ContactoProyecto; entity.TipoProyecto = dto.TipoProyecto; entity.Prioridad = dto.Prioridad; entity.Ciudad = dto.Ciudad; entity.SedeNext = dto.SedeNext; entity.FechaSolicitud = dto.FechaSolicitud; entity.FechaEvento = dto.FechaEvento; entity.EstadoId = dto.EstadoId; entity.PorcentajeAvance = dto.PorcentajeAvance; entity.EstadoBrief = dto.EstadoBrief; entity.PropuestaEstado = dto.PropuestaEstado; entity.NumeroFactura = dto.NumeroFactura; entity.Pagado = dto.Pagado; entity.FechaPago = dto.FechaPago; entity.Notas = dto.Notas; entity.GerenteId = dto.GerenteId;
        // Guid.Empty (no Guid.NewGuid()) para los miembros de equipo nuevos -- ver el comentario detallado
        // en ActualizarClienteUseCase (ClienteUseCases.cs) sobre por qué un Id ya asignado hace que EF Core
        // confunda un ProyectoEquipo nuevo con uno existente cuando el proyecto padre ya está rastreado.
        entity.Equipo.Clear(); foreach (var miembro in dto.Equipo) entity.Equipo.Add(new ProyectoEquipo { Id = miembro.Id ?? Guid.Empty, ProyectoId = entity.Id, Rol = miembro.Rol, Nombre = miembro.Nombre });
        entity.Proveedores.Clear(); foreach (var proveedorId in dto.ProveedorIds.Distinct()) entity.Proveedores.Add(new ProyectoProveedor { ProyectoId = entity.Id, ProveedorId = proveedorId });
    }
    public static ProyectoResponseDto ToResponse(Proyecto entity) => new()
    {
        Id = entity.Id, Nombre = entity.Nombre, ClienteId = entity.ClienteId, ContactoProyecto = entity.ContactoProyecto, TipoProyecto = entity.TipoProyecto, Prioridad = entity.Prioridad, Ciudad = entity.Ciudad, SedeNext = entity.SedeNext, FechaSolicitud = entity.FechaSolicitud, FechaEvento = entity.FechaEvento, EstadoId = entity.EstadoId, PorcentajeAvance = entity.PorcentajeAvance, EstadoBrief = entity.EstadoBrief, PropuestaEstado = entity.PropuestaEstado, NumeroFactura = entity.NumeroFactura, Pagado = entity.Pagado, FechaPago = entity.FechaPago, Notas = entity.Notas, GerenteId = entity.GerenteId, CreatedAt = entity.CreatedAt, UpdatedAt = entity.UpdatedAt,
        Equipo = entity.Equipo.Select(x => new ProyectoEquipoDto { Id = x.Id, Rol = x.Rol, Nombre = x.Nombre }).ToList(), ProveedorIds = entity.Proveedores.Select(x => x.ProveedorId).ToList()
    };
    public static SeguimientoProyectoDto ToResponse(ProyectoSeguimiento entity) => new() { Id = entity.Id, AutorId = entity.AutorId, Area = entity.Area, Fecha = entity.Fecha, Nota = entity.Nota, CreatedAt = entity.CreatedAt };
}

internal static class ProyectoRules
{
    public static bool EsAdminOAbove(string? rol) => rol is Roles.Admin or Roles.SuperAdmin;

    public static async Task ValidarReferencias(CrearProyectoDto dto, IClienteRepository clientes, IProveedorRepository proveedores, ICatalogosRepository catalogos, CancellationToken ct)
    {
        if (dto.ClienteId.HasValue && await clientes.GetByIdAsync(dto.ClienteId.Value, ct) is null) throw new BusinessRuleException("El cliente indicado no existe.");
        if (await catalogos.GetEstadoAsync(dto.EstadoId, ct) is null) throw new BusinessRuleException("El estado indicado no existe.");
        foreach (var proveedorId in dto.ProveedorIds.Distinct())
            if (await proveedores.GetByIdAsync(proveedorId, ct) is null) throw new BusinessRuleException("Uno de los proveedores indicados no existe.");
    }
}
