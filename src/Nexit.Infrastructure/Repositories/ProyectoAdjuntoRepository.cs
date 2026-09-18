using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class ProyectoAdjuntoRepository(NexitDbContext context) : Repository<ProyectoAdjunto>(context), IProyectoAdjuntoRepository
{
    public async Task<IReadOnlyList<ProyectoAdjunto>> GetByProyectoIdAsync(Guid proyectoId, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Where(x => x.ProyectoId == proyectoId).OrderByDescending(x => x.Fecha).ToListAsync(cancellationToken);
}
