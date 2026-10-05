using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.PanelPm;
using Nexit.Application.UseCases.PanelPm;

namespace Nexit.API.Controllers;

/// <summary>
/// Panel de Project Managers -- reemplaza al módulo de Informes (2026-10-05). Solo lectura y en vivo:
/// quién es Project Manager de qué proyectos, con cuántas personas, qué clientes y qué proveedores.
/// Lo ve cualquier usuario con perfil (2026-10-05, pedido de Alicia: "que todos puedan ver qué
/// proyectos tienen"): admin y super_admin ven a todos los Project Managers; el resto, solo los
/// proyectos que tiene a su cargo.
/// </summary>
[Authorize]
public class PanelController(IConsultarPanelProjectManagersUseCase consultar) : BaseController
{
    [HttpGet("project-managers")]
    public async Task<ActionResult<PanelPmDto>> GetProjectManagers(CancellationToken ct)
    {
        var esAdmin = GetUserRole() is "admin" or "super_admin";
        if (esAdmin) return Ok(await consultar.ExecuteAsync(ct));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await consultar.ExecuteAsync(ct, userId));
    }
}
