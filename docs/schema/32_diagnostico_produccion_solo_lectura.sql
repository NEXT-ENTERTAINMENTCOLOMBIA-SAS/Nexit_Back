-- ============================================================================
-- 32_diagnostico_produccion_solo_lectura.sql
--
-- SOLO LECTURA: no modifica nada. Pégalo en el SQL Editor de Supabase para ver
-- qué scripts de docs/schema faltan en producción. Si algún resultado dice
-- "FALTA", ese script es la causa probable de errores como
-- "La operación no pudo completarse por una restricción de datos".
-- ============================================================================

SELECT
  'script 30 (quitar brief / cargo libre)' AS revision,
  CASE
    WHEN EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'proyectos' AND column_name = 'estado_brief')
      OR EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyecto_equipo_rol')
    THEN 'FALTA: correr docs/schema/30_quitar_brief_y_cargo_libre_equipo.sql'
    ELSE 'OK'
  END AS estado
UNION ALL
SELECT
  'script 31 (configuración editable)',
  CASE
    WHEN to_regclass('public.roles_config') IS NULL OR to_regclass('public.opciones_config') IS NULL
    THEN 'FALTA: correr docs/schema/31_configuracion_editable.sql'
    WHEN EXISTS (SELECT 1 FROM pg_constraint WHERE conname IN ('ck_proyectos_tipo','ck_proyectos_prioridad','ck_proyectos_propuesta','ck_proyecto_seguimiento_area'))
    THEN 'FALTA: los CHECK de listas siguen activos (re-correr el script 31)'
    ELSE 'OK'
  END
UNION ALL
SELECT
  'script 29 (FK y CHECK sincronizados)',
  CASE WHEN (SELECT count(*) FROM pg_constraint WHERE contype = 'f' AND connamespace = 'public'::regnamespace) >= 41
       THEN 'OK' ELSE 'FALTA: correr docs/schema/29_sincronizar_fk_check_constraints_produccion.sql' END
UNION ALL
SELECT
  'migraciones EF registradas',
  (SELECT count(*)::text || ' migraciones registradas en __EFMigrationsHistory (el código tiene 18 migraciones)' FROM "__EFMigrationsHistory")
UNION ALL
SELECT
  'tablas de public SIN RLS',
  COALESCE((SELECT string_agg(relname, ', ') FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND NOT c.relrowsecurity), 'ninguna (bien)')
UNION ALL
SELECT
  'usuarios activos / proyectos / proyectos sin gerente',
  (SELECT count(*) FROM usuarios WHERE activo)::text || ' / ' || (SELECT count(*) FROM proyectos)::text || ' / ' || (SELECT count(*) FROM proyectos WHERE gerente_id IS NULL)::text;
