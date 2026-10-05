namespace Nexit.Application.DTOs.Configuracion;

public class RolConfigDto
{
    /// <summary>Clave técnica (super_admin/admin/manager/miembro) -- no editable.</summary>
    public string Rol { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public class ActualizarRolConfigDto
{
    public string Etiqueta { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

public class OpcionConfigDto
{
    public Guid Id { get; set; }
    public string Valor { get; set; } = string.Empty;
    public short Orden { get; set; }
    /// <summary>True si la lógica de negocio depende de este valor exacto: no se puede renombrar ni eliminar.</summary>
    public bool Protegido { get; set; }
}

public class GuardarOpcionDto
{
    public string Valor { get; set; } = string.Empty;
}

public class DominioCorreoDto
{
    public Guid Id { get; set; }
    public string Dominio { get; set; } = string.Empty;
}

public class GuardarDominioDto
{
    public string Dominio { get; set; } = string.Empty;
}
