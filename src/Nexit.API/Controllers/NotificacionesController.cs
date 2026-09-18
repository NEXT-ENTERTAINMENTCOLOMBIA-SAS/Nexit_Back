using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Notificaciones;
using Nexit.Application.UseCases.Notificaciones;

namespace Nexit.API.Controllers;

/// <summary>Bandeja de notificaciones del usuario autenticado (docs/19) -- solo la propia, nunca la de alguien más.</summary>
public class NotificacionesController(
    IListarMisNotificacionesUseCase listar, IMarcarNotificacionLeidaUseCase marcarLeida, IDescartarNotificacionUseCase descartar) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificacionResponseDto>>> GetMisNotificaciones(CancellationToken ct) => Ok(await listar.ExecuteAsync(GetUserId(), ct));

    [HttpPut("{id:guid}/marcar-leida")]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken ct)
    {
        await marcarLeida.ExecuteAsync(id, GetUserId(), ct);
        return NoContent();
    }

    // Ícono de X para ir descartando notificaciones ya vistas (Alicia 2026-09-18) -- a diferencia de
    // marcar-leida, esto la borra de verdad de la bandeja de quien la descarta.
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Descartar(Guid id, CancellationToken ct)
    {
        await descartar.ExecuteAsync(id, GetUserId(), ct);
        return NoContent();
    }
}
