-- ============================================================================
-- 27_cliente_proyecto_adjuntos.sql
--
-- "Archivos y enlaces" para Clientes y Proyectos (2026-09-10, a pedido de
-- Alicia). El frontend ya tenía la pantalla lista desde el 2026-09-09 (ver
-- ClienteAdjuntosController / ProyectoAdjuntosController nuevos), pero
-- apuntaba a un endpoint que no existía del lado del servidor -- por eso
-- seguía pidiendo "guarda primero" en la práctica, aunque el formulario ya no
-- se cerraba al crear. Mismo esquema que `proveedor_adjuntos` (docs/28,
-- HU-13), que sí ya funciona en producción -- esto solo lo replica para las
-- otras dos entidades.
--
-- Idempotente -- se puede correr más de una vez sin duplicar nada ni fallar
-- si algo ya existe.
-- ============================================================================

BEGIN;

-- --- Clientes ---------------------------------------------------------------

CREATE TABLE IF NOT EXISTS cliente_adjuntos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    cliente_id uuid NOT NULL,
    tipo character varying(10) NOT NULL,
    nombre character varying(255) NOT NULL,
    url text,
    storage_path text,
    meta text,
    content_type character varying(255),
    tamano_bytes bigint,
    fecha timestamp with time zone NOT NULL DEFAULT CURRENT_DATE,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

DO $$ BEGIN
    ALTER TABLE cliente_adjuntos ADD CONSTRAINT fk_cliente_adjuntos_clientes_cliente_id
        FOREIGN KEY (cliente_id) REFERENCES clientes (id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE cliente_adjuntos ADD CONSTRAINT ck_cliente_adjuntos_tipo
        CHECK (tipo IN ('link', 'file'));
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS ix_cliente_adjuntos_cliente_id ON cliente_adjuntos (cliente_id);

-- --- Proyectos ----------------------------------------------------------------

CREATE TABLE IF NOT EXISTS proyecto_adjuntos (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    proyecto_id uuid NOT NULL,
    tipo character varying(10) NOT NULL,
    nombre character varying(255) NOT NULL,
    url text,
    storage_path text,
    meta text,
    content_type character varying(255),
    tamano_bytes bigint,
    fecha timestamp with time zone NOT NULL DEFAULT CURRENT_DATE,
    created_at timestamp with time zone NOT NULL DEFAULT now()
);

DO $$ BEGIN
    ALTER TABLE proyecto_adjuntos ADD CONSTRAINT fk_proyecto_adjuntos_proyectos_proyecto_id
        FOREIGN KEY (proyecto_id) REFERENCES proyectos (id) ON DELETE CASCADE;
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

DO $$ BEGIN
    ALTER TABLE proyecto_adjuntos ADD CONSTRAINT ck_proyecto_adjuntos_tipo
        CHECK (tipo IN ('link', 'file'));
EXCEPTION WHEN duplicate_object THEN NULL; END $$;

CREATE INDEX IF NOT EXISTS ix_proyecto_adjuntos_proyecto_id ON proyecto_adjuntos (proyecto_id);

COMMIT;

-- --- Verificación -------------------------------------------------------------
SELECT 'cliente_adjuntos' AS tabla, count(*) AS filas FROM cliente_adjuntos
UNION ALL
SELECT 'proyecto_adjuntos', count(*) FROM proyecto_adjuntos;
