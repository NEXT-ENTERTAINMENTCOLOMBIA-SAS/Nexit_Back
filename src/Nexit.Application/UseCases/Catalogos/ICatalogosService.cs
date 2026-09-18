using Nexit.Application.DTOs.Catalogos;

namespace Nexit.Application.UseCases.Catalogos;

public interface ICatalogosService
{
    Task<IReadOnlyList<PaisDto>> GetPaisesAsync(CancellationToken cancellationToken = default);
    Task<PaisDto> CrearPaisAsync(CrearPaisDto input, CancellationToken cancellationToken = default);
    Task<PaisDto> ActualizarPaisAsync(Guid id, CrearPaisDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegionDto>> GetRegionesAsync(Guid paisId, CancellationToken cancellationToken = default);
    Task<RegionDto> CrearRegionAsync(CrearRegionDto input, CancellationToken cancellationToken = default);
    Task<RegionDto> ActualizarRegionAsync(Guid id, CrearRegionDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CiudadDto>> GetCiudadesAsync(Guid regionId, CancellationToken cancellationToken = default);
    Task<CiudadDto> CrearCiudadAsync(CrearCiudadDto input, CancellationToken cancellationToken = default);
    Task<CiudadDto> ActualizarCiudadAsync(Guid id, CrearCiudadDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemCatalogoDto>> GetCategoriasAsync(CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> CrearCategoriaAsync(NombreDto input, CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> ActualizarCategoriaAsync(Guid id, NombreDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemCatalogoDto>> GetServiciosAsync(CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> CrearServicioAsync(NombreDto input, CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> ActualizarServicioAsync(Guid id, NombreDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ItemCatalogoDto>> GetEstadosProveedorAsync(CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> CrearEstadoProveedorAsync(NombreDto input, CancellationToken cancellationToken = default);
    Task<ItemCatalogoDto> ActualizarEstadoProveedorAsync(Guid id, NombreDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FaseProyectoDto>> GetFasesAsync(CancellationToken cancellationToken = default);
    Task<FaseProyectoDto> ActualizarFaseAsync(short fase, NombreDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EstadoProyectoDto>> GetEstadosAsync(short? fase, CancellationToken cancellationToken = default);
    Task<EstadoProyectoDto> CrearEstadoAsync(CrearEstadoProyectoDto input, CancellationToken cancellationToken = default);
    Task<EstadoProyectoDto> ActualizarEstadoAsync(Guid id, CrearEstadoProyectoDto input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EtapaClienteDto>> GetEtapasClienteAsync(CancellationToken cancellationToken = default);
    Task<EtapaClienteDto> CrearEtapaClienteAsync(CrearEtapaClienteDto input, CancellationToken cancellationToken = default);
    Task<EtapaClienteDto> ActualizarEtapaClienteAsync(Guid id, CrearEtapaClienteDto input, CancellationToken cancellationToken = default);
    Task EliminarAsync(string tipo, Guid id, CancellationToken cancellationToken = default);
}
