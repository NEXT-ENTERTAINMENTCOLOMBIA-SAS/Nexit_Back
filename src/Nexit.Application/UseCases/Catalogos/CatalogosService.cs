using Nexit.Application.DTOs.Catalogos;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Catalogos;

public class CatalogosService(ICatalogosRepository repository, IUnitOfWork unitOfWork) : ICatalogosService
{
    public async Task<IReadOnlyList<PaisDto>> GetPaisesAsync(CancellationToken ct = default) => (await repository.GetPaisesAsync(ct)).Select(x => new PaisDto(x.Id, x.Nombre, x.EtiquetaRegion)).ToList();
    public async Task<PaisDto> CrearPaisAsync(CrearPaisDto input, CancellationToken ct = default)
    {
        ValidarNombre(input.Nombre); await AsegurarNombreDisponible<Pais>(input.Nombre, null, ct);
        var pais = new Pais { Nombre = input.Nombre.Trim(), EtiquetaRegion = string.IsNullOrWhiteSpace(input.EtiquetaRegion) ? "Departamento" : input.EtiquetaRegion.Trim() };
        await repository.AddAsync(pais, ct); await unitOfWork.SaveChangesAsync(ct); return new PaisDto(pais.Id, pais.Nombre, pais.EtiquetaRegion);
    }
    public async Task<PaisDto> ActualizarPaisAsync(Guid id, CrearPaisDto input, CancellationToken ct = default)
    {
        var pais = await repository.GetPaisAsync(id, ct) ?? throw NoEncontrada<Pais>(id); ValidarNombre(input.Nombre); await AsegurarNombreDisponible<Pais>(input.Nombre, id, ct);
        pais.Nombre = input.Nombre.Trim(); pais.EtiquetaRegion = string.IsNullOrWhiteSpace(input.EtiquetaRegion) ? "Departamento" : input.EtiquetaRegion.Trim(); repository.Update(pais); await unitOfWork.SaveChangesAsync(ct); return new PaisDto(pais.Id, pais.Nombre, pais.EtiquetaRegion);
    }
    public async Task<IReadOnlyList<RegionDto>> GetRegionesAsync(Guid paisId, CancellationToken ct = default) => (await repository.GetRegionesAsync(paisId, ct)).Select(x => new RegionDto(x.Id, x.PaisId, x.Nombre)).ToList();
    public async Task<RegionDto> CrearRegionAsync(CrearRegionDto input, CancellationToken ct = default)
    {
        _ = await repository.GetPaisAsync(input.PaisId, ct) ?? throw NoEncontrada<Pais>(input.PaisId); ValidarNombre(input.Nombre);
        var region = new Region { PaisId = input.PaisId, Nombre = input.Nombre.Trim() }; await repository.AddAsync(region, ct); await unitOfWork.SaveChangesAsync(ct); return new RegionDto(region.Id, region.PaisId, region.Nombre);
    }
    public async Task<RegionDto> ActualizarRegionAsync(Guid id, CrearRegionDto input, CancellationToken ct = default)
    {
        var region = await repository.GetRegionAsync(id, ct) ?? throw NoEncontrada<Region>(id); _ = await repository.GetPaisAsync(input.PaisId, ct) ?? throw NoEncontrada<Pais>(input.PaisId); ValidarNombre(input.Nombre);
        region.PaisId = input.PaisId; region.Nombre = input.Nombre.Trim(); repository.Update(region); await unitOfWork.SaveChangesAsync(ct); return new RegionDto(region.Id, region.PaisId, region.Nombre);
    }
    public async Task<IReadOnlyList<CiudadDto>> GetCiudadesAsync(Guid regionId, CancellationToken ct = default) => (await repository.GetCiudadesAsync(regionId, ct)).Select(x => new CiudadDto(x.Id, x.RegionId, x.Nombre)).ToList();
    public async Task<CiudadDto> CrearCiudadAsync(CrearCiudadDto input, CancellationToken ct = default)
    {
        _ = await repository.GetRegionAsync(input.RegionId, ct) ?? throw NoEncontrada<Region>(input.RegionId); ValidarNombre(input.Nombre);
        var ciudad = new Ciudad { RegionId = input.RegionId, Nombre = input.Nombre.Trim() }; await repository.AddAsync(ciudad, ct); await unitOfWork.SaveChangesAsync(ct); return new CiudadDto(ciudad.Id, ciudad.RegionId, ciudad.Nombre);
    }
    public async Task<CiudadDto> ActualizarCiudadAsync(Guid id, CrearCiudadDto input, CancellationToken ct = default)
    {
        var ciudad = await repository.GetCiudadAsync(id, ct) ?? throw NoEncontrada<Ciudad>(id); _ = await repository.GetRegionAsync(input.RegionId, ct) ?? throw NoEncontrada<Region>(input.RegionId); ValidarNombre(input.Nombre);
        ciudad.RegionId = input.RegionId; ciudad.Nombre = input.Nombre.Trim(); repository.Update(ciudad); await unitOfWork.SaveChangesAsync(ct); return new CiudadDto(ciudad.Id, ciudad.RegionId, ciudad.Nombre);
    }
    public async Task<IReadOnlyList<ItemCatalogoDto>> GetCategoriasAsync(CancellationToken ct = default) => (await repository.GetCategoriasAsync(ct)).Select(x => new ItemCatalogoDto(x.Id, x.Nombre)).ToList();
    public Task<ItemCatalogoDto> CrearCategoriaAsync(NombreDto input, CancellationToken ct = default) => CrearItem(input, ct, nombre => new CategoriaProveedor { Nombre = nombre }, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public Task<ItemCatalogoDto> ActualizarCategoriaAsync(Guid id, NombreDto input, CancellationToken ct = default) => ActualizarItem(id, input, ct, repository.GetCategoriaAsync, x => x.Nombre, (x, nombre) => x.Nombre = nombre, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public async Task<IReadOnlyList<ItemCatalogoDto>> GetServiciosAsync(CancellationToken ct = default) => (await repository.GetServiciosAsync(ct)).Select(x => new ItemCatalogoDto(x.Id, x.Nombre)).ToList();
    public Task<ItemCatalogoDto> CrearServicioAsync(NombreDto input, CancellationToken ct = default) => CrearItem(input, ct, nombre => new Servicio { Nombre = nombre }, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public Task<ItemCatalogoDto> ActualizarServicioAsync(Guid id, NombreDto input, CancellationToken ct = default) => ActualizarItem(id, input, ct, repository.GetServicioAsync, x => x.Nombre, (x, nombre) => x.Nombre = nombre, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public async Task<IReadOnlyList<ItemCatalogoDto>> GetEstadosProveedorAsync(CancellationToken ct = default) => (await repository.GetEstadosProveedorAsync(ct)).Select(x => new ItemCatalogoDto(x.Id, x.Nombre)).ToList();
    public Task<ItemCatalogoDto> CrearEstadoProveedorAsync(NombreDto input, CancellationToken ct = default) => CrearItem(input, ct, nombre => new EstadoProveedor { Nombre = nombre }, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public Task<ItemCatalogoDto> ActualizarEstadoProveedorAsync(Guid id, NombreDto input, CancellationToken ct = default) => ActualizarItem(id, input, ct, repository.GetEstadoProveedorAsync, x => x.Nombre, (x, nombre) => x.Nombre = nombre, x => new ItemCatalogoDto(x.Id, x.Nombre));
    public async Task<IReadOnlyList<FaseProyectoDto>> GetFasesAsync(CancellationToken ct = default) => (await repository.GetFasesAsync(ct)).Select(x => new FaseProyectoDto(x.Fase, x.Nombre)).ToList();
    public async Task<FaseProyectoDto> ActualizarFaseAsync(short fase, NombreDto input, CancellationToken ct = default)
    {
        var entity = await repository.GetFaseAsync(fase, ct) ?? throw new EntityNotFoundException("FaseProyecto", Guid.Empty); ValidarNombre(input.Nombre); entity.Nombre = input.Nombre.Trim(); repository.Update(entity); await unitOfWork.SaveChangesAsync(ct); return new FaseProyectoDto(entity.Fase, entity.Nombre);
    }
    public async Task<IReadOnlyList<EstadoProyectoDto>> GetEstadosAsync(short? fase, CancellationToken ct = default) => (await repository.GetEstadosAsync(fase, ct)).Select(x => new EstadoProyectoDto(x.Id, x.Nombre, x.Fase, x.Orden)).ToList();
    public async Task<EstadoProyectoDto> CrearEstadoAsync(CrearEstadoProyectoDto input, CancellationToken ct = default)
    {
        _ = await repository.GetFaseAsync(input.Fase, ct) ?? throw new BusinessRuleException("La fase indicada no existe."); ValidarNombre(input.Nombre); await AsegurarNombreDisponible<EstadoProyecto>(input.Nombre, null, ct);
        var entity = new EstadoProyecto { Nombre = input.Nombre.Trim(), Fase = input.Fase, Orden = input.Orden }; await repository.AddAsync(entity, ct); await unitOfWork.SaveChangesAsync(ct); return new EstadoProyectoDto(entity.Id, entity.Nombre, entity.Fase, entity.Orden);
    }
    public async Task<EstadoProyectoDto> ActualizarEstadoAsync(Guid id, CrearEstadoProyectoDto input, CancellationToken ct = default)
    {
        var entity = await repository.GetEstadoAsync(id, ct) ?? throw NoEncontrada<EstadoProyecto>(id); _ = await repository.GetFaseAsync(input.Fase, ct) ?? throw new BusinessRuleException("La fase indicada no existe."); ValidarNombre(input.Nombre); await AsegurarNombreDisponible<EstadoProyecto>(input.Nombre, id, ct);
        entity.Nombre = input.Nombre.Trim(); entity.Fase = input.Fase; entity.Orden = input.Orden; repository.Update(entity); await unitOfWork.SaveChangesAsync(ct); return new EstadoProyectoDto(entity.Id, entity.Nombre, entity.Fase, entity.Orden);
    }
    public async Task<IReadOnlyList<EtapaClienteDto>> GetEtapasClienteAsync(CancellationToken ct = default) => (await repository.GetEtapasClienteAsync(ct)).Select(x => new EtapaClienteDto(x.Id, x.Nombre, x.Orden, x.PorcentajeProceso)).ToList();
    public async Task<EtapaClienteDto> CrearEtapaClienteAsync(CrearEtapaClienteDto input, CancellationToken ct = default)
    {
        ValidarNombre(input.Nombre); await AsegurarNombreDisponible<EtapaCliente>(input.Nombre, null, ct); ValidarPorcentaje(input.PorcentajeProceso);
        var entity = new EtapaCliente { Nombre = input.Nombre.Trim(), Orden = input.Orden, PorcentajeProceso = input.PorcentajeProceso };
        await repository.AddAsync(entity, ct); await unitOfWork.SaveChangesAsync(ct); return new EtapaClienteDto(entity.Id, entity.Nombre, entity.Orden, entity.PorcentajeProceso);
    }
    public async Task<EtapaClienteDto> ActualizarEtapaClienteAsync(Guid id, CrearEtapaClienteDto input, CancellationToken ct = default)
    {
        var entity = await repository.GetEtapaClienteAsync(id, ct) ?? throw NoEncontrada<EtapaCliente>(id); ValidarNombre(input.Nombre); await AsegurarNombreDisponible<EtapaCliente>(input.Nombre, id, ct); ValidarPorcentaje(input.PorcentajeProceso);
        entity.Nombre = input.Nombre.Trim(); entity.Orden = input.Orden; entity.PorcentajeProceso = input.PorcentajeProceso; repository.Update(entity); await unitOfWork.SaveChangesAsync(ct); return new EtapaClienteDto(entity.Id, entity.Nombre, entity.Orden, entity.PorcentajeProceso);
    }
    public async Task EliminarAsync(string tipo, Guid id, CancellationToken ct = default)
    {
        switch (tipo.ToLowerInvariant())
        {
            case "paises": await Eliminar(await repository.GetPaisAsync(id, ct), id, ct); break;
            case "regiones": await Eliminar(await repository.GetRegionAsync(id, ct), id, ct); break;
            case "ciudades": await Eliminar(await repository.GetCiudadAsync(id, ct), id, ct); break;
            // "categorias-proveedor"/"estados-proyecto" -- deben calzar con el segmento de ruta
            // que ya usan las demás acciones de este mismo catálogo en CatalogosController
            // (GET/POST/PUT), no con un nombre corto distinto (antes decía "categorias"/"estados",
            // que el frontend nunca podía enviar porque CatalogoTipo -- types/api.ts -- ya usa los
            // nombres largos en todos lados; DELETE quedaba inalcanzable para esos dos tipos).
            case "categorias-proveedor": await Eliminar(await repository.GetCategoriaAsync(id, ct), id, ct); break;
            case "servicios": await Eliminar(await repository.GetServicioAsync(id, ct), id, ct); break;
            case "estados-proveedor": await Eliminar(await repository.GetEstadoProveedorAsync(id, ct), id, ct); break;
            case "estados-proyecto": await Eliminar(await repository.GetEstadoAsync(id, ct), id, ct); break;
            case "etapas-cliente": await Eliminar(await repository.GetEtapaClienteAsync(id, ct), id, ct); break;
            default: throw new BusinessRuleException("Tipo de catálogo no válido.");
        }
    }
    private async Task<ItemCatalogoDto> CrearItem<T>(NombreDto input, CancellationToken ct, Func<string, T> factory, Func<T, ItemCatalogoDto> map) where T : class
    { ValidarNombre(input.Nombre); await AsegurarNombreDisponible<T>(input.Nombre, null, ct); var entity = factory(input.Nombre.Trim()); await repository.AddAsync(entity, ct); await unitOfWork.SaveChangesAsync(ct); return map(entity); }
    private async Task<ItemCatalogoDto> ActualizarItem<T>(Guid id, NombreDto input, CancellationToken ct, Func<Guid, CancellationToken, Task<T?>> get, Func<T, string> name, Action<T, string> setName, Func<T, ItemCatalogoDto> map) where T : class
    { var entity = await get(id, ct) ?? throw NoEncontrada<T>(id); ValidarNombre(input.Nombre); await AsegurarNombreDisponible<T>(input.Nombre, id, ct); setName(entity, input.Nombre.Trim()); repository.Update(entity); await unitOfWork.SaveChangesAsync(ct); return map(entity); }
    private async Task Eliminar<T>(T? entity, Guid id, CancellationToken ct) where T : class { if (entity is null) throw NoEncontrada<T>(id); await repository.DeleteAsync(entity, ct); await unitOfWork.SaveChangesAsync(ct); }
    private async Task AsegurarNombreDisponible<T>(string nombre, Guid? id, CancellationToken ct) where T : class { if (await repository.NombreExisteAsync<T>(nombre.Trim(), id, ct)) throw new BusinessRuleException("Ya existe un registro con ese nombre."); }
    private static void ValidarNombre(string nombre) { if (string.IsNullOrWhiteSpace(nombre) || nombre.Trim().Length > 255) throw new BusinessRuleException("El nombre es requerido y no puede exceder 255 caracteres."); }
    private static void ValidarPorcentaje(short porcentaje) { if (porcentaje is < 0 or > 100) throw new BusinessRuleException("El porcentaje del proceso debe estar entre 0 y 100."); }
    private static EntityNotFoundException NoEncontrada<T>(Guid id) => new(typeof(T).Name, id);
}
