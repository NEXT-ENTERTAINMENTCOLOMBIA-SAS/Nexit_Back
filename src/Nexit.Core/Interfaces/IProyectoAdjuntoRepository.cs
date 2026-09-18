using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

public interface IProyectoAdjuntoRepository : IRepository<ProyectoAdjunto>
{
    Task<IReadOnlyList<ProyectoAdjunto>> GetByProyectoIdAsync(Guid proyectoId, CancellationToken cancellationToken = default);
}
