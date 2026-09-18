using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Core.Exceptions;

namespace Nexit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/proyectos/{proyectoId:guid}/adjuntos")]
public class ProyectoAdjuntosController(IProyectoAdjuntosUseCase adjuntos) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProyectoAdjuntoDto>>> GetAll(Guid proyectoId, CancellationToken ct) => Ok(await adjuntos.ListAsync(proyectoId, ct));

    [HttpPost]
    public async Task<ActionResult<ProyectoAdjuntoDto>> Create(Guid proyectoId, CrearProyectoAdjuntoDto dto, CancellationToken ct) => Ok(await adjuntos.CrearAsync(proyectoId, dto, ct));

    /// <summary>Sube un archivo real (docs/28) -- solo PDF/Excel, máximo 20 MB (mismo límite del bucket de Supabase Storage).</summary>
    [HttpPost("subir")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ProyectoAdjuntoDto>> Subir(Guid proyectoId, IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0) throw new BusinessRuleException("Debes adjuntar un archivo.");
        await using var contenido = archivo.OpenReadStream();
        var resultado = await adjuntos.SubirAsync(proyectoId, archivo.FileName, archivo.ContentType, archivo.Length, contenido, ct);
        return Ok(resultado);
    }

    /// <summary>Devuelve la URL para descargar este adjunto (el link tal cual, o una URL firmada temporal si es un archivo real en Storage) -- el frontend abre esa URL directamente, este endpoint no transmite el archivo.</summary>
    [HttpGet("{id:guid}/descargar")]
    public async Task<ActionResult> Descargar(Guid proyectoId, Guid id, CancellationToken ct) => Ok(new { url = await adjuntos.ObtenerUrlDescargaAsync(proyectoId, id, ct) });

    [HttpDelete("{id:guid}"), Authorize(Policy = "AdminOrAbove")]
    public async Task<IActionResult> Delete(Guid proyectoId, Guid id, CancellationToken ct) { await adjuntos.EliminarAsync(proyectoId, id, ct); return NoContent(); }
}
