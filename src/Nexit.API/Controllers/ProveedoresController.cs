using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.DTOs.Proveedores;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Proveedores;
using Nexit.Core.Exceptions;

namespace Nexit.API.Controllers;

public class ProveedoresController(
    ICrearProveedorUseCase crear, IActualizarProveedorUseCase actualizar, IConsultarProveedoresUseCase consultar, IEliminarProveedorUseCase eliminar,
    IMarcarColaboradorProveedorUseCase marcarColaborador, IQuitarColaboradorProveedorUseCase quitarColaborador, IListarMisProveedoresUseCase listarMios,
    IConsultarPrioridadProveedoresUseCase consultarPrioridad, IProveedoresImportExporter importExporter,
    IValidator<CreateProveedorDto> createValidator, IValidator<UpdateProveedorDto> updateValidator) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProveedorResponseDto>>> GetAll(CancellationToken ct) => Ok(await consultar.ListAsync(ct));

    // Antes de "{id:guid}" a propósito -- si no, ASP.NET Core intenta parsear "mios"/"prioridad"/"exportar" como Guid y falla con 404.
    [HttpGet("mios")]
    public async Task<ActionResult<IReadOnlyList<ProveedorResponseDto>>> GetMisProveedores(CancellationToken ct) => Ok(await listarMios.ExecuteAsync(GetUserId(), ct));

    /// <summary>"A qué proveedor prestarle atención" (docs/21, docs/24) -- puntuado y ordenado, con las razones de cada puntaje.</summary>
    [HttpGet("prioridad")]
    public async Task<ActionResult<IReadOnlyList<ProveedorPrioridadResponseDto>>> GetPrioridad(CancellationToken ct) => Ok(await consultarPrioridad.ExecuteAsync(ct));

    /// <summary>Descarga todos los proveedores como .xlsx (docs/31) -- mismo formato que espera <see cref="Importar"/>, así que sirve también como plantilla.</summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(CancellationToken ct)
    {
        var bytes = importExporter.Exportar(await consultar.ListAsync(ct));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"proveedores-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    /// <summary>Carga masiva desde .xlsx (docs/31) -- País/Ciudad/Categoría se resuelven por nombre contra Catálogos; una fila inválida no detiene el resto.</summary>
    [HttpPost("importar"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ImportarResultadoDto>> Importar(IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0) throw new BusinessRuleException("Debes adjuntar un archivo .xlsx.");
        await using var contenido = archivo.OpenReadStream();
        return Ok(await importExporter.ImportarAsync(contenido, GetUserId(), ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProveedorResponseDto>> GetById(Guid id, CancellationToken ct) => Ok(await consultar.GetByIdAsync(id, ct));

    /// <summary>"Estoy trabajando con este proveedor" (docs/19) -- cada quien se marca a sí mismo, no un administrador.</summary>
    [HttpPost("{id:guid}/colaboradores")]
    public async Task<IActionResult> MarcarColaborador(Guid id, CancellationToken ct)
    {
        await marcarColaborador.ExecuteAsync(id, GetUserId(), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/colaboradores")]
    public async Task<IActionResult> QuitarColaborador(Guid id, CancellationToken ct)
    {
        await quitarColaborador.ExecuteAsync(id, GetUserId(), ct);
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<ProveedorResponseDto>> Create(CreateProveedorDto dto, CancellationToken ct)
    {
        var validation = await createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await crear.ExecuteAsync(dto, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ProveedorResponseDto>> Update(Guid id, UpdateProveedorDto dto, CancellationToken ct)
    {
        dto.Id = id;
        var validation = await updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await actualizar.ExecuteAsync(dto, userId, ct));
    }

    // Solo el super_admin elimina directo, sin pasar por SolicitudesEliminacionController (Alicia
    // 2026-09-18, corrige la decisión anterior del 2026-09-09 que dejaba entrar aquí también a
    // director/admin). Cualquier otro rol -- admin incluido -- tiene que pedirlo y esperar que un
    // administrador o el super_admin lo apruebe (ver SolicitudesEliminacionController).
    [HttpDelete("{id:guid}"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await eliminar.ExecuteAsync(id, GetUserId(), GetUserRole(), ct);
        return NoContent();
    }
}
