using Microsoft.EntityFrameworkCore;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;

namespace Nexit.Infrastructure.Repositories;

public class InformesRepository : Repository<InformeSnapshot>, IInformesRepository
{
    private readonly NexitDbContext context;
    public InformesRepository(NexitDbContext context) : base(context) => this.context = context;
    public Task<InformeSnapshot?> GetByPeriodoAsync(string tipo, string periodoKey, CancellationToken cancellationToken = default) =>
        DbSet.AsNoTracking().FirstOrDefaultAsync(x => x.Tipo == tipo && x.PeriodoKey == periodoKey, cancellationToken);

    public async Task<InformeDatos> ObtenerDatosAsync(CancellationToken cancellationToken = default)
    {
        var porEstado = await (from proyecto in context.Proyectos
                               join estado in context.EstadosProyecto on proyecto.EstadoId equals estado.Id
                               group proyecto by estado.Nombre into grupo
                               select new { grupo.Key, Total = grupo.Count() }).ToDictionaryAsync(x => x.Key, x => x.Total, cancellationToken);
        return new InformeDatos(
            await context.Proveedores.CountAsync(cancellationToken),
            await context.Clientes.CountAsync(cancellationToken),
            await context.Proyectos.CountAsync(cancellationToken),
            await context.Proyectos.CountAsync(x => !x.Proveedores.Any(), cancellationToken),
            porEstado);
    }
}
