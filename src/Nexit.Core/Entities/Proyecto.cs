namespace Nexit.Core.Entities;

public class Proyecto : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public Guid? ClienteId { get; set; }
    public string? ContactoProyecto { get; set; }
    public string? TipoProyecto { get; set; }
    public string? Prioridad { get; set; }
    public string? Ciudad { get; set; }
    public string? SedeNext { get; set; }
    public DateTime? FechaSolicitud { get; set; }
    public DateTime? FechaEvento { get; set; }
    public Guid EstadoId { get; set; }
    public int PorcentajeAvance { get; set; }
    public string PropuestaEstado { get; set; } = "No enviada";
    public string? NumeroFactura { get; set; }
    public bool Pagado { get; set; }
    public DateTime? FechaPago { get; set; }
    public string? Notas { get; set; }
    /// <summary>
    /// El gerente (manager) responsable/dueño de este proyecto. Solo esta persona puede tomar
    /// decisiones directas sobre el proyecto (incluido endosar una solicitud de eliminación de
    /// otra persona); el resto del equipo puede trabajar en él pero no es su "dueño". Se asigna
    /// automáticamente al creador cuando quien crea el proyecto ya es gerente; en cualquier otro
    /// caso queda sin asignar hasta que un administrador lo asigne.
    /// </summary>
    public Guid? GerenteId { get; set; }
    public Usuario? Gerente { get; set; }
    public Cliente? Cliente { get; set; }
    public ICollection<ProyectoEquipo> Equipo { get; set; } = new List<ProyectoEquipo>();
    public ICollection<ProyectoProveedor> Proveedores { get; set; } = new List<ProyectoProveedor>();
    public ICollection<ProyectoSeguimiento> Seguimiento { get; set; } = new List<ProyectoSeguimiento>();
}
