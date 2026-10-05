namespace Nexit.Application.DTOs.Clientes;

public class CrearClienteNotaDto
{
    public string Area { get; set; } = "General";
    public string Nota { get; set; } = string.Empty;
}

public class ClienteNotaDto
{
    public Guid Id { get; set; }
    public Guid? AutorId { get; set; }
    /// <summary>Nombre de quien la escribió (null si esa cuenta ya no existe).</summary>
    public string? AutorNombre { get; set; }
    public string Area { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Nota { get; set; } = string.Empty;
}
