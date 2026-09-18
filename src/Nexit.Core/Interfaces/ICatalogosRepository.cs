using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

public interface ICatalogosRepository
{
    Task<IReadOnlyList<Pais>> GetPaisesAsync(CancellationToken cancellationToken = default);
    Task<Pais?> GetPaisAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Region>> GetRegionesAsync(Guid paisId, CancellationToken cancellationToken = default);
    Task<Region?> GetRegionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Ciudad>> GetCiudadesAsync(Guid regionId, CancellationToken cancellationToken = default);
    Task<Ciudad?> GetCiudadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoriaProveedor>> GetCategoriasAsync(CancellationToken cancellationToken = default);
    Task<CategoriaProveedor?> GetCategoriaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Servicio>> GetServiciosAsync(CancellationToken cancellationToken = default);
    Task<Servicio?> GetServicioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EstadoProveedor>> GetEstadosProveedorAsync(CancellationToken cancellationToken = default);
    Task<EstadoProveedor?> GetEstadoProveedorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FaseProyecto>> GetFasesAsync(CancellationToken cancellationToken = default);
    Task<FaseProyecto?> GetFaseAsync(short fase, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EstadoProyecto>> GetEstadosAsync(short? fase, CancellationToken cancellationToken = default);
    Task<EstadoProyecto?> GetEstadoAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtapaCliente>> GetEtapasClienteAsync(CancellationToken cancellationToken = default);
    Task<EtapaCliente?> GetEtapaClienteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NombreExisteAsync<T>(string nombre, Guid? excludeId = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Busca un país por nombre (sin distinguir mayúsculas/acentos exactos) -- para la importación masiva desde Excel (docs/31), donde el archivo trae el nombre, no el Id.</summary>
    Task<Guid?> FindPaisIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default);
    /// <summary>Busca una categoría de proveedor por nombre -- mismo uso que <see cref="FindPaisIdPorNombreAsync"/>.</summary>
    Task<Guid?> FindCategoriaIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default);
    /// <summary>Busca un estado de proyecto por nombre -- mismo uso que <see cref="FindPaisIdPorNombreAsync"/>.</summary>
    Task<Guid?> FindEstadoIdPorNombreAsync(string nombre, CancellationToken cancellationToken = default);
    /// <summary>
    /// Busca una ciudad por nombre, calificada por el país al que debe pertenecer (una ciudad no es
    /// única globalmente por nombre -- hay más de una "La Paz" en la región). Devuelve también la
    /// región de la ciudad encontrada, ya que <c>CreateProveedorDto</c> guarda los tres niveles.
    /// </summary>
    Task<(Guid CiudadId, Guid RegionId, Guid PaisId)?> FindCiudadPorNombreAsync(string nombrePais, string nombreCiudad, CancellationToken cancellationToken = default);
    Task AddAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;
    void Update<T>(T entity) where T : class;
    Task DeleteAsync<T>(T entity, CancellationToken cancellationToken = default) where T : class;
}
