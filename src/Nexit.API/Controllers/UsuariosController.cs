using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.API.Filters;
using Nexit.Application.DTOs.Usuarios;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Usuarios;

namespace Nexit.API.Controllers;

/// <summary>
/// Gestión de cuentas de usuario. Tres niveles de acceso dentro del mismo controlador (actualizado
/// 2026-09-09, ver docs/06-modelo-permisos-roles.md sección 6):
///
///  - <b>Crear/editar</b> (<see cref="Create"/>, <see cref="Registrar"/>, <see cref="Update"/>):
///    <c>admin</c> o <c>super_admin</c> (<c>AdminOrAbove</c>) -- Alicia 2026-09-09: van a haber
///    varias administradoras que necesitan manejar usuarios ellas mismas, ya no exclusivo de
///    super_admin. Ninguna de las dos puede darle a nadie el rol de super_admin (ver
///    CrearUsuarioUseCase/ActualizarUsuarioUseCase -- un solo dueño del sistema, sembrado
///    directamente en la base). <b>Eliminar ya no vive acá</b>: pasa por
///    SolicitudesEliminacionController (ver el comentario al final de esta clase).
///  - <b>Listar a todos</b> (<see cref="GetAll"/>): también <c>admin</c>, no solo
///    <c>super_admin</c> (<c>AdminOrAbove</c>).
///  - <b>Ver un perfil individual</b> (<see cref="GetById"/>, <see cref="GetMe"/>): cualquier persona
///    autenticada, sin importar el rol -- solo lectura, como el directorio de personas de Microsoft
///    Teams: cualquiera puede mirar el perfil de un compañero, pero editarlo/eliminarlo sigue
///    exclusivo de admin/super_admin.
///
/// Crear un usuario aquí solo registra su perfil de negocio; la cuenta de acceso (correo, contraseña)
/// se invita primero desde Supabase Auth.
/// </summary>
public class UsuariosController(
    ICrearUsuarioUseCase crear,
    IActualizarUsuarioUseCase actualizar,
    IConsultarUsuariosUseCase consultar,
    IConsultarUsuariosEquipoUseCase consultarEquipo,
    IRegistrarUsuarioUseCase registrar,
    IUsuariosImportExporter importExporter,
    IValidator<CreateUsuarioDto> createValidator,
    IValidator<RegistrarUsuarioDto> registrarValidator,
    IValidator<UpdateUsuarioDto> updateValidator) : BaseController
{
    /// <summary>Directorio completo -- admin/super_admin (ver el resumen de la clase).</summary>
    [HttpGet, Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<IReadOnlyList<UsuarioResponseDto>>> GetAll(CancellationToken ct) => Ok(await consultar.ListAsync(ct));

    /// <summary>
    /// Quiénes se pueden agregar al equipo de un proyecto -- cualquier usuario activo, sin importar
    /// el rol (Alicia 2026-09-18). A diferencia de <see cref="GetAll"/>, CUALQUIER autenticado con
    /// perfil puede pedir esto: crear o editar un proyecto no es exclusivo de admin+ (<see
    /// cref="Nexit.API.Controllers.ProyectosController"/> no restringe por rol), así que buscar a
    /// quién agregar al equipo tampoco puede estarlo. Antes de "{id:guid}" a propósito.
    /// </summary>
    [HttpGet("equipo")]
    public async Task<ActionResult<IReadOnlyList<UsuarioEquipoDto>>> GetEquipo(CancellationToken ct) => Ok(await consultarEquipo.ListAsync(ct));

    /// <summary>
    /// El equipo como .xlsx -- mismo permiso que ver el directorio (admin/super_admin). No hay un
    /// "importar" simétrico acá: importar usuarios es invitarlos, y eso vive en
    /// <see cref="InvitacionesController.ImportarInvitaciones"/> (ver IUsuariosImportExporter).
    /// Antes de "{id:guid}" a propósito -- si no, ASP.NET Core intenta parsear "exportar" como Guid.
    /// </summary>
    [HttpGet("exportar"), Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        var bytes = importExporter.Exportar(await consultar.ListAsync(ct));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"usuarios-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    // Antes de "{id:guid}" a propósito -- "me" no es un Guid válido, así que no compite con esa ruta,
    // pero se pone primero para que quede junto al resto de rutas estáticas por convención del repo.
    /// <summary>Perfil propio -- cualquier autenticado, no exclusivo de super_admin (ver el resumen de la clase).</summary>
    // [PermitirSinPerfil]: es justamente el endpoint con el que el frontend averigua si la persona
    // ya tiene perfil -- si el filtro lo bloqueara, nunca podria distinguir "sin registrar" de
    // "sin permiso" y no sabria a donde mandarla (ver PerfilRequeridoFilter).
    [HttpGet("me"), PermitirSinPerfil]
    public async Task<ActionResult<UsuarioResponseDto>> GetMe(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        return Ok(await consultar.GetByIdAsync(userId, ct));
    }

    /// <summary>
    /// Perfil de OTRA persona, solo lectura -- cualquier autenticado (agregado 2026-08-26, ver el
    /// resumen de la clase). No expone nada que <see cref="Update"/>/<see cref="Delete"/> dejen
    /// modificar: quien llama esto no puede editar ni eliminar, solo mirar.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UsuarioResponseDto>> GetById(Guid id, CancellationToken ct) => Ok(await consultar.GetByIdAsync(id, ct));

    [HttpPost, Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UsuarioResponseDto>> Create(CreateUsuarioDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var result = await crear.ExecuteAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Da de alta a alguien de una vez, sin correo de invitación de por medio: crea su cuenta en
    /// Supabase Auth y su perfil en un solo paso (ver IRegistrarUsuarioUseCase). Alternativa a
    /// invitar, no reemplazo -- invitar sigue siendo el camino cuando se prefiere que la propia
    /// persona complete sus datos.
    /// </summary>
    [HttpPost("registrar"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UsuarioResponseDto>> Registrar(RegistrarUsuarioDto dto, CancellationToken ct)
    {
        var validation = await registrarValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var result = await registrar.ExecuteAsync(dto, GetUserId(), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<UsuarioResponseDto>> Update(Guid id, UpdateUsuarioDto dto, CancellationToken ct)
    {
        var validation = await updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        return Ok(await actualizar.ExecuteAsync(id, dto, GetUserId(), ct));
    }

    // Ya NO existe DELETE /api/usuarios/{id} (2026-09-08, docs/40). Eliminar a una persona dejó de
    // ser un botón directo: se pide con POST /api/solicitudeseliminacion { tipoEntidad: "usuario" },
    // le llega la notificación a todos los administradores y al super administrador, y el borrado
    // real lo ejecuta la aprobación de esa solicitud. Las únicas dos formas de que una cuenta
    // desaparezca son esa y la limpieza automática de los 30 días desactivada (docs/17).
}
