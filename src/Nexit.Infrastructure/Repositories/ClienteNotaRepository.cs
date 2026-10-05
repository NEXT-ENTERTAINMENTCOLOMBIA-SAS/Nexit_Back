using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class ClienteNotaRepository(NexitDbContext context) : Repository<ClienteNota>(context), IClienteNotaRepository
{
    public async Task<IReadOnlyList<ClienteNota>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken = default) =>
        await DbSet.AsNoTracking().Include(x => x.Autor).Where(x => x.ClienteId == clienteId)
            .OrderByDescending(x => x.Fecha).ThenByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
}
