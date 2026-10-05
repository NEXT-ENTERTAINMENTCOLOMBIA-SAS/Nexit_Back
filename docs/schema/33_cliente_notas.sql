-- ============================================================================
-- 33_cliente_notas.sql
--
-- Notas internas de cliente (2026-10-05): bitácora del equipo dentro de cada
-- cliente (quién escribió qué y de qué área). Mismo molde que proyecto_seguimiento.
--
-- Idempotente. No toca datos existentes. Correr ANTES de publicar el backend que
-- trae api/clientes/{id}/notas.
-- ============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS cliente_notas (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cliente_id uuid NOT NULL REFERENCES clientes (id) ON DELETE CASCADE,
    autor_id uuid REFERENCES usuarios (id) ON DELETE SET NULL,
    area character varying(100) NOT NULL DEFAULT 'General',
    fecha timestamp with time zone NOT NULL DEFAULT now(),
    nota text NOT NULL,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_cliente_notas_autor_id ON cliente_notas (autor_id);
CREATE INDEX IF NOT EXISTS ix_cliente_notas_cliente_id_fecha ON cliente_notas (cliente_id, fecha);

ALTER TABLE cliente_notas ENABLE ROW LEVEL SECURITY;

DO $pol$ BEGIN
    CREATE POLICY "solo_nexit_app" ON cliente_notas FOR ALL TO nexit_app USING (true) WITH CHECK (true);
EXCEPTION WHEN duplicate_object THEN NULL; END $pol$;

DO $hist$ BEGIN
    IF to_regclass('public."__EFMigrationsHistory"') IS NOT NULL THEN
        INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
        VALUES ('20261005224844_ClienteNotas', '8.0.11')
        ON CONFLICT (migration_id) DO NOTHING;
    END IF;
END $hist$;

COMMIT;

-- Verificación (solo lectura):
-- SELECT to_regclass('public.cliente_notas');
-- SELECT relrowsecurity FROM pg_class WHERE relname = 'cliente_notas';
