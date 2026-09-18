namespace Nexit.Application.DTOs.Clientes;

public class CrearClienteAdjuntoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Url { get; set; }
    public string? StoragePath { get; set; }
    public string? Meta { get; set; }
    public string? ContentType { get; set; }
    public long? TamanoBytes { get; set; }
    public DateTime? Fecha { get; set; }
}

public class ClienteAdjuntoDto : CrearClienteAdjuntoDto
{
    public Guid Id { get; init; }
    public Guid ClienteId { get; init; }
    public new DateTime Fecha { get; init; }
    public DateTime CreatedAt { get; init; }
}
