-- ============================================================================
-- 31_configuracion_editable.sql
--
-- Configuración editable de todo el sistema (2026-10-05, pedido de Alicia):
--  * roles_config: nombre y descripción visibles de cada rol (la clave técnica
--    super_admin/admin/manager/miembro NO cambia -- la usan las políticas y el
--    Auth Hook).
--  * opciones_config: las listas de Proyectos que estaban fijas (tipo, prioridad,
--    sede, estado de la propuesta, área de seguimiento). Se eliminan los CHECK
--    que las fijaban; los proyectos siguen guardando el texto.
--
-- Idempotente: se puede correr más de una vez. No borra ni modifica datos de
-- negocio. Aplica ESTE script a producción ANTES o justo después de publicar el
-- backend nuevo (el backend nuevo lee estas dos tablas).
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS roles_config (
    rol character varying(30) PRIMARY KEY,
    etiqueta character varying(60) NOT NULL,
    descripcion character varying(255) NOT NULL DEFAULT '',
    updated_at timestamp with time zone
);

CREATE TABLE IF NOT EXISTS opciones_config (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    lista character varying(40) NOT NULL,
    valor character varying(100) NOT NULL,
    orden smallint NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_opciones_config_lista_valor ON opciones_config (lista, valor);

-- Mismo patrón que TODAS las demás tablas (ver 22_rls_...): RLS activo + política para nexit_app.
-- Sin esto la tabla quedaría legible desde la API pública de Supabase con la anon key.
ALTER TABLE roles_config ENABLE ROW LEVEL SECURITY;
ALTER TABLE opciones_config ENABLE ROW LEVEL SECURITY;

DO $pol$ BEGIN
    CREATE POLICY "solo_nexit_app" ON roles_config FOR ALL TO nexit_app USING (true) WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $pol$;

DO $pol$ BEGIN
    CREATE POLICY "solo_nexit_app" ON opciones_config FOR ALL TO nexit_app USING (true) WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $pol$;

INSERT INTO roles_config (rol, etiqueta, descripcion) VALUES
    ('super_admin', 'Super admin', 'Manda en todo: es el único que crea, edita y elimina usuarios.'),
    ('admin', 'Admin', 'Administra el sistema y decide las solicitudes de eliminación. No toca usuarios.'),
    ('manager', 'Director', 'Director de sus proyectos: endosa la eliminación de los que tiene a cargo.'),
    ('miembro', 'Miembro', 'Trabaja en el sistema; para eliminar algo tiene que solicitarlo.')
ON CONFLICT (rol) DO NOTHING;

INSERT INTO opciones_config (lista, valor, orden) VALUES
    ('tipo-proyecto', 'Corporativo', 1), ('tipo-proyecto', 'Evento social', 2),
    ('prioridad', 'Alta', 1), ('prioridad', 'Media', 2), ('prioridad', 'Baja', 3),
    ('sede-next', 'Bogotá', 1), ('sede-next', 'Ciudad de México', 2),
    ('estado-propuesta', 'No enviada', 1), ('estado-propuesta', 'En proceso', 2), ('estado-propuesta', 'Enviada', 3),
    ('area-seguimiento', 'General', 1), ('area-seguimiento', 'Creativo', 2), ('area-seguimiento', 'Comercial', 3), ('area-seguimiento', 'Administrativo', 4)
ON CONFLICT (lista, valor) DO NOTHING;

-- Las listas ahora son editables: los CHECK fijos ya no aplican.
ALTER TABLE proyectos DROP CONSTRAINT IF EXISTS ck_proyectos_tipo;
ALTER TABLE proyectos DROP CONSTRAINT IF EXISTS ck_proyectos_prioridad;
ALTER TABLE proyectos DROP CONSTRAINT IF EXISTS ck_proyectos_propuesta;
ALTER TABLE proyecto_seguimiento DROP CONSTRAINT IF EXISTS ck_proyecto_seguimiento_area;

-- Registro en el historial de migraciones de EF (si ya existe la tabla), para que
-- `dotnet ef database update` no intente re-crear lo de arriba.
DO $hist$ BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261005210309_ConfiguracionEditable', '8.0.11')
        ON CONFLICT (migration_id) DO NOTHING;
    END IF;
END $hist$;

COMMIT;

-- --- Verificación (solo lectura) ---------------------------------------------
SELECT 'roles_config' AS tabla, count(*) AS filas FROM roles_config
UNION ALL SELECT 'opciones_config', count(*) FROM opciones_config;

SELECT relname AS tabla, relrowsecurity AS rls_activo
FROM pg_class WHERE relname IN ('roles_config', 'opciones_config');
