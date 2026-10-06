namespace Nexit.Core.Entities;

public class Proveedor : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public Guid PaisId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? CiudadId { get; set; }
    public Guid CategoriaId { get; set; }
    public string Estado { get; set; } = "Activo";
    public string? Contacto { get; set; }
    public string? CargoContacto { get; set; }
    public string? Web { get; set; }
    public string? Direccion { get; set; }
    public int? Aforo { get; set; }
    /// <summary>Texto libre histórico ("desde $500k"); se conserva cuando no se pudo convertir a número. Lo nuevo va en <see cref="CostoReferenciaValor"/>.</summary>
    public string? CostoReferencia { get; set; }
    public decimal? CostoReferenciaValor { get; set; }
    public string Moneda { get; set; } = "COP";
    public int? Score { get; set; }
    public string? Presupuesto { get; set; }
    public string? Cobertura { get; set; }
    public string? Notas { get; set; }
    public ICollection<ProveedorTelefono> Telefonos { get; set; } = new List<ProveedorTelefono>();
    /// <summary>Lista simple de correos, sin "principal" -- ver el comentario equivalente en <see cref="Cliente.Emails"/>.</summary>
    public ICollection<ProveedorEmail> Emails { get; set; } = new List<ProveedorEmail>();
    public ICollection<ProveedorServicio> Servicios { get; set; } = new List<ProveedorServicio>();
    public ICollection<ProveedorAdjunto> Adjuntos { get; set; } = new List<ProveedorAdjunto>();
    public ICollection<ProyectoProveedor> Proyectos { get; set; } = new List<ProyectoProveedor>();
    /// <summary>Quiénes se marcaron "trabajando con este proveedor" -- ver <see cref="ProveedorColaborador"/> y docs/19.</summary>
    public ICollection<ProveedorColaborador> Colaboradores { get; set; } = new List<ProveedorColaborador>();
}
