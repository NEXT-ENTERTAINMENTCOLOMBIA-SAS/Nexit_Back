using Nexit.Application.DTOs.Notificaciones;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Notificaciones;

public class ListarMisNotificacionesUseCase(INotificacionRepository repository) : IListarMisNotificacionesUseCase
{
    public async Task<IReadOnlyList<NotificacionResponseDto>> ExecuteAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        (await repository.GetPorUsuarioAsync(usuarioId, cancellationToken)).Select(NotificacionMapper.ToResponse).ToList();
}

public class MarcarNotificacionLeidaUseCase(INotificacionRepository repository, IUnitOfWork unitOfWork) : IMarcarNotificacionLeidaUseCase
{
    public async Task ExecuteAsync(Guid notificacionId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var notificacion = await repository.GetByIdAsync(notificacionId, cancellationToken) ?? throw new EntityNotFoundException("Notificacion", notificacionId);
        if (notificacion.UsuarioDestinatarioId != usuarioId) throw new ForbiddenOperationException("Esta notificación no es tuya.");
        if (!notificacion.Leida) { notificacion.Leida = true; notificacion.FechaLeida = DateTime.UtcNow; repository.Update(notificacion); await unitOfWork.SaveChangesAsync(cancellationToken); }
    }
}

/// <summary>
/// Descartar (el ícono de X del panel, Alicia 2026-09-18) borra la notificación de verdad -- a
/// diferencia de marcar-leida, que la conserva como historial. Solo quita esta de la bandeja de
/// quien la descarta; no toca la solicitud/entidad que la originó ni la bandeja de nadie más.
/// </summary>
public class DescartarNotificacionUseCase(INotificacionRepository repository, IUnitOfWork unitOfWork) : IDescartarNotificacionUseCase
{
    public async Task ExecuteAsync(Guid notificacionId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var notificacion = await repository.GetByIdAsync(notificacionId, cancellationToken) ?? throw new EntityNotFoundException("Notificacion", notificacionId);
        if (notificacion.UsuarioDestinatarioId != usuarioId) throw new ForbiddenOperationException("Esta notificación no es tuya.");
        await repository.DeleteAsync(notificacionId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

internal static class NotificacionMapper
{
    public static NotificacionResponseDto ToResponse(Notificacion n) => new()
    {
        Id = n.Id, Tipo = n.Tipo, Titulo = n.Titulo, Mensaje = n.Mensaje, TipoEntidad = n.TipoEntidad,
        EntidadId = n.EntidadId, SolicitudId = n.SolicitudId, Leida = n.Leida, FechaCreacion = n.FechaCreacion, FechaLeida = n.FechaLeida
    };
}

/// <summary>
/// Construye las notificaciones del flujo de solicitudes de eliminación (docs/19) -- centralizado
/// acá para que los 5 casos de uso de <c>SolicitudEliminacionUseCases.cs</c> que las disparan no
/// repitan la construcción del texto cada uno por su lado.
/// </summary>
internal static class NotificacionFactory
{
    /// <summary>
    /// "un cliente" / "una cuenta de usuario"... -- existe porque los textos se construyen
    /// interpolando <c>TipoEntidad</c>, y desde que se puede solicitar eliminar personas (docs/40)
    /// "eliminar un usuario" sonaba a inventario. Ante un tipo desconocido cae en algo neutro en vez
    /// de romper: estas notificaciones nunca deben tumbar la operación que las dispara.
    /// </summary>
    internal static string Articulo(string tipoEntidad) => tipoEntidad switch
    {
        "cliente" => "un cliente",
        "proveedor" => "un proveedor",
        "proyecto" => "un proyecto",
        "usuario" => "una cuenta de usuario",
        _ => "un registro"
    };

    public static Notificacion SolicitudCreadaParaGerente(Guid gerenteId, SolicitudEliminacion solicitud) => new()
    {
        UsuarioDestinatarioId = gerenteId, Tipo = "solicitud_eliminacion_creada",
        Titulo = $"Te pidieron eliminar {Articulo(solicitud.TipoEntidad)}",
        Mensaje = $"Alguien de tu equipo solicitó eliminar {Articulo(solicitud.TipoEntidad)} que lideras. Motivo: {solicitud.Motivo ?? "(sin motivo indicado)"}.",
        TipoEntidad = solicitud.TipoEntidad, EntidadId = solicitud.EntidadId, SolicitudId = solicitud.Id
    };

    public static Notificacion SolicitudCreadaParaAdmin(Guid adminId, SolicitudEliminacion solicitud, int totalPendientesParaEstaEntidad) => new()
    {
        UsuarioDestinatarioId = adminId, Tipo = "solicitud_eliminacion_creada",
        Titulo = $"Solicitud para eliminar {Articulo(solicitud.TipoEntidad)}",
        Mensaje = totalPendientesParaEstaEntidad > 1
            ? $"Motivo: {solicitud.Motivo ?? "(sin motivo indicado)"}. Ya van {totalPendientesParaEstaEntidad} solicitudes pendientes para {Articulo(solicitud.TipoEntidad)} igual."
            : $"Motivo: {solicitud.Motivo ?? "(sin motivo indicado)"}.",
        TipoEntidad = solicitud.TipoEntidad, EntidadId = solicitud.EntidadId, SolicitudId = solicitud.Id
    };

    public static Notificacion GerenteEndoso(Guid adminId, SolicitudEliminacion solicitud) => new()
    {
        UsuarioDestinatarioId = adminId, Tipo = "solicitud_eliminacion_endosada",
        Titulo = $"El gerente responsable ya aprobó eliminar {Articulo(solicitud.TipoEntidad)}",
        Mensaje = $"Falta tu decisión final para completar esta eliminación.",
        TipoEntidad = solicitud.TipoEntidad, EntidadId = solicitud.EntidadId, SolicitudId = solicitud.Id
    };

    /// <summary>
    /// Un director eliminó un cliente/proveedor/proyecto DIRECTO, sin pasar por una solicitud
    /// (Alicia 2026-09-09: "los directores sí podrían borrar proyectos... claramente se recibe la
    /// notificación al administrador"). A diferencia de las demás notificaciones de este archivo,
    /// esta no nace de una <see cref="SolicitudEliminacion"/> -- no existe ninguna, el borrado ya
    /// ocurrió -- así que la arman directamente EliminarClienteUseCase/EliminarProveedorUseCase/
    /// EliminarProyectoUseCase.
    /// </summary>
    public static Notificacion EliminacionDirectaDirector(Guid adminId, string tipoEntidad, Guid entidadId, string nombreDirector) => new()
    {
        UsuarioDestinatarioId = adminId, Tipo = "eliminacion_directa_director",
        Titulo = $"Un director eliminó {Articulo(tipoEntidad)}",
        Mensaje = $"{nombreDirector} eliminó {Articulo(tipoEntidad)} directamente, sin pasar por una solicitud.",
        TipoEntidad = tipoEntidad, EntidadId = entidadId
    };

    /// <summary>
    /// Le avisa a quien invitó que ya le respondieron (2026-09-08). Sin esto, la única forma de
    /// enterarse era entrar a Usuarios y notar que la invitación desapareció de la lista de
    /// pendientes -- nadie revisa eso a diario, así que en la práctica no se enteraba.
    /// </summary>
    public static Notificacion? InvitacionRespondida(Guid? invitadoPorId, string email, bool aceptada) => invitadoPorId is null ? null : new()
    {
        UsuarioDestinatarioId = invitadoPorId.Value,
        Tipo = aceptada ? "invitacion_aceptada" : "invitacion_rechazada",
        Titulo = aceptada ? "Alguien aceptó tu invitación" : "Rechazaron tu invitación",
        Mensaje = aceptada
            ? $"{email} ya creó su perfil y puede entrar a Nexit."
            : $"{email} rechazó la invitación, así que no se creó ningún perfil.",
    };

    /// <summary>
    /// <c>null</c> cuando quien la pidió ya no está en el equipo (SolicitadoPorId quedó en null al
    /// eliminar su cuenta): no hay a quién avisarle, y la decisión se toma igual.
    /// </summary>
    public static Notificacion? DecisionParaSolicitante(SolicitudEliminacion solicitud, bool aprobada, string? comentario) => solicitud.SolicitadoPorId is null ? null : new()
    {
        UsuarioDestinatarioId = solicitud.SolicitadoPorId.Value, Tipo = "solicitud_eliminacion_decidida",
        Titulo = aprobada ? $"Aprobaron tu solicitud de eliminar {Articulo(solicitud.TipoEntidad)}" : $"Rechazaron tu solicitud de eliminar {Articulo(solicitud.TipoEntidad)}",
        // El comentario de quien decidió se agrega DESPUÉS de la frase, no la reemplaza: antes, si
        // escribía algo, la notificación era solo ese texto suelto y quien la recibía no sabía si le
        // habían dicho que sí o que no.
        Mensaje = (aprobada ? "Se eliminó según lo solicitado." : "No se eliminó.")
                  + (string.IsNullOrWhiteSpace(comentario) ? "" : $" {comentario.Trim()}"),
        TipoEntidad = solicitud.TipoEntidad, EntidadId = solicitud.EntidadId, SolicitudId = solicitud.Id
    };
}
