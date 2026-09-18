using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Clientes;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Clientes;
using Nexit.Core.Exceptions;

namespace Nexit.API.Controllers;

public class ClientesController(ICrearClienteUseCase crear, IActualizarClienteUseCase actualizar, IConsultarClientesUseCase consultar, IEliminarClienteUseCase eliminar, IConsultarPrioridadClientesUseCase consultarPrioridad, IClientesImportExporter importExporter, IValidator<CreateClienteDto> createValidator, IValidator<UpdateClienteDto> updateValidator) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteResponseDto>>> GetAll(CancellationToken cancellationToken) => Ok(await consultar.ListAsync(cancellationToken));

    // Antes de "{id:guid}" a propósito -- si no, ASP.NET Core intenta parsear "prioridad"/"exportar" como Guid y falla con 404.
    /// <summary>"A qué cliente prestarle atención" (docs/21, docs/24) -- puntuado y ordenado, con las razones de cada puntaje.</summary>
    [HttpGet("prioridad")]
    public async Task<ActionResult<IReadOnlyList<ClientePrioridadResponseDto>>> GetPrioridad(CancellationToken cancellationToken) => Ok(await consultarPrioridad.ExecuteAsync(cancellationToken));

    /// <summary>Descarga todos los clientes como .xlsx (docs/31) -- mismo formato que espera <see cref="Importar"/>, así que sirve también como plantilla.</summary>
    [HttpGet("exportar")]
    public async Task<IActionResult> Exportar(CancellationToken cancellationToken)
    {
        var bytes = importExporter.Exportar(await consultar.ListAsync(cancellationToken));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"clientes-{DateTime.UtcNow:yyyyMMdd}.xlsx");
    }

    /// <summary>Carga masiva desde .xlsx (docs/31) -- cada fila se valida y crea igual que <see cref="Create"/>; una fila inválida no detiene el resto, queda reportada con su número y motivo.</summary>
    [HttpPost("importar"), Authorize(Policy = "AdminOrAbove")]
    public async Task<ActionResult<ImportarResultadoDto>> Importar(IFormFile? archivo, CancellationToken cancellationToken)
    {
        if (archivo is null || archivo.Length == 0) throw new BusinessRuleException("Debes adjuntar un archivo .xlsx.");
        await using var contenido = archivo.OpenReadStream();
        return Ok(await importExporter.ImportarAsync(contenido, GetUserId(), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClienteResponseDto>> GetById(Guid id, CancellationToken cancellationToken) => Ok(await consultar.GetByIdAsync(id, cancellationToken));
    [HttpPost]
    public async Task<ActionResult<ClienteResponseDto>> Create(CreateClienteDto dto, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId();
        if (userId == Guid.Empty) return Unauthorized();
        var result = await crear.ExecuteAsync(dto, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClienteResponseDto>> Update(Guid id, UpdateClienteDto dto, CancellationToken cancellationToken)
    {
        dto.Id = id;
        var validation = await updateValidator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await actualizar.ExecuteAsync(dto, userId, cancellationToken));
    }
    // Solo el super_admin elimina directo, sin pasar por SolicitudesEliminacionController (Alicia
    // 2026-09-18, corrige la decisión anterior del 2026-09-09 que dejaba entrar aquí también a
    // director/admin). Cualquier otro rol -- admin incluido -- tiene que pedirlo y esperar que un
    // administrador o el super_admin lo apruebe (ver SolicitudesEliminacionController).
    [HttpDelete("{id:guid}"), Authorize(Policy = "SuperAdminOnly")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await eliminar.ExecuteAsync(id, GetUserId(), GetUserRole(), cancellationToken);
        return NoContent();
    }
}
