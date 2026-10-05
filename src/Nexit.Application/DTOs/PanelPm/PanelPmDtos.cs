namespace Nexit.Application.DTOs.PanelPm;

public class PanelPmResumenDto
{
    public int ProjectManagersConProyectos { get; set; }
    public int ProjectManagersSinProyectos { get; set; }
    public int TotalProyectos { get; set; }
    public int ProyectosSinProjectManager { get; set; }
    public int PersonasEnProyectos { get; set; }
    public int ClientesActivos { get; set; }
    public int ProveedoresActivos { get; set; }
}

public class PanelPmReferenciaDto
{
    public Guid? Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    /// <summary>Cuántos de los proyectos de este Project Manager involucran a esta persona/cliente/proveedor.</summary>
    public int Proyectos { get; set; }
}

public class PanelPmPersonaDto
{
    public string Nombre { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public int Proyectos { get; set; }
}

public class PanelPmProyectoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Cliente { get; set; }
    public string Estado { get; set; } = string.Empty;
    public int PorcentajeAvance { get; set; }
    public DateTime? FechaEvento { get; set; }
    public string? Prioridad { get; set; }
    public int Personas { get; set; }
    public List<string> Proveedores { get; set; } = [];
}

public class PanelPmProjectManagerDto
{
    /// <summary>Null en la tarjeta especial "Sin Project Manager".</summary>
    public Guid? Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Iniciales { get; set; }
    public bool Activo { get; set; } = true;
    public int TotalProyectos { get; set; }
    public int TotalPersonas { get; set; }
    public int TotalClientes { get; set; }
    public int TotalProveedores { get; set; }
    public List<PanelPmProyectoDto> Proyectos { get; set; } = [];
    public List<PanelPmPersonaDto> Personas { get; set; } = [];
    public List<PanelPmReferenciaDto> Clientes { get; set; } = [];
    public List<PanelPmReferenciaDto> Proveedores { get; set; } = [];
}

public class PanelPmDto
{
    public PanelPmResumenDto Resumen { get; set; } = new();
    public List<PanelPmProjectManagerDto> ProjectManagers { get; set; } = [];
    /// <summary>Proyectos sin gerente asignado -- null si no hay ninguno.</summary>
    public PanelPmProjectManagerDto? SinProjectManager { get; set; }
}
