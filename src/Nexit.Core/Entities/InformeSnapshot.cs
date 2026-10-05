namespace Nexit.Core.Entities;

public class InformeSnapshot : BaseEntity
{
    public string Tipo { get; set; } = string.Empty;
    public string PeriodoKey { get; set; } = string.Empty;
    public int TotalProveedores { get; set; }
    public int TotalClientes { get; set; }
    public int TotalProyectos { get; set; }
    public int ProyectosSinProveedor { get; set; }
    public string PorEstado { get; set; } = "{}";
}
