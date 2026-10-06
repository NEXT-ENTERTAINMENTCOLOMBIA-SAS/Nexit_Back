namespace Nexit.Core.Entities;

public class Cliente : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Sector { get; set; }
    /// <summary>
    /// País/departamento/ciudad "de catálogo" (mismas tablas que usa Proveedor) -- agregados
    /// 2026-09-03 para el formulario en cascada del mockup aprobado. Todos opcionales (a
    /// diferencia de Proveedor, donde PaisId es obligatorio) porque los clientes existentes,
    /// creados antes de este cambio, solo tienen la <see cref="Ciudad"/> de texto libre de abajo
    /// y no se puede inferir su país de forma confiable en una migración.
    /// </summary>
    public Guid? PaisId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? CiudadId { get; set; }
    /// <summary>"Activo" / "Prospecto" / "Inactivo" -- ver constraint ck_clientes_estado en NexitDbContext.</summary>
    public string Estado { get; set; } = "Activo";
    /// <summary>
    /// Etapa del proceso comercial (E1-E6, catálogo <see cref="EtapaCliente"/> -- ver docs/33). Distinto
    /// de <see cref="Estado"/>: Estado es un estatus general (Activo/Prospecto/Inactivo), Etapa es dónde
    /// va la relación comercial dentro del proceso de Next. Opcional -- los clientes existentes antes de
    /// este cambio no tienen etapa asignada todavía.
    /// </summary>
    public Guid? EtapaId { get; set; }
    /// <summary>Ciudad como texto libre -- se conserva para los clientes creados antes de <see cref="CiudadId"/>;
    /// el frontend prioriza el nombre resuelto de CiudadId cuando está presente.</summary>
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public string? Web { get; set; }
    public string? Contacto { get; set; }
    public string? CargoContacto { get; set; }
    /// <summary>Texto libre histórico; se conserva cuando no se pudo convertir a número. Lo nuevo va en <see cref="ValorReferenciaMonto"/>.</summary>
    public string? ValorReferencia { get; set; }
    public decimal? ValorReferenciaMonto { get; set; }
    public string Moneda { get; set; } = "COP";
    public string? Notas { get; set; }
    public ICollection<ClienteTelefono> Telefonos { get; set; } = new List<ClienteTelefono>();
    /// <summary>
    /// Lista simple de correos, sin "principal" -- un cliente puede tener más de uno (2026-09-06,
    /// hallazgo real al importar el histórico: el mismo contacto puede traer dos correos). Mismo
    /// patrón que <see cref="Telefonos"/>: nada de flag de cuál es el correo "de verdad", el orden
    /// de la lista es el único orden que existe.
    /// </summary>
    public ICollection<ClienteEmail> Emails { get; set; } = new List<ClienteEmail>();
    public ICollection<Proyecto> Proyectos { get; set; } = new List<Proyecto>();
}
