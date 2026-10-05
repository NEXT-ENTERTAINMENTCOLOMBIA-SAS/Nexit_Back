using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

public interface IClienteNotaRepository : IRepository<ClienteNota>
{
    /// <summary>Notas del cliente, más reciente primero, con el autor cargado para mostrar su nombre.</summary>
    Task<IReadOnlyList<ClienteNota>> GetByClienteIdAsync(Guid clienteId, CancellationToken cancellationToken = default);
}
