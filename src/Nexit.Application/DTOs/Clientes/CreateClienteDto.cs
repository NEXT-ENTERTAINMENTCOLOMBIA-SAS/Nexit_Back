namespace Nexit.Application.DTOs.Clientes;

public class CreateClienteDto
{
    public string Nombre { get; set; } = string.Empty;
    public string? Sector { get; set; }
    public Guid? PaisId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? CiudadId { get; set; }
    public string Estado { get; set; } = "Activo";
    /// <summary>Etapa del proceso comercial (catálogo EtapaCliente, docs/33). Opcional.</summary>
    public Guid? EtapaId { get; set; }
    public string? Ciudad { get; set; }
    public string? Direccion { get; set; }
    public string? Web { get; set; }
    public string? Contacto { get; set; }
    public string? CargoContacto { get; set; }
    public string? ValorReferencia { get; set; }
    public decimal? ValorReferenciaMonto { get; set; }
    public string Moneda { get; set; } = "COP";
    public string? Notas { get; set; }
    public List<ClienteTelefonoDto> Telefonos { get; set; } = [];
    public List<ClienteEmailDto> Emails { get; set; } = [];
}
