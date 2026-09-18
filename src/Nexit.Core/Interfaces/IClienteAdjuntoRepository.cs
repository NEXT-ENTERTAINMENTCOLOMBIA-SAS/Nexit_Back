using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

public interface IClienteAdjuntoRepository : IRepository<ClienteAdjunto>
{
    Task<IReadOnlyList<ClienteAdjunto>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken = default);
}
