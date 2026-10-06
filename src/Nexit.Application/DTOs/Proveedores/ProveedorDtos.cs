namespace Nexit.Application.DTOs.Proveedores;

public class ProveedorTelefonoDto
{
    public Guid? Id { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string? Etiqueta { get; set; }
}

public class ProveedorEmailDto
{
    public Guid? Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Etiqueta { get; set; }
}

public class CreateProveedorDto
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
    public string? CostoReferencia { get; set; }
    public decimal? CostoReferenciaValor { get; set; }
    public string Moneda { get; set; } = "COP";
    public int? Score { get; set; }
    public string? Presupuesto { get; set; }
    public string? Cobertura { get; set; }
    public string? Notas { get; set; }
    public List<ProveedorTelefonoDto> Telefonos { get; set; } = [];
    public List<ProveedorEmailDto> Emails { get; set; } = [];
    public List<Guid> ServicioIds { get; set; } = [];
}

public class UpdateProveedorDto : CreateProveedorDto { public Guid Id { get; set; } }

/// <summary>Alguien marcado como "trabajando con este proveedor" (docs/19) -- los "circulitos" de la lista general.</summary>
public class ColaboradorProveedorDto
{
    public Guid UsuarioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Iniciales { get; set; }
}

public class ProveedorResponseDto : CreateProveedorDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ColaboradorProveedorDto> Colaboradores { get; set; } = [];
}

/// <summary>"A qué proveedor prestarle atención" (docs/21, docs/24) -- puntuado con las razones de cada puntaje.</summary>
public class ProveedorPrioridadResponseDto
{
    public Guid ProveedorId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Puntaje { get; set; }
    public List<string> Razones { get; set; } = [];
}
