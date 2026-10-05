using Nexit.Core.Entities;

namespace Nexit.Core.Interfaces;

public record InformeDatos(int TotalProveedores, int TotalClientes, int TotalProyectos, int ProyectosSinProveedor, IReadOnlyDictionary<string, int> PorEstado);

public interface IInformesRepository : IRepository<InformeSnapshot>
{
    Task<InformeDatos> ObtenerDatosAsync(CancellationToken cancellationToken = default);
    Task<InformeSnapshot?> GetByPeriodoAsync(string tipo, string periodoKey, CancellationToken cancellationToken = default);
}
