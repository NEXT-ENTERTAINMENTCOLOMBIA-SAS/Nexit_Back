using Microsoft.EntityFrameworkCore;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class ConfiguracionRepository(NexitDbContext context) : IConfiguracionRepository
{
    public async Task<IReadOnlyList<RolConfig>> GetRolesAsync(CancellationToken ct = default) => await context.RolesConfig.AsNoTracking().ToListAsync(ct);
    public Task<RolConfig?> GetRolAsync(string rol, CancellationToken ct = default) => context.RolesConfig.FirstOrDefaultAsync(x => x.Rol == rol, ct);
    public async Task AddRolAsync(RolConfig rol, CancellationToken ct = default) => await context.RolesConfig.AddAsync(rol, ct);

    public async Task<IReadOnlyList<OpcionConfig>> GetOpcionesAsync(CancellationToken ct = default) => await context.OpcionesConfig.AsNoTracking().ToListAsync(ct);
    public Task<OpcionConfig?> GetOpcionAsync(string lista, Guid id, CancellationToken ct = default) => context.OpcionesConfig.FirstOrDefaultAsync(x => x.Id == id && x.Lista == lista, ct);
    public Task<bool> OpcionExisteAsync(string lista, string valor, Guid? excluirId, CancellationToken ct = default) =>
        context.OpcionesConfig.AnyAsync(x => x.Lista == lista && x.Valor.ToLower() == valor.ToLower() && (!excluirId.HasValue || x.Id != excluirId.Value), ct);
    public async Task<short> SiguienteOrdenAsync(string lista, CancellationToken ct = default) =>
        (short)((await context.OpcionesConfig.Where(x => x.Lista == lista).Select(x => (short?)x.Orden).MaxAsync(ct) ?? 0) + 1);
    public async Task AddOpcionAsync(OpcionConfig opcion, CancellationToken ct = default) => await context.OpcionesConfig.AddAsync(opcion, ct);
    public void RemoveOpcion(OpcionConfig opcion) => context.OpcionesConfig.Remove(opcion);

    public async Task RenombrarOpcionAsync(OpcionConfig opcion, string valorNuevo, CancellationToken ct = default)
    {
        var viejo = opcion.Valor;
        await using var tx = await context.Database.BeginTransactionAsync(ct);
        // Los proyectos guardan el texto, no un Id: al renombrar hay que mover también a los que ya usaban el valor viejo.
        switch (opcion.Lista)
        {
            case ListasConfigurables.TipoProyecto: await context.Proyectos.Where(p => p.TipoProyecto == viejo).ExecuteUpdateAsync(s => s.SetProperty(p => p.TipoProyecto, valorNuevo), ct); break;
            case ListasConfigurables.Prioridad: await context.Proyectos.Where(p => p.Prioridad == viejo).ExecuteUpdateAsync(s => s.SetProperty(p => p.Prioridad, valorNuevo), ct); break;
            case ListasConfigurables.SedeNext: await context.Proyectos.Where(p => p.SedeNext == viejo).ExecuteUpdateAsync(s => s.SetProperty(p => p.SedeNext, valorNuevo), ct); break;
            case ListasConfigurables.EstadoPropuesta: await context.Proyectos.Where(p => p.PropuestaEstado == viejo).ExecuteUpdateAsync(s => s.SetProperty(p => p.PropuestaEstado, valorNuevo), ct); break;
            case ListasConfigurables.AreaSeguimiento: await context.ProyectoSeguimientos.Where(p => p.Area == viejo).ExecuteUpdateAsync(s => s.SetProperty(p => p.Area, valorNuevo), ct); break;
        }
        opcion.Valor = valorNuevo;
        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<DominioCorreoPermitido>> GetDominiosAsync(CancellationToken ct = default) => await context.DominiosCorreoPermitidos.AsNoTracking().OrderBy(x => x.Dominio).ToListAsync(ct);
    public Task<DominioCorreoPermitido?> GetDominioAsync(Guid id, CancellationToken ct = default) => context.DominiosCorreoPermitidos.FirstOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> DominioExisteAsync(string dominio, CancellationToken ct = default) => context.DominiosCorreoPermitidos.AnyAsync(x => x.Dominio.ToLower() == dominio.ToLower(), ct);
    public Task<int> ContarDominiosAsync(CancellationToken ct = default) => context.DominiosCorreoPermitidos.CountAsync(ct);
    public async Task AddDominioAsync(DominioCorreoPermitido dominio, CancellationToken ct = default) => await context.DominiosCorreoPermitidos.AddAsync(dominio, ct);
    public void RemoveDominio(DominioCorreoPermitido dominio) => context.DominiosCorreoPermitidos.Remove(dominio);
}
