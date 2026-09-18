using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class CatalogosRepository(NexitDbContext context) : ICatalogosRepository
{
    public async Task<IReadOnlyList<Pais>> GetPaisesAsync(CancellationToken cancellationToken = default) => await context.Paises.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<Pais?> GetPaisAsync(Guid id, CancellationToken cancellationToken = default) => context.Paises.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<Region>> GetRegionesAsync(Guid paisId, CancellationToken cancellationToken = default) => await context.Regiones.AsNoTracking().Where(x => x.PaisId == paisId).OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<Region?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default) => context.Regiones.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<Ciudad>> GetCiudadesAsync(Guid regionId, CancellationToken cancellationToken = default) => await context.Ciudades.AsNoTracking().Where(x => x.RegionId == regionId).OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<Ciudad?> GetCiudadAsync(Guid id, CancellationToken cancellationToken = default) => context.Ciudades.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<CategoriaProveedor>> GetCategoriasAsync(CancellationToken cancellationToken = default) => await context.CategoriasProveedor.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<CategoriaProveedor?> GetCategoriaAsync(Guid id, CancellationToken cancellationToken = default) => context.CategoriasProveedor.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<Servicio>> GetServiciosAsync(CancellationToken cancellationToken = default) => await context.Servicios.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<Servicio?> GetServicioAsync(Guid id, CancellationToken cancellationToken = default) => context.Servicios.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<EstadoProveedor>> GetEstadosProveedorAsync(CancellationToken cancellationToken = default) => await context.EstadosProveedor.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(cancellationToken);
    public Task<EstadoProveedor?> GetEstadoProveedorAsync(Guid id, CancellationToken cancellationToken = default) => context.EstadosProveedor.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<FaseProyecto>> GetFasesAsync(CancellationToken cancellationToken = default) => await context.FasesProyecto.AsNoTracking().OrderBy(x => x.Fase).ToListAsync(cancellationToken);
    public Task<FaseProyecto?> GetFaseAsync(short fase, CancellationToken cancellationToken = default) => context.FasesProyecto.FindAsync([fase], cancellationToken).AsTask();
    public async Task<IReadOnlyList<EstadoProyecto>> GetEstadosAsync(short? fase, CancellationToken cancellationToken = default)
    {
        var query = context.EstadosProyecto.AsNoTracking();
        if (fase.HasValue) query = query.Where(x => x.Fase == fase.Value);
        return await query.OrderBy(x => x.Orden).ToListAsync(cancellationToken);
    }
    public Task<EstadoProyecto?> GetEstadoAsync(Guid id, CancellationToken cancellationToken = default) => context.EstadosProyecto.FindAsync([id], cancellationToken).AsTask();
    public async Task<IReadOnlyList<EtapaCliente>> GetEtapasClienteAsync(CancellationToken cancellationToken = default) => await context.EtapasCliente.AsNoTracking().OrderBy(x => x.Orden).ToListAsync(cancellationToken);
    public Task<EtapaCliente?> GetEtapaClienteAsync(Guid id, CancellationToken cancellationToken = default) => context.EtapasCliente.FindAsync([id], cancellationToken).AsTask();
    public Task<bool> NombreExisteAsync<T>(string nombre, Guid? excludeId = null, CancellationToken cancellationToken = default) where T : class =>
        context.Set<T>().AnyAsync(x => EF.Property<string>(x, "Nombre").ToLower() == nombre.ToLower() && (!excludeId.HasValue || EF.Property<Guid>(x, "Id") != excludeId.Value), cancellationToken);

    public async Task<Guid?> FindPaisIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default) =>
        (await context.Paises.AsNoTracking().FirstOrDefaultAsync(x => x.Nombre.ToLower() == nombre.Trim().ToLower(), cancellationToken))?.Id;

    public async Task<Guid?> FindCategoriaIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default) =>
        (await context.CategoriasProveedor.AsNoTracking().FirstOrDefaultAsync(x => x.Nombre.ToLower() == nombre.Trim().ToLower(), cancellationToken))?.Id;

    public async Task<Guid?> FindEstadoIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default) =>
        (await context.EstadosProyecto.AsNoTracking().FirstOrDefaultAsync(x => x.Nombre.ToLower() == nombre.Trim().ToLower(), cancellationToken))?.Id;

    public async Task<(Guid CiudadId, Guid RegionId, Guid PaisId)?> FindCiudadPorNombreAsync(string nombrePais, string nombreCiudad, CancellationToken cancellationToken = default)
    {
        var ciudad = await context.Ciudades.AsNoTracking()
            .Where(c => c.Nombre.ToLower() == nombreCiudad.Trim().ToLower() && c.Region.Pais.Nombre.ToLower() == nombrePais.Trim().ToLower())
            .Select(c => new { c.Id, c.RegionId, c.Region.PaisId })
            .FirstOrDefaultAsync(cancellationToken);
        return ciudad is null ? null : (ciudad.Id, ciudad.RegionId, ciudad.PaisId);
    }
    public Task AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class => context.Set<T>().AddAsync(entity, cancellationToken).AsTask();
    public void Update<T>(T entity) where T : class => context.Set<T>().Update(entity);
    public Task DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class { context.Set<T>().Remove(entity); return Task.CompletedTask; }
}
