using System.Text.Json;
using Nexit.Application.DTOs.Informes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Application.UseCases.Informes;

public interface IConsultarInformesUseCase { Task<InformeResumenDto> ObtenerResumenAsync(CancellationToken cancellationToken = default); Task<InformeSnapshotDto> ObtenerSnapshotAsync(string tipo, string periodoKey, CancellationToken cancellationToken = default); }
public interface IGenerarInformeSnapshotUseCase { Task<InformeSnapshotDto> ExecuteAsync(CrearInformeSnapshotDto input, Guid usuarioId, CancellationToken cancellationToken = default); }

public class ConsultarInformesUseCase(IInformesRepository repository) : IConsultarInformesUseCase
{
    public async Task<InformeResumenDto> ObtenerResumenAsync(CancellationToken ct = default) => InformesMapper.ToResumen(await repository.ObtenerDatosAsync(ct));
    public async Task<InformeSnapshotDto> ObtenerSnapshotAsync(string tipo, string periodoKey, CancellationToken ct = default) => InformesMapper.ToSnapshot(await repository.GetByPeriodoAsync(tipo, periodoKey, ct) ?? throw new EntityNotFoundException("InformeSnapshot", Guid.Empty));
}

public class GenerarInformeSnapshotUseCase(IInformesRepository repository, IUnitOfWork unitOfWork) : IGenerarInformeSnapshotUseCase
{
    public async Task<InformeSnapshotDto> ExecuteAsync(CrearInformeSnapshotDto input, Guid usuarioId, CancellationToken ct = default)
    {
        if (input.Tipo is not ("semanal" or "mensual")) throw new BusinessRuleException("El tipo de informe debe ser semanal o mensual.");
        if (string.IsNullOrWhiteSpace(input.PeriodoKey)) throw new BusinessRuleException("El período del informe es requerido.");
        if (await repository.GetByPeriodoAsync(input.Tipo, input.PeriodoKey.Trim(), ct) is not null) throw new BusinessRuleException("Ya existe un informe para ese período.");
        var datos = await repository.ObtenerDatosAsync(ct);
        var snapshot = new InformeSnapshot { Tipo = input.Tipo, PeriodoKey = input.PeriodoKey.Trim(), TotalProveedores = datos.TotalProveedores, TotalClientes = datos.TotalClientes, TotalProyectos = datos.TotalProyectos, ProyectosSinProveedor = datos.ProyectosSinProveedor, PorEstado = JsonSerializer.Serialize(datos.PorEstado), CreatedBy = usuarioId };
        await repository.AddAsync(snapshot, ct); await unitOfWork.SaveChangesAsync(ct);
        return InformesMapper.ToSnapshot(snapshot);
    }
}

internal static class InformesMapper
{
    public static InformeResumenDto ToResumen(InformeDatos datos) => new() { TotalProveedores = datos.TotalProveedores, TotalClientes = datos.TotalClientes, TotalProyectos = datos.TotalProyectos, ProyectosSinProveedor = datos.ProyectosSinProveedor, PorEstado = datos.PorEstado };
    public static InformeSnapshotDto ToSnapshot(InformeSnapshot snapshot) => new() { Id = snapshot.Id, Tipo = snapshot.Tipo, PeriodoKey = snapshot.PeriodoKey, CreatedAt = snapshot.CreatedAt, TotalProveedores = snapshot.TotalProveedores, TotalClientes = snapshot.TotalClientes, TotalProyectos = snapshot.TotalProyectos, ProyectosSinProveedor = snapshot.ProyectosSinProveedor, PorEstado = Deserialize(snapshot.PorEstado) };
    private static IReadOnlyDictionary<string, int> Deserialize(string json) => JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new Dictionary<string, int>();
}
