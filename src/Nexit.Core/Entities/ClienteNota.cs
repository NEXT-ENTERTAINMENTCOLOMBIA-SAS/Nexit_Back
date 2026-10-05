namespace Nexit.Core.Entities;

/// <summary>
/// Nota interna de un cliente (2026-10-05): bitácora del equipo -- quién escribió qué y de qué área.
/// Mismo molde que <see cref="ProyectoSeguimiento"/>. Solo el equipo la ve; nunca sale hacia el cliente.
/// </summary>
public class ClienteNota : BaseEntity
{
    public Guid ClienteId { get; set; }
    public Guid? AutorId { get; set; }
    public string Area { get; set; } = "General";
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string Nota { get; set; } = string.Empty;
    public Cliente Cliente { get; set; } = null!;
    public Usuario? Autor { get; set; }
}
