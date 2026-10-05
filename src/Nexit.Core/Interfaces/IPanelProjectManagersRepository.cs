namespace Nexit.Core.Interfaces;

public record PanelProyectoFila(
    Guid Id, string Nombre, Guid? GerenteId, Guid? ClienteId, string? ClienteNombre, string Estado, int PorcentajeAvance,
    DateTime? FechaEvento, string? Prioridad,
    IReadOnlyList<(string Nombre, string Rol)> Equipo, IReadOnlyList<(Guid Id, string Nombre)> Proveedores);

public record PanelUsuarioFila(Guid Id, string Nombre, string Apellido, string Email, string Rol, bool Activo, string? Iniciales);

/// <summary>Lectura (solo lectura) de lo necesario para el panel de Project Managers.</summary>
public interface IPanelProjectManagersRepository
{
    Task<IReadOnlyList<PanelProyectoFila>> GetProyectosAsync(CancellationToken cancellationToken = default);
    /// <summary>Usuarios que pueden ser Project Manager: los de rol manager y los que ya figuran como gerente de algún proyecto.</summary>
    Task<IReadOnlyList<PanelUsuarioFila>> GetPosiblesProjectManagersAsync(IReadOnlyCollection<Guid> gerenteIds, CancellationToken cancellationToken = default);
}
