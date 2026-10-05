namespace Nexit.Application.DTOs.Informes;

public class InformeResumenDto
{
    public int TotalProveedores { get; init; }
    public int TotalClientes { get; init; }
    public int TotalProyectos { get; init; }
    public int ProyectosSinProveedor { get; init; }
    public IReadOnlyDictionary<string, int> PorEstado { get; init; } = new Dictionary<string, int>();
}

public class InformeSnapshotDto : InformeResumenDto
{
    public Guid Id { get; init; }
    public string Tipo { get; init; } = string.Empty;
    public string PeriodoKey { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public class CrearInformeSnapshotDto
{
    public string Tipo { get; set; } = string.Empty;
    public string PeriodoKey { get; set; } = string.Empty;
}
