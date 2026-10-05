namespace Nexit.Application.DTOs.Proyectos;

public class ProyectoEquipoDto
{
    public Guid? Id { get; set; }
    public string Rol { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public class CrearProyectoDto
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
    /// El gerente responsable/dueño del proyecto. Solo un administrador o super administrador puede
    /// asignarlo o cambiarlo explícitamente por este campo — si quien crea/edita el proyecto es un
    /// gerente y no envía este valor, el backend lo asigna automáticamente a sí mismo.
    /// </summary>
    public Guid? GerenteId { get; set; }
    public List<ProyectoEquipoDto> Equipo { get; set; } = [];
    public List<Guid> ProveedorIds { get; set; } = [];
}

public class ActualizarProyectoDto : CrearProyectoDto
{
    public Guid Id { get; set; }
}

public class ProyectoResponseDto : CrearProyectoDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Un proyecto puntuado por <c>ConsultarPrioridadProyectosUseCase</c> (docs/21, docs/22) -- el puntaje siempre viene acompañado de sus razones, nunca solo.</summary>
public class ProyectoPrioridadResponseDto
{
    public Guid ProyectoId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Puntaje { get; set; }
    public List<string> Razones { get; set; } = [];
}

public class CrearSeguimientoProyectoDto
{
    public string Area { get; set; } = "General";
    public DateTime? Fecha { get; set; }
    public string Nota { get; set; } = string.Empty;
}

public class SeguimientoProyectoDto
{
    public Guid Id { get; set; }
    public Guid? AutorId { get; set; }
    public string Area { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Nota { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
