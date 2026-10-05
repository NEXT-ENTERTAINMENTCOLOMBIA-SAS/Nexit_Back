-- ============================================================
-- 30 - Quitar el estado del brief y abrir el cargo del equipo a texto libre
--
-- Cambios pedidos por Alicia (2026-09-29):
--  * El estado del brief no brinda valor: se elimina proyectos.estado_brief
--    (con su indice y su CHECK) e informes_snapshot.por_brief.
--  * "Miembros del equipo": el cargo en el proyecto (proyecto_equipo.rol) pasa
--    a texto libre, asi que se elimina el CHECK de lista fija.
--
-- ATENCION: DROP COLUMN borra esos datos de forma definitiva. Haz un respaldo
-- antes de correrlo en produccion. Es idempotente (se puede correr mas de una
-- vez). Publica el backend nuevo ANTES o justo despues: el codigo viejo sigue
-- leyendo estado_brief.
-- ============================================================

BEGIN;

ALTER TABLE proyectos DROP CONSTRAINT IF EXISTS ck_proyectos_brief;
DROP INDEX IF EXISTS ix_proyectos_estado_brief;
ALTER TABLE proyectos DROP COLUMN IF EXISTS estado_brief;

ALTER TABLE informes_snapshot DROP COLUMN IF EXISTS por_brief;

ALTER TABLE proyecto_equipo DROP CONSTRAINT IF EXISTS ck_proyecto_equipo_rol;

COMMIT;
