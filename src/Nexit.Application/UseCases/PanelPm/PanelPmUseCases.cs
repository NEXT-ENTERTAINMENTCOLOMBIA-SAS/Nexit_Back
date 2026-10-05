using Nexit.Application.DTOs.PanelPm;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.PanelPm;

public interface IConsultarPanelProjectManagersUseCase { Task<PanelPmDto> ExecuteAsync(CancellationToken cancellationToken = default); }

/// <summary>
/// Reemplaza al módulo de Informes (2026-10-05): ya no se guardan capturas semanales ni se exporta a
/// Excel -- interesa saber, por cada Project Manager (el gerente del proyecto), cuántos proyectos
/// tiene, con cuántas personas, con qué clientes y con qué proveedores trabaja. Todo se calcula en
/// vivo; no se persiste nada.
/// </summary>
public class ConsultarPanelProjectManagersUseCase(IPanelProjectManagersRepository repository) : IConsultarPanelProjectManagersUseCase
{
    public async Task<PanelPmDto> ExecuteAsync(CancellationToken ct = default)
    {
        var proyectos = await repository.GetProyectosAsync(ct);
        var gerenteIds = proyectos.Where(p => p.GerenteId.HasValue).Select(p => p.GerenteId!.Value).Distinct().ToList();
        var usuarios = await repository.GetPosiblesProjectManagersAsync(gerenteIds, ct);

        var tarjetas = new List<PanelPmProjectManagerDto>();
        foreach (var usuario in usuarios)
        {
            var suyos = proyectos.Where(p => p.GerenteId == usuario.Id).ToList();
            var tarjeta = Armar(suyos);
            tarjeta.Id = usuario.Id;
            tarjeta.Nombre = $"{usuario.Nombre} {usuario.Apellido}".Trim();
            tarjeta.Email = usuario.Email;
            tarjeta.Iniciales = usuario.Iniciales;
            tarjeta.Activo = usuario.Activo;
            tarjetas.Add(tarjeta);
        }

        // Más carga primero; a igualdad de carga, orden alfabético.
        tarjetas = tarjetas.OrderByDescending(t => t.TotalProyectos).ThenBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        var huerfanos = proyectos.Where(p => !p.GerenteId.HasValue || usuarios.All(u => u.Id != p.GerenteId)).ToList();
        PanelPmProjectManagerDto? sin = null;
        if (huerfanos.Count > 0) { sin = Armar(huerfanos); sin.Nombre = "Sin Project Manager"; }

        var personasUnicas = proyectos.SelectMany(p => p.Equipo).Select(e => Normalizar(e.Nombre)).Where(n => n.Length > 0).Distinct().Count();
        return new PanelPmDto
        {
            ProjectManagers = tarjetas,
            SinProjectManager = sin,
            Resumen = new PanelPmResumenDto
            {
                ProjectManagersConProyectos = tarjetas.Count(t => t.TotalProyectos > 0),
                ProjectManagersSinProyectos = tarjetas.Count(t => t.TotalProyectos == 0 && t.Activo),
                TotalProyectos = proyectos.Count,
                ProyectosSinProjectManager = huerfanos.Count,
                PersonasEnProyectos = personasUnicas,
                ClientesActivos = proyectos.Where(p => p.ClienteId.HasValue).Select(p => p.ClienteId).Distinct().Count(),
                ProveedoresActivos = proyectos.SelectMany(p => p.Proveedores).Select(x => x.Id).Distinct().Count(),
            },
        };
    }

    private static string Normalizar(string? nombre) => (nombre ?? string.Empty).Trim().ToLowerInvariant();

    private static PanelPmProjectManagerDto Armar(IReadOnlyList<PanelProyectoFila> proyectos)
    {
        var personas = proyectos
            .SelectMany(p => p.Equipo.Select(e => (Proyecto: p.Id, e.Nombre, e.Rol)))
            .Where(x => Normalizar(x.Nombre).Length > 0)
            .GroupBy(x => Normalizar(x.Nombre))
            .Select(g => new PanelPmPersonaDto
            {
                Nombre = g.First().Nombre.Trim(),
                Cargo = string.Join(" · ", g.Select(x => x.Rol.Trim()).Where(r => r.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase)),
                Proyectos = g.Select(x => x.Proyecto).Distinct().Count(),
            })
            .OrderByDescending(x => x.Proyectos).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        var clientes = proyectos.Where(p => p.ClienteId.HasValue)
            .GroupBy(p => p.ClienteId!.Value)
            .Select(g => new PanelPmReferenciaDto { Id = g.Key, Nombre = g.First().ClienteNombre ?? "Sin nombre", Proyectos = g.Count() })
            .OrderByDescending(x => x.Proyectos).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        var proveedores = proyectos.SelectMany(p => p.Proveedores.Select(x => (Proyecto: p.Id, x.Id, x.Nombre)))
            .GroupBy(x => x.Id)
            .Select(g => new PanelPmReferenciaDto { Id = g.Key, Nombre = g.First().Nombre, Proyectos = g.Select(x => x.Proyecto).Distinct().Count() })
            .OrderByDescending(x => x.Proyectos).ThenBy(x => x.Nombre, StringComparer.OrdinalIgnoreCase).ToList();

        return new PanelPmProjectManagerDto
        {
            TotalProyectos = proyectos.Count,
            TotalPersonas = personas.Count,
            TotalClientes = clientes.Count,
            TotalProveedores = proveedores.Count,
            Personas = personas,
            Clientes = clientes,
            Proveedores = proveedores,
            Proyectos = proyectos
                .OrderBy(p => p.FechaEvento ?? DateTime.MaxValue).ThenBy(p => p.Nombre, StringComparer.OrdinalIgnoreCase)
                .Select(p => new PanelPmProyectoDto
                {
                    Id = p.Id, Nombre = string.IsNullOrWhiteSpace(p.Nombre) ? "(Sin nombre)" : p.Nombre, Cliente = p.ClienteNombre, Estado = p.Estado,
                    PorcentajeAvance = p.PorcentajeAvance, FechaEvento = p.FechaEvento, Prioridad = p.Prioridad,
                    Personas = p.Equipo.Select(e => Normalizar(e.Nombre)).Where(n => n.Length > 0).Distinct().Count(),
                    Proveedores = p.Proveedores.Select(x => x.Nombre).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList(),
                }).ToList(),
        };
    }
}
