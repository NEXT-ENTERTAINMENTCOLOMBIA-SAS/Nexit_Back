using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Configuracion;
using Nexit.Application.UseCases.Configuracion;

namespace Nexit.API.Controllers;

/// <summary>
/// Configuración editable de todo el sistema (2026-10-05). Leer los nombres de los roles y las listas
/// de Proyectos lo puede hacer cualquier persona autenticada (las pantallas las necesitan para
/// mostrarse); cambiarlos es de admin/super_admin. Los dominios de correo permitidos deciden quién
/// puede ser invitado, así que verlos y cambiarlos es solo de super_admin.
/// Los catálogos clásicos (ubicaciones, categorías, servicios, estados, etapas) siguen en /api/catalogos.
/// </summary>
public class ConfiguracionController(IConfiguracionService configuracion) : BaseController
{
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RolConfigDto>>> GetRoles(CancellationToken ct) => Ok(await configuracion.GetRolesAsync(ct));

    [HttpPut("roles/{rol}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<RolConfigDto>> ActualizarRol(string rol, ActualizarRolConfigDto dto, CancellationToken ct) => Ok(await configuracion.ActualizarRolAsync(rol, dto, ct));

    [HttpGet("opciones")]
    public async Task<ActionResult<IReadOnlyDictionary<string, List<OpcionConfigDto>>>> GetOpciones(CancellationToken ct) => Ok(await configuracion.GetOpcionesAsync(ct));

    [HttpPost("opciones/{lista}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<OpcionConfigDto>> CrearOpcion(string lista, GuardarOpcionDto dto, CancellationToken ct) => Ok(await configuracion.CrearOpcionAsync(lista, dto, ct));

    [HttpPut("opciones/{lista}/{id:guid}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<OpcionConfigDto>> ActualizarOpcion(string lista, Guid id, GuardarOpcionDto dto, CancellationToken ct) => Ok(await configuracion.ActualizarOpcionAsync(lista, id, dto, ct));

    [HttpDelete("opciones/{lista}/{id:guid}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> EliminarOpcion(string lista, Guid id, CancellationToken ct) { await configuracion.EliminarOpcionAsync(lista, id, ct); return NoContent(); }

    [HttpGet("dominios-correo"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<IReadOnlyList<DominioCorreoDto>>> GetDominios(CancellationToken ct) => Ok(await configuracion.GetDominiosAsync(ct));

    [HttpPost("dominios-correo"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<ActionResult<DominioCorreoDto>> CrearDominio(GuardarDominioDto dto, CancellationToken ct) => Ok(await configuracion.CrearDominioAsync(dto, ct));

    [HttpDelete("dominios-correo/{id:guid}"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> EliminarDominio(Guid id, CancellationToken ct) { await configuracion.EliminarDominioAsync(id, ct); return NoContent(); }
}
