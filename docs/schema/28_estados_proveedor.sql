-- ============================================================================
-- 28_estados_proveedor.sql
--
-- "Estados de gestión de proveedores" (Activo / En evaluación / Pausado /
-- Bloqueado) pasa de estar fijo en el código a ser un catálogo editable desde
-- Configuración -- 2026-09-10, a pedido explícito de Alicia ("hazlo").
--
-- `proveedores.estado` SIGUE siendo texto libre (no se vuelve una FK): lo
-- único que cambia es que el conjunto de valores permitidos deja de estar
-- fijo en un CHECK constraint y pasa a este catálogo, que se puede editar
-- desde la pantalla. Se elimina el CHECK viejo y se siembra la tabla nueva
-- con los 4 valores que ya usan los proveedores existentes -- ningún dato
-- existente cambia.
--
-- Idempotente -- se puede correr más de una vez sin duplicar nada ni fallar
-- si algo ya existe.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS estados_proveedor (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    nombre character varying(255) NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_estados_proveedor_nombre ON estados_proveedor (nombre);

-- Semilla: los mismos 4 valores que ya usan los proveedores existentes, para que nada quede
-- huérfano. ON CONFLICT es seguro porque "nombre" ya tiene restricción UNIQUE real.
INSERT INTO estados_proveedor (nombre) VALUES
    ('Activo'),
    ('En evaluación'),
    ('Pausado'),
    ('Bloqueado')
ON CONFLICT (nombre) DO NOTHING;

-- El check fijo ya no aplica -- el catálogo de arriba es ahora la fuente de verdad editable.
ALTER TABLE proveedores DROP CONSTRAINT IF EXISTS ck_proveedores_estado;

COMMIT;

-- --- Verificación -------------------------------------------------------------
SELECT 'estados_proveedor' AS tabla, count(*) AS filas FROM estados_proveedor;
