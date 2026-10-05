using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

/// <summary>Acceso a la configuración editable: nombres de roles, listas de Proyectos y dominios de correo permitidos.</summary>
public interface IConfiguracionRepository
{
    Task<IReadOnlyList<RolConfig>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RolConfig?> GetRolAsync(string rol, CancellationToken cancellationToken = default);
    Task AddRolAsync(RolConfig rol, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OpcionConfig>> GetOpcionesAsync(CancellationToken cancellationToken = default);
    Task<OpcionConfig?> GetOpcionAsync(string lista, Guid id, CancellationToken cancellationToken = default);
    Task<bool> OpcionExisteAsync(string lista, string valor, Guid? excluirId, CancellationToken cancellationToken = default);
    Task<short> SiguienteOrdenAsync(string lista, CancellationToken cancellationToken = default);
    Task AddOpcionAsync(OpcionConfig opcion, CancellationToken cancellationToken = default);
    void RemoveOpcion(OpcionConfig opcion);
    /// <summary>Cambia el texto de una opción Y de los proyectos que ya la usaban, en una sola transacción.</summary>
    Task RenombrarOpcionAsync(OpcionConfig opcion, string valorNuevo, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DominioCorreoPermitido>> GetDominiosAsync(CancellationToken cancellationToken = default);
    Task<DominioCorreoPermitido?> GetDominioAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> DominioExisteAsync(string dominio, CancellationToken cancellationToken = default);
    Task<int> ContarDominiosAsync(CancellationToken cancellationToken = default);
    Task AddDominioAsync(DominioCorreoPermitido dominio, CancellationToken cancellationToken = default);
    void RemoveDominio(DominioCorreoPermitido dominio);
}
