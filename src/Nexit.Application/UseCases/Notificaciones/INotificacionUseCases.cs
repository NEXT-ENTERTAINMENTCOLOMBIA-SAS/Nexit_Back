using Nexit.Application.DTOs.Notificaciones;

namespace Nexit.Application.UseCases.Notificaciones;

public interface IListarMisNotificacionesUseCase
{
    Task<IReadOnlyList<NotificacionResponseDto>> ExecuteAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IMarcarNotificacionLeidaUseCase
{
    /// <summary>Solo el destinatario puede marcar su propia notificación como leída -- lanza ForbiddenOperationException si no lo es.</summary>
    Task ExecuteAsync(Guid notificacionId, Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IDescartarNotificacionUseCase
{
    /// <summary>Solo el destinatario puede descartar su propia notificación -- lanza ForbiddenOperationException si no lo es.</summary>
    Task ExecuteAsync(Guid notificacionId, Guid usuarioId, CancellationToken cancellationToken = default);
}
