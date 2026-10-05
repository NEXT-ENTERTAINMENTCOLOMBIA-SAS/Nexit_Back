using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Clientes;
using Nexit.Application.UseCases.Clientes;

namespace Nexit.API.Controllers;

/// <summary>Notas internas de un cliente (2026-10-05). Cualquier usuario con perfil las lee y las escribe; borrar: autor o admin.</summary>
[ApiController]
[Authorize]
[Route("api/clientes/{clienteId:guid}/notas")]
public class ClienteNotasController(IClienteNotasUseCase notas, IValidator<CrearClienteNotaDto> validator) : BaseController
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteNotaDto>>> GetAll(Guid clienteId, CancellationToken ct) => Ok(await notas.ListAsync(clienteId, ct));

    [HttpPost]
    public async Task<ActionResult<ClienteNotaDto>> Create(Guid clienteId, CrearClienteNotaDto dto, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(dto, ct);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        return Ok(await notas.AgregarAsync(clienteId, dto, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid clienteId, Guid id, CancellationToken ct)
    {
        var userId = GetUserId(); if (userId == Guid.Empty) return Unauthorized();
        await notas.EliminarAsync(clienteId, id, userId, GetUserRole(), ct);
        return NoContent();
    }
}
