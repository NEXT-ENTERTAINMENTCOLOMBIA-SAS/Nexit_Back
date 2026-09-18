using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class ClienteAdjuntoRepository(NexitDbContext context) : Repository<ClienteAdjunto>(context), IClienteAdjuntoRepository
{
    public async Task<IReadOnlyList<ClienteAdjunto>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Where(x => x.ClienteId == clienteId).OrderByDescending(x => x.Fecha).ToListAsync(cancellationToken);
}
