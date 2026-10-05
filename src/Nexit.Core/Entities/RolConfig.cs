namespace Nexit.Core.Entities;

/// <summary>
/// Cómo se llama y qué descripción tiene cada rol DENTRO de la aplicación (Configuración, 2026-10-05).
/// La clave técnica del rol (<see cref="Rol"/>: super_admin/admin/manager/miembro) NO se puede cambiar:
/// las políticas de autorización, el CHECK <c>ck_usuarios_rol</c> y el Auth Hook de Supabase dependen
/// de ella. Lo que sí se puede editar acá es la etiqueta que ven las personas ("manager" → "Project
/// Manager", por ejemplo) y la descripción de qué puede hacer.
/// </summary>
public class RolConfig
{
    public string Rol { get; set; } = string.Empty;
    public string Etiqueta { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
}
