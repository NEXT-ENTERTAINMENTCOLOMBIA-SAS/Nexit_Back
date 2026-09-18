namespace Nexit.Core.Entities;

/// <summary>
/// Catálogo editable de los estados de gestión de un proveedor (Activo / En evaluación / Pausado /
/// Bloqueado, y lo que Alicia quiera agregar/renombrar desde Configuración) -- agregado 2026-09-10 a
/// pedido explícito de Alicia ("hazlo"). Antes esta lista vivía fija en el código (`ck_proveedores_estado`
/// y `PROVEEDOR_ESTADOS` del frontend); ahora es un catálogo real, mismo patrón que <see cref="CategoriaProveedor"/>.
///
/// A propósito NO es una FK desde <see cref="Proveedor"/> (que sigue guardando `Estado` como texto
/// libre, igual que antes): convertirlo en FK habría exigido migrar cada proveedor existente a un Id
/// nuevo, un riesgo real sobre datos de producción para un cambio que es, en el fondo, solo "qué
/// valores aparecen en el desplegable". El check constraint viejo que limitaba `proveedores.estado` a
/// esos 4 valores exactos se elimina (ver `docs/schema/28_estados_proveedor.sql`) -- ahora cualquier
/// nombre de este catálogo es válido.
/// </summary>
public class EstadoProveedor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
}
