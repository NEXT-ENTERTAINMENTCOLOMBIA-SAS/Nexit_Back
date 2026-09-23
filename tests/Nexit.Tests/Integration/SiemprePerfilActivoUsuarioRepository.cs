using Nexit.Core.Entities;
using Nexit.Core.Interfaces;

namespace Nexit.Tests.Integration;

/// <summary>
/// Envoltorio de <see cref="IUsuarioRepository"/> usado solo en <see cref="NexitApiFactory"/>
/// (pruebas H8 de autorización). <see cref="TestAuthHandler"/> autentica con una identidad
/// sintética (un <c>Guid</c> nuevo por petición, salvo que la prueba fije <c>X-Test-UserId</c>) que
/// nunca tiene fila real en <c>usuarios</c> -- así que <c>PerfilRequeridoFilter</c>, que sí consulta
/// la base de verdad (ver 2026-09-21, docs/40), rechazaría con 403 "perfil_requerido" CUALQUIER
/// petición autenticada antes de llegar siquiera a la política de rol que estas pruebas quieren
/// ejercitar -- por eso `AuthorizationIntegrationTests` empezó a fallar en bloque al agregarse ese
/// filtro, sin que cambiara ninguna regla de autorización real.
///
/// Se fuerza <see cref="EstaActivoAsync"/> a <c>true</c> (perfil existente y activo) para que
/// PerfilRequeridoFilter deje pasar siempre. Esto NO tapa las pruebas de "cuenta desactivada"
/// (<c>GetClientes_with_an_inactive_account_returns_403...</c>,
/// <c>GetUsuarios_with_an_inactive_super_admin_returns_403</c>): esas dependen del claim
/// <c>user_active=false</c> que llega vía <see cref="TestAuthHandler.TestActiveHeader"/> y que
/// revisa el <c>IsActive</c> de las políticas en Program.cs, un camino totalmente aparte de este
/// repositorio.
/// </summary>
public class SiemprePerfilActivoUsuarioRepository(IUsuarioRepository inner) : IUsuarioRepository
{
    public Task<bool?> EstaActivoAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<bool?>(true);

    public Task<bool> ExistsByEmailAsync(string email, Guid? excludedId = null, CancellationToken cancellationToken = default) =>
        inner.ExistsByEmailAsync(email, excludedId, cancellationToken);

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        inner.GetByEmailAsync(email, cancellationToken);

    public Task<IReadOnlyList<Usuario>> GetInactivosDesdeAsync(DateTime limite, CancellationToken cancellationToken = default) =>
        inner.GetInactivosDesdeAsync(limite, cancellationToken);

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Usuario>> GetAllAsync(CancellationToken cancellationToken = default) =>
        inner.GetAllAsync(cancellationToken);

    public Task AddAsync(Usuario entity, CancellationToken cancellationToken = default) =>
        inner.AddAsync(entity, cancellationToken);

    public void Update(Usuario entity) => inner.Update(entity);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        inner.DeleteAsync(id, cancellationToken);
}
