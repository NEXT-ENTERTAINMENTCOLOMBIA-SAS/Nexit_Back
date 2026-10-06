using System.Text.RegularExpressions;
using Nexit.Application.DTOs.Configuracion;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Configuracion;

public interface IConfiguracionService
{
    Task<IReadOnlyList<RolConfigDto>> GetRolesAsync(CancellationToken ct = default);
    Task<RolConfigDto> ActualizarRolAsync(string rol, ActualizarRolConfigDto dto, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, List<OpcionConfigDto>>> GetOpcionesAsync(CancellationToken ct = default);
    Task<OpcionConfigDto> CrearOpcionAsync(string lista, GuardarOpcionDto dto, CancellationToken ct = default);
    Task<OpcionConfigDto> ActualizarOpcionAsync(string lista, Guid id, GuardarOpcionDto dto, CancellationToken ct = default);
    Task EliminarOpcionAsync(string lista, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<DominioCorreoDto>> GetDominiosAsync(CancellationToken ct = default);
    Task<DominioCorreoDto> CrearDominioAsync(GuardarDominioDto dto, CancellationToken ct = default);
    Task EliminarDominioAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Configuración editable de todo el sistema (2026-10-05, a pedido de Alicia: "todo se puede
/// configurar"). Los catálogos de ubicaciones, categorías, servicios, estados y etapas ya vivían en
/// <see cref="Catalogos.CatalogosService"/>; esto agrega lo que faltaba: nombres de roles, las listas
/// de Proyectos y los dominios de correo permitidos.
/// </summary>
/// <summary>
/// Caché en memoria de roles y listas de Configuración (2026-10-05): se leen en casi cada pantalla y casi nunca
/// cambian. Dura 60 s como tope y se invalida al instante cuando alguien edita (en esta instancia).
/// </summary>
public class ConfiguracionCache
{
    private static readonly TimeSpan Vida = TimeSpan.FromSeconds(60);
    private readonly object _lock = new();
    private (DateTime Hasta, IReadOnlyList<RolConfigDto> Valor)? _roles;
    private (DateTime Hasta, IReadOnlyDictionary<string, List<OpcionConfigDto>> Valor)? _opciones;

    public IReadOnlyList<RolConfigDto>? Roles { get { lock (_lock) return _roles is { } r && r.Hasta > DateTime.UtcNow ? r.Valor : null; } }
    public IReadOnlyDictionary<string, List<OpcionConfigDto>>? Opciones { get { lock (_lock) return _opciones is { } o && o.Hasta > DateTime.UtcNow ? o.Valor : null; } }
    public void GuardarRoles(IReadOnlyList<RolConfigDto> v) { lock (_lock) _roles = (DateTime.UtcNow + Vida, v); }
    public void GuardarOpciones(IReadOnlyDictionary<string, List<OpcionConfigDto>> v) { lock (_lock) _opciones = (DateTime.UtcNow + Vida, v); }
    public void Invalidar() { lock (_lock) { _roles = null; _opciones = null; } }
}

public class ConfiguracionService(IConfiguracionRepository repository, IUnitOfWork unitOfWork, ConfiguracionCache cache) : IConfiguracionService
{
    private static readonly Regex DominioValido = new(@"^(?=.{3,253}$)([a-z0-9]([a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}$", RegexOptions.Compiled);

    // Textos de respaldo si la tabla todavía no tiene la fila (así la pantalla nunca queda con un rol sin nombre).
    private static readonly IReadOnlyDictionary<string, (string Etiqueta, string Descripcion)> Defaults = new Dictionary<string, (string, string)>
    {
        [Roles.SuperAdmin] = ("Super admin", "Manda en todo: es el único que crea, edita y elimina usuarios."),
        [Roles.Admin] = ("Admin", "Administra el sistema y decide las solicitudes de eliminación. No toca usuarios."),
        [Roles.Manager] = ("Director", "Director de sus proyectos: endosa la eliminación de los que tiene a cargo."),
        [Roles.Miembro] = ("Miembro", "Trabaja en el sistema; para eliminar algo tiene que solicitarlo."),
    };

    public async Task<IReadOnlyList<RolConfigDto>> GetRolesAsync(CancellationToken ct = default)
    {
        if (cache.Roles is { } enCache) return enCache;
        var roles = await LeerRolesAsync(ct);
        cache.GuardarRoles(roles);
        return roles;
    }

    private async Task<IReadOnlyList<RolConfigDto>> LeerRolesAsync(CancellationToken ct)
    {
        var guardados = (await repository.GetRolesAsync(ct)).ToDictionary(x => x.Rol);
        return Roles.Todos.Select(rol =>
        {
            var (etiqueta, descripcion) = Defaults[rol];
            return guardados.TryGetValue(rol, out var r)
                ? new RolConfigDto { Rol = rol, Etiqueta = r.Etiqueta, Descripcion = r.Descripcion }
                : new RolConfigDto { Rol = rol, Etiqueta = etiqueta, Descripcion = descripcion };
        }).ToList();
    }

    public async Task<RolConfigDto> ActualizarRolAsync(string rol, ActualizarRolConfigDto dto, CancellationToken ct = default)
    {
        if (!Roles.Todos.Contains(rol)) throw new BusinessRuleException("Ese rol no existe.");
        var etiqueta = (dto.Etiqueta ?? string.Empty).Trim();
        var descripcion = (dto.Descripcion ?? string.Empty).Trim();
        if (etiqueta.Length == 0) throw new BusinessRuleException("El nombre del rol es requerido.");
        if (etiqueta.Length > 60) throw new BusinessRuleException("El nombre del rol no puede superar 60 caracteres.");
        if (descripcion.Length > 255) throw new BusinessRuleException("La descripción no puede superar 255 caracteres.");

        var otros = (await LeerRolesAsync(ct)).Where(r => r.Rol != rol);
        if (otros.Any(r => string.Equals(r.Etiqueta, etiqueta, StringComparison.OrdinalIgnoreCase)))
            throw new BusinessRuleException("Ya hay otro rol con ese nombre.");

        var existente = await repository.GetRolAsync(rol, ct);
        if (existente is null)
            await repository.AddRolAsync(new RolConfig { Rol = rol, Etiqueta = etiqueta, Descripcion = descripcion, UpdatedAt = DateTime.UtcNow }, ct);
        else
        {
            existente.Etiqueta = etiqueta; existente.Descripcion = descripcion; existente.UpdatedAt = DateTime.UtcNow;
        }
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidar();
        return new RolConfigDto { Rol = rol, Etiqueta = etiqueta, Descripcion = descripcion };
    }

    public async Task<IReadOnlyDictionary<string, List<OpcionConfigDto>>> GetOpcionesAsync(CancellationToken ct = default)
    {
        if (cache.Opciones is { } enCache) return enCache;
        var todas = await repository.GetOpcionesAsync(ct);
        var resultado = ListasConfigurables.Todas.ToDictionary(
            lista => lista,
            lista => todas.Where(o => o.Lista == lista).OrderBy(o => o.Orden).ThenBy(o => o.Valor, StringComparer.OrdinalIgnoreCase).Select(Mapear).ToList());
        cache.GuardarOpciones(resultado);
        return resultado;
    }

    public async Task<OpcionConfigDto> CrearOpcionAsync(string lista, GuardarOpcionDto dto, CancellationToken ct = default)
    {
        ValidarLista(lista);
        var valor = ValidarValor(dto.Valor);
        if (await repository.OpcionExisteAsync(lista, valor, null, ct)) throw new BusinessRuleException("Esa opción ya existe en la lista.");
        var opcion = new OpcionConfig { Lista = lista, Valor = valor, Orden = await repository.SiguienteOrdenAsync(lista, ct) };
        await repository.AddOpcionAsync(opcion, ct);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidar();
        return Mapear(opcion);
    }

    public async Task<OpcionConfigDto> ActualizarOpcionAsync(string lista, Guid id, GuardarOpcionDto dto, CancellationToken ct = default)
    {
        ValidarLista(lista);
        var valor = ValidarValor(dto.Valor);
        var opcion = await repository.GetOpcionAsync(lista, id, ct) ?? throw new EntityNotFoundException(nameof(OpcionConfig), id);
        if (string.Equals(opcion.Valor, valor, StringComparison.Ordinal)) return Mapear(opcion);
        if (ListasConfigurables.EsProtegido(lista, opcion.Valor))
            throw new BusinessRuleException($"“{opcion.Valor}” no se puede renombrar: el sistema lo usa para calcular la prioridad de los proyectos. Puedes agregar otras opciones.");
        if (await repository.OpcionExisteAsync(lista, valor, id, ct)) throw new BusinessRuleException("Esa opción ya existe en la lista.");
        await repository.RenombrarOpcionAsync(opcion, valor, ct);
        cache.Invalidar();
        return Mapear(opcion);
    }

    public async Task EliminarOpcionAsync(string lista, Guid id, CancellationToken ct = default)
    {
        ValidarLista(lista);
        var opcion = await repository.GetOpcionAsync(lista, id, ct) ?? throw new EntityNotFoundException(nameof(OpcionConfig), id);
        if (ListasConfigurables.EsProtegido(lista, opcion.Valor))
            throw new BusinessRuleException($"“{opcion.Valor}” no se puede eliminar: el sistema lo usa para calcular la prioridad de los proyectos.");
        repository.RemoveOpcion(opcion);
        await unitOfWork.SaveChangesAsync(ct);
        cache.Invalidar();
    }

    public async Task<IReadOnlyList<DominioCorreoDto>> GetDominiosAsync(CancellationToken ct = default) =>
        (await repository.GetDominiosAsync(ct)).Select(d => new DominioCorreoDto { Id = d.Id, Dominio = d.Dominio }).ToList();

    public async Task<DominioCorreoDto> CrearDominioAsync(GuardarDominioDto dto, CancellationToken ct = default)
    {
        var dominio = (dto.Dominio ?? string.Empty).Trim().TrimStart('@').ToLowerInvariant();
        if (!DominioValido.IsMatch(dominio)) throw new BusinessRuleException("Escribe un dominio válido, por ejemplo empresa.com.");
        if (await repository.DominioExisteAsync(dominio, ct)) throw new BusinessRuleException("Ese dominio ya está permitido.");
        var entidad = new DominioCorreoPermitido { Dominio = dominio };
        await repository.AddDominioAsync(entidad, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new DominioCorreoDto { Id = entidad.Id, Dominio = entidad.Dominio };
    }

    public async Task EliminarDominioAsync(Guid id, CancellationToken ct = default)
    {
        var dominio = await repository.GetDominioAsync(id, ct) ?? throw new EntityNotFoundException(nameof(DominioCorreoPermitido), id);
        // Sin ningún dominio permitido nadie podría ser invitado ni registrado -- se bloquea quedarse en cero.
        if (await repository.ContarDominiosAsync(ct) <= 1) throw new BusinessRuleException("Debe quedar al menos un dominio permitido; si no, nadie podría ser invitado.");
        repository.RemoveDominio(dominio);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void ValidarLista(string lista)
    {
        if (!ListasConfigurables.Todas.Contains(lista)) throw new EntityNotFoundException("ListaConfigurable", Guid.Empty);
    }

    private static string ValidarValor(string? valor)
    {
        var limpio = (valor ?? string.Empty).Trim();
        if (limpio.Length == 0) throw new BusinessRuleException("El texto de la opción es requerido.");
        if (limpio.Length > 100) throw new BusinessRuleException("La opción no puede superar 100 caracteres.");
        return limpio;
    }

    private static OpcionConfigDto Mapear(OpcionConfig o) => new() { Id = o.Id, Valor = o.Valor, Orden = o.Orden, Protegido = ListasConfigurables.EsProtegido(o.Lista, o.Valor) };
}
