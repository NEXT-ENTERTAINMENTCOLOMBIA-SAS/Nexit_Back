using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Core.Exceptions;

namespace Nexit.API.Controllers;

public class ProyectosController(ICrearProyectoUseCase crear, IActualizarProyectoUseCase actualizar, IConsultarProyectosUseCase consultar, IEliminarProyectoUseCase eliminar, IAgregarSeguimientoProyectoUseCase agregarSeguimiento, IConsultarSeguimientoProyectoUseCase consultarSeguimiento, IConsultarPrioridadProyectosUseCase consultarPrioridad, IProyectosImportExporter importExporter, IValidator<CrearProyectoDto> createValidator, IValidator<ActualizarProyectoDto> updateValidator, IValidator<CrearSeguimientoProyectoDto> seguimientoValidator) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProyectoResponseDto>>> GetAll(CancellationToken ct) => Ok(await consultar.ListAsync(ct));

    // Antes de "{id:guid}" a propósito -- si no, ASP.NET Core intenta parsear "prioridad"/"exportar" como Guid y falla con 404.
    /// <summary>"A qué proyecto atender primero" (docs/21, docs/22) -- puntuado y ordenado, con las razones de cada puntaje.</summary>
    [HttpGet("prioridad")]
    public async Task<ActionResult<IReadOnlyList<ProyectoPrioridadResponseDto>>> GetPrioridad(CancellationToken ct) => Ok(await consultarPrioridad.ExecuteAsync(ct));

    /// <summary>Descarga todos los proyectos como .xlsx (docs/31) -- mismo formato que espera <see cref="Importar"/>, así que sirve también como plantilla. No incluye equipo/proveedores/gerente (son relaciones, se completan en la pantalla de edición).</summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        var bytes = await importExporter.ExportarAsync(await consultar.ListAsync(ct), ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"proyectos-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    /// <summary>Carga masiva desde .xlsx (docs/31) -- Cliente/Estado se resuelven por nombre; una fila inválida no detiene el resto.</summary>
    [HttpPost("importar"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ImportarResultadoDto>> Importar(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0) throw new BusinessRuleException("Debes adjuntar un archivo .xlsx.");
        await using var contenido = archivo.OpenReadStream();
        return Ok(await importExporter.ImportarAsync(contenido, GetUserId(), GetUserRole(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProyectoResponseDto>> GetById(Guid id, CancellationToken ct) => Ok(await consultar.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<ProyectoResponseDto>> Create(CrearProyectoDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        var result = await crear.ExecuteAsync(dto, userId, GetUserRole(), ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProyectoResponseDto>> Update(Guid id, ActualizarProyectoDto dto, CancellationToken ct)
    {
        dto.Id = id; var validation = await updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await actualizar.ExecuteAsync(dto, userId, GetUserRole(), ct));
    }

    // Solo el super_admin elimina directo, sin pasar por SolicitudesEliminacionController (Alicia
    // 2026-09-18, corrige la decisión anterior del 2026-09-09 que dejaba entrar aquí también a
    // director/admin). Cualquier otro rol -- admin incluido -- tiene que pedirlo y esperar que un
    // administrador o el super_admin lo apruebe (ver SolicitudesEliminacionController).
    [HttpDelete("{id:guid}"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await eliminar.ExecuteAsync(id, GetUserId(), GetUserRole(), ct); return NoContent(); }

    [HttpPost("{id:guid}/seguimiento")]
    public async Task<ActionResult<SeguimientoProyectoDto>> AddSeguimiento(Guid id, CrearSeguimientoProyectoDto dto, CancellationToken ct)
    {
        var validation = await seguimientoValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await agregarSeguimiento.ExecuteAsync(id, dto, userId, ct));
    }

    /// <summary>La bitácora completa del proyecto, más reciente primero -- agregado 2026-08-26 (antes solo existía el POST de arriba, sin forma de listar lo que ya había).</summary>
    [HttpGet("{id:guid}/seguimiento")]
    public async Task<ActionResult<IReadOnlyList<SeguimientoProyectoDto>>> GetSeguimiento(Guid id, CancellationToken ct) => Ok(await consultarSeguimiento.ExecuteAsync(id, ct));
}
