using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

// El calendario de proyectos se eliminó del sistema el 2026-09-09 (ver docs/41): con él se fueron
// los records ConteoMesProyectos/ProyectoCalendarioItem y los tres métodos de agregación por
// año/mes que solo esa vista usaba.

/// <summary>Filtros del listado paginado de proyectos (todos opcionales). <c>Alerta</c>: sinPm | sinProveedor | proximos7 | proximos30.</summary>
public record FiltroProyectos(string? Texto, Guid? EstadoId, Guid? ClienteId, string? Tipo, Guid? GerenteId, int Pagina, int TamanoPagina, string? Alerta = null);

/// <summary>Conteos globales (sin filtros) que muestran las tarjetas de arriba de la pantalla de Proyectos.</summary>
public record ResumenProyectos(int Total, int EnCurso, int Proximos30Dias, int SinProveedor, int SinGerente = 0, int Proximos7Dias = 0);

public interface IProyectoRepository : IRepository<Proyecto>
{
    /// <summary>Una página de proyectos ya filtrada y ordenada en la base (no trae todo para filtrar en memoria). Devuelve también el total que cumple los filtros.</summary>
    Task<(IReadOnlyList<Proyecto> Items, int Total)> BuscarPaginaAsync(FiltroProyectos filtro, CancellationToken cancellationToken = default);

    Task<ResumenProyectos> ResumenAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un proyecto por su cliente (puede ser null -- "sin cliente" es válido) y su nombre exacto
    /// (sin distinguir mayúsculas) -- para la importación masiva (docs/31, docs/35). El nombre solo NO
    /// alcanza como llave: el mismo nombre de proyecto puede repetirse legítimamente para clientes
    /// distintos (o para varios proyectos "sin cliente"), así que la pareja (Cliente, Nombre) es la
    /// llave que evita fusionar por error dos proyectos que no tienen nada que ver.
    /// </summary>
    /// <summary>Todos los proyectos CON su bitácora de seguimiento. Solo lo necesita el cálculo de prioridad; el listado normal va sin ella (mucho más liviano).</summary>
    Task<IReadOnlyList<Proyecto>> GetAllConSeguimientoAsync(CancellationToken cancellationToken = default);

    Task<Guid?> FindIdPorClienteYNombreAsync(Guid? clienteId, string nombre, CancellationToken cancellationToken = default);
}
