using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Clientes;
using Nexit.Application.UseCases.Clientes;
using Nexit.Core.Exceptions;

namespace Nexit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/clientes/{clienteId:guid}/adjuntos")]
public class ClienteAdjuntosController(IClienteAdjuntosUseCase adjuntos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClienteAdjuntoDto>>> GetAll(Guid clienteId, CancellationToken ct) => Ok(await adjuntos.ListAsync(clienteId, ct));

    [HttpPost]
    public async Task<ActionResult<ClienteAdjuntoDto>> Create(Guid clienteId, CrearClienteAdjuntoDto dto, CancellationToken ct) => Ok(await adjuntos.CrearAsync(clienteId, dto, ct));

    /// <summary>Sube un archivo real (docs/28) -- solo PDF/Excel, máximo 20 MB (mismo límite del bucket de Supabase Storage).</summary>
    [HttpPost("subir")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ClienteAdjuntoDto>> Subir(Guid clienteId, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0) throw new BusinessRuleException("Debes adjuntar un archivo.");
        await using var contenido = archivo.OpenReadStream();
        var resultado = await adjuntos.SubirAsync(clienteId, archivo.FileName, archivo.ContentType, archivo.Length, contenido, ct);
        return Ok(resultado);
    }

    /// <summary>Devuelve la URL para descargar este adjunto (el link tal cual, o una URL firmada temporal si es un archivo real en Storage) -- el frontend abre esa URL directamente, este endpoint no transmite el archivo.</summary>
    [HttpGet("{id:guid}/descargar")]
    public async Task<ActionResult> Descargar(Guid clienteId, Guid id, CancellationToken ct) => Ok(new { url = await adjuntos.ObtenerUrlDescargaAsync(clienteId, id, ct) });

    [HttpDelete("{id:guid}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> Delete(Guid clienteId, Guid id, CancellationToken ct) { await adjuntos.EliminarAsync(clienteId, id, ct); return NoContent(); }
}
