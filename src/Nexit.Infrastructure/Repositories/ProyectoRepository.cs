using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class ProyectoRepository(NexitDbContext context) : Repository<Proyecto>(context), IProyectoRepository
{
    public override Task<Proyecto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        DbSet.Include(x => x.Equipo).Include(x => x.Proveedores).Include(x => x.Seguimiento)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public override async Task<IReadOnlyList<Proyecto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Include(x => x.Equipo).Include(x => x.Proveedores)
            .AsSplitQuery()
            .OrderByDescending(x => x.FechaEvento).ThenBy(x => x.Nombre).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Proyecto>> GetAllConSeguimientoAsync(CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Include(x => x.Seguimiento)
            .OrderByDescending(x => x.FechaEvento).ThenBy(x => x.Nombre).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<Proyecto> Items, int Total)> BuscarPaginaAsync(FiltroProyectos f, CancellationToken cancellationToken = default)
    {
        var q = DbSet.AsNoTracking().AsQueryable();
        if (f.EstadoId.HasValue) q = q.Where(x => x.EstadoId == f.EstadoId);
        if (f.ClienteId.HasValue) q = q.Where(x => x.ClienteId == f.ClienteId);
        if (f.GerenteId.HasValue) q = q.Where(x => x.GerenteId == f.GerenteId);
        if (!string.IsNullOrWhiteSpace(f.Tipo)) q = q.Where(x => x.TipoProyecto == f.Tipo);
        var ahora = DateTime.UtcNow;
        switch (f.Alerta)
        {
            case "sinPm": q = q.Where(x => x.GerenteId == null); break;
            case "sinProveedor": q = q.Where(x => !x.Proveedores.Any()); break;
            case "proximos7": { var l = ahora.AddDays(7); q = q.Where(x => x.FechaEvento != null && x.FechaEvento >= ahora && x.FechaEvento <= l); break; }
            case "proximos30": { var l = ahora.AddDays(30); q = q.Where(x => x.FechaEvento != null && x.FechaEvento >= ahora && x.FechaEvento <= l); break; }
        }
        var texto = f.Texto?.Trim().ToLower();
        if (!string.IsNullOrEmpty(texto))
        {
            var patron = "%" + texto.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            var anio = texto.Length == 4 && int.TryParse(texto, out var a) ? a : (int?)null;
            q = q.Where(x =>
                EF.Functions.Like(x.Nombre.ToLower(), patron)
                || (x.ClienteId != null && Context.Clientes.Any(c => c.Id == x.ClienteId && EF.Functions.Like(c.Nombre.ToLower(), patron)))
                || x.Equipo.Any(m => EF.Functions.Like(m.Nombre.ToLower(), patron))
                || (anio != null && ((x.FechaEvento != null && x.FechaEvento.Value.Year == anio) || (x.FechaSolicitud != null && x.FechaSolicitud.Value.Year == anio))));
        }
        var total = await q.CountAsync(cancellationToken);
        var tam = Math.Clamp(f.TamanoPagina, 1, 200);
        var pagina = Math.Max(1, f.Pagina);
        var items = await q.Include(x => x.Equipo).Include(x => x.Proveedores).AsSplitQuery()
            .OrderByDescending(x => x.FechaEvento).ThenBy(x => x.Nombre).ThenBy(x => x.Id)
            .Skip((pagina - 1) * tam).Take(tam).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<ResumenProyectos> ResumenAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        var limite = ahoraUtc.AddDays(30);
        var total = await DbSet.CountAsync(cancellationToken);
        var enCurso = await DbSet.CountAsync(x => Context.EstadosProyecto.Any(e => e.Id == x.EstadoId && e.Nombre == "En curso"), cancellationToken);
        var proximos = await DbSet.CountAsync(x => x.FechaEvento != null && x.FechaEvento >= ahoraUtc && x.FechaEvento <= limite, cancellationToken);
        var sinProveedor = await DbSet.CountAsync(x => !x.Proveedores.Any(), cancellationToken);
        var sinGerente = await DbSet.CountAsync(x => x.GerenteId == null, cancellationToken);
        var limite7 = ahoraUtc.AddDays(7);
        var proximos7 = await DbSet.CountAsync(x => x.FechaEvento != null && x.FechaEvento >= ahoraUtc && x.FechaEvento <= limite7, cancellationToken);
        return new ResumenProyectos(total, enCurso, proximos, sinProveedor, sinGerente, proximos7);
    }

    // Mismo patrón que ClienteRepository.FindIdPorNombreAsync/ProveedorRepository.FindIdPorNombreAsync
    // (docs/35), pero con la pareja (ClienteId, Nombre) como llave -- ver el comentario en la interfaz.
    // La comparación `x.ClienteId == clienteId` con ambos nullable es segura tal cual (EF Core la
    // traduce a SQL que trata NULL = NULL como verdadero para este propósito, no hace falta un caso
    // especial para "sin cliente").
    public async Task<Guid?> FindIdPorClienteYNombreAsync(Guid? clienteId, string nombre, CancellationToken cancellationToken = default) =>
        (await DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.Nombre.ToLower() == nombre.Trim().ToLower(), cancellationToken))?.Id;

}
