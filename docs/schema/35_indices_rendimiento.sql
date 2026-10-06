-- ============================================================================
-- 35_indices_rendimiento.sql
--
-- Índices para que los filtros y la búsqueda del listado de proyectos (api/proyectos/pagina)
-- y de clientes sigan rápidos cuando crezca la data (2026-10-05).
--
-- Idempotente (IF NOT EXISTS). Solo crea índices: no cambia ni borra datos. En una tabla de
-- ~600 filas se crean en milisegundos. Si el rol no puede crear extensiones, la línea de
-- pg_trgm se puede omitir (los índices de texto se saltan solos; el resto funciona igual).
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Búsqueda "contiene" (LIKE '%texto%') por nombre -- coincide con lower(nombre) que usa el backend.
CREATE INDEX IF NOT EXISTS ix_proyectos_nombre_trgm ON proyectos USING gin (lower(nombre) gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_clientes_nombre_trgm ON clientes USING gin (lower(nombre) gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_proyecto_equipo_nombre_trgm ON proyecto_equipo USING gin (lower(nombre) gin_trgm_ops);

-- Filtros por Project Manager, cliente y tipo.
CREATE INDEX IF NOT EXISTS ix_proyectos_gerente_id ON proyectos (gerente_id);
CREATE INDEX IF NOT EXISTS ix_proyectos_cliente_id ON proyectos (cliente_id);
CREATE INDEX IF NOT EXISTS ix_proyectos_tipo_proyecto ON proyectos (tipo_proyecto);

-- Orden por defecto del listado (evento más reciente primero).
CREATE INDEX IF NOT EXISTS ix_proyectos_fecha_evento_nombre ON proyectos (fecha_evento DESC, nombre);

-- Verificación (solo lectura):
-- SELECT indexname FROM pg_indexes WHERE tablename IN ('proyectos','clientes','proyecto_equipo') ORDER BY 1;
