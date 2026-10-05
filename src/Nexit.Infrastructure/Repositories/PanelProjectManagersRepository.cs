using Microsoft.EntityFrameworkCore;
using Nexit.Core.Constants;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class PanelProjectManagersRepository(NexitDbContext context) : IPanelProjectManagersRepository
{
    public async Task<IReadOnlyList<PanelProyectoFila>> GetProyectosAsync(CancellationToken ct = default)
    {
        var filas = await (from p in context.Proyectos.AsNoTracking()
                           join e in context.EstadosProyecto.AsNoTracking() on p.EstadoId equals e.Id
                           select new
                           {
                               p.Id, p.Nombre, p.GerenteId, p.ClienteId, ClienteNombre = p.Cliente != null ? p.Cliente.Nombre : null,
                               Estado = e.Nombre, p.PorcentajeAvance, p.FechaEvento, p.Prioridad,
                               Equipo = p.Equipo.Select(x => new { x.Nombre, x.Rol }).ToList(),
                               Proveedores = p.Proveedores.Select(x => new { x.ProveedorId, x.Proveedor.Nombre }).ToList(),
                           }).ToListAsync(ct);
        return filas.Select(f => new PanelProyectoFila(
            f.Id, f.Nombre, f.GerenteId, f.ClienteId, f.ClienteNombre, f.Estado, f.PorcentajeAvance, f.FechaEvento, f.Prioridad,
            f.Equipo.Select(x => (x.Nombre, x.Rol)).ToList(),
            f.Proveedores.Select(x => (x.ProveedorId, x.Nombre)).ToList())).ToList();
    }

    public async Task<IReadOnlyList<PanelUsuarioFila>> GetPosiblesProjectManagersAsync(IReadOnlyCollection<Guid> gerenteIds, CancellationToken ct = default) =>
        await context.Usuarios.AsNoTracking()
            .Where(u => (u.Rol == Roles.Manager && u.Activo) || gerenteIds.Contains(u.Id))
            .Select(u => new PanelUsuarioFila(u.Id, u.Nombre, u.Apellido, u.Email, u.Rol, u.Activo, u.Iniciales))
            .ToListAsync(ct);
}
