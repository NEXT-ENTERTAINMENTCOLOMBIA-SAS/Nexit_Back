using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.PanelPm;
using Nexit.Application.UseCases.PanelPm;

namespace Nexit.API.Controllers;

/// <summary>
/// Panel de Project Managers -- reemplaza al módulo de Informes (2026-10-05). Solo lectura y en vivo:
/// quién es Project Manager de qué proyectos, con cuántas personas, qué clientes y qué proveedores.
/// Misma restricción que tenían los informes: solo admin y super_admin.
/// </summary>
[Authorize(Policy = "AdminOrAbove")]
public class PanelController(IConsultarPanelProjectManagersUseCase consultar) : BaseController
{
    [HttpGet("project-managers")]
    public async Task<ActionResult<PanelPmDto>> GetProjectManagers(CancellationToken ct) => Ok(await consultar.ExecuteAsync(ct));
}
