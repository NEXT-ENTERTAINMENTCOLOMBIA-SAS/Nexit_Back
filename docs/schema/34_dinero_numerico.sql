-- ============================================================================
-- 34_dinero_numerico.sql
--
-- Dinero como número (2026-10-05): antes los valores de referencia eran texto libre
-- ("$1.500.000", "1500000 COP"...) y no se podían sumar ni ordenar. Se agregan columnas
-- numéricas + moneda. Las columnas de texto viejas NO se tocan (quedan como respaldo).
--
-- Idempotente. Correr ANTES de publicar el backend que usa estas columnas.
-- El respaldo de datos solo convierte textos sin ambigüedad (solo dígitos, o miles
-- separados por punto/coma con grupos de 3). Lo demás queda en NULL y se corrige a mano.
-- ============================================================================

BEGIN;

ALTER TABLE proyectos    ADD COLUMN IF NOT EXISTS valor numeric(18,2);
ALTER TABLE proyectos    ADD COLUMN IF NOT EXISTS moneda character varying(3) NOT NULL DEFAULT 'COP';
ALTER TABLE proveedores  ADD COLUMN IF NOT EXISTS costo_referencia_valor numeric(18,2);
ALTER TABLE proveedores  ADD COLUMN IF NOT EXISTS moneda character varying(3) NOT NULL DEFAULT 'COP';
ALTER TABLE clientes     ADD COLUMN IF NOT EXISTS valor_referencia_monto numeric(18,2);
ALTER TABLE clientes     ADD COLUMN IF NOT EXISTS moneda character varying(3) NOT NULL DEFAULT 'COP';

-- Conversión conservadora del texto existente (solo filas aún sin número).
UPDATE proveedores
   SET costo_referencia_valor = regexp_replace(regexp_replace(costo_referencia, '^(\$|COP)\s*', '', 'i'), '[.,\s]', '', 'g')::numeric
 WHERE costo_referencia_valor IS NULL
   AND trim(costo_referencia) ~* '^(\$|COP)?\s*(\d+|\d{1,3}([.,]\d{3})+)$';

UPDATE clientes
   SET valor_referencia_monto = regexp_replace(regexp_replace(valor_referencia, '^(\$|COP)\s*', '', 'i'), '[.,\s]', '', 'g')::numeric
 WHERE valor_referencia_monto IS NULL
   AND trim(valor_referencia) ~* '^(\$|COP)?\s*(\d+|\d{1,3}([.,]\d{3})+)$';

-- Historial de migraciones de EF (para que el backend no intente aplicarla otra vez).
DO $hist$ BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        SELECT '20261005231622_DineroNumerico', '8.0.11'
        WHERE NOT EXISTS (SELECT 1 FROM "__EFMigrationsHistory" WHERE migration_id = '20261005231622_DineroNumerico');
    END IF;
END $hist$;

COMMIT;

-- Verificación (solo lectura):
-- SELECT count(*) FILTER (WHERE costo_referencia IS NOT NULL) AS con_texto,
--        count(*) FILTER (WHERE costo_referencia_valor IS NOT NULL) AS convertidos FROM proveedores;
-- SELECT count(*) FILTER (WHERE valor_referencia IS NOT NULL) AS con_texto,
--        count(*) FILTER (WHERE valor_referencia_monto IS NOT NULL) AS convertidos FROM clientes;
