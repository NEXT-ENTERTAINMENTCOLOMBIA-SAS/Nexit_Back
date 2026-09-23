-- ============================================================
-- Sincroniza produccion con el modelo de NexitDbContext:
-- agrega las 41 FOREIGN KEY y 23 CHECK constraints que el codigo
-- ya define (16 migraciones de EF Core) pero que nunca quedaron
-- aplicadas en la base real (creada por fuera de `dotnet ef
-- database update`, con __EFMigrationsHistory vacia).
--
-- Generado y verificado el 2026-09-22: se auditaron las 41
-- relaciones y las 23 reglas CHECK contra los datos reales de
-- produccion -- 0 huerfanos, 0 violaciones. Es seguro aplicar
-- este script tal cual, sin limpieza previa de datos.
--
-- Cada bloque es idempotente (solo agrega si no existe), asi que
-- se puede correr mas de una vez sin error. Revisalo con calma
-- antes de ejecutarlo contra produccion -- recomendado en un
-- horario de bajo trafico, aunque con estos volumenes (proyectos:
-- 634 filas, historial_cambios: 1165) deberia tomar segundos.
-- ============================================================

BEGIN;

-- ---------- FOREIGN KEYS ----------
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_ciudades_regiones_region_id') THEN
    ALTER TABLE "ciudades" ADD CONSTRAINT "fk_ciudades_regiones_region_id"
      FOREIGN KEY ("region_id") REFERENCES "regiones" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_ciudades_ciudad_id') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "fk_clientes_ciudades_ciudad_id"
      FOREIGN KEY ("ciudad_id") REFERENCES "ciudades" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_usuarios_created_by') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "fk_clientes_usuarios_created_by"
      FOREIGN KEY ("created_by") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_etapas_cliente_etapa_id') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "fk_clientes_etapas_cliente_etapa_id"
      FOREIGN KEY ("etapa_id") REFERENCES "etapas_cliente" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_paises_pais_id') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "fk_clientes_paises_pais_id"
      FOREIGN KEY ("pais_id") REFERENCES "paises" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_clientes_regiones_region_id') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "fk_clientes_regiones_region_id"
      FOREIGN KEY ("region_id") REFERENCES "regiones" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_cliente_adjuntos_clientes_cliente_id') THEN
    ALTER TABLE "cliente_adjuntos" ADD CONSTRAINT "fk_cliente_adjuntos_clientes_cliente_id"
      FOREIGN KEY ("cliente_id") REFERENCES "clientes" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_cliente_emails_clientes_cliente_id') THEN
    ALTER TABLE "cliente_emails" ADD CONSTRAINT "fk_cliente_emails_clientes_cliente_id"
      FOREIGN KEY ("cliente_id") REFERENCES "clientes" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_cliente_telefonos_clientes_cliente_id') THEN
    ALTER TABLE "cliente_telefonos" ADD CONSTRAINT "fk_cliente_telefonos_clientes_cliente_id"
      FOREIGN KEY ("cliente_id") REFERENCES "clientes" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_estados_proyecto_fases_proyecto_fase') THEN
    ALTER TABLE "estados_proyecto" ADD CONSTRAINT "fk_estados_proyecto_fases_proyecto_fase"
      FOREIGN KEY ("fase") REFERENCES "fases_proyecto" ("fase") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_historial_cambios_usuarios_usuario_id') THEN
    ALTER TABLE "historial_cambios" ADD CONSTRAINT "fk_historial_cambios_usuarios_usuario_id"
      FOREIGN KEY ("usuario_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_informes_snapshot_usuarios_created_by') THEN
    ALTER TABLE "informes_snapshot" ADD CONSTRAINT "fk_informes_snapshot_usuarios_created_by"
      FOREIGN KEY ("created_by") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_invitaciones_equipo_usuarios_invitado_por_id') THEN
    ALTER TABLE "invitaciones_equipo" ADD CONSTRAINT "fk_invitaciones_equipo_usuarios_invitado_por_id"
      FOREIGN KEY ("invitado_por_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_notificaciones_usuarios_usuario_destinatario_id') THEN
    ALTER TABLE "notificaciones" ADD CONSTRAINT "fk_notificaciones_usuarios_usuario_destinatario_id"
      FOREIGN KEY ("usuario_destinatario_id") REFERENCES "usuarios" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedores_categorias_proveedor_categoria_id') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "fk_proveedores_categorias_proveedor_categoria_id"
      FOREIGN KEY ("categoria_id") REFERENCES "categorias_proveedor" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedores_ciudades_ciudad_id') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "fk_proveedores_ciudades_ciudad_id"
      FOREIGN KEY ("ciudad_id") REFERENCES "ciudades" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedores_usuarios_created_by') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "fk_proveedores_usuarios_created_by"
      FOREIGN KEY ("created_by") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedores_paises_pais_id') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "fk_proveedores_paises_pais_id"
      FOREIGN KEY ("pais_id") REFERENCES "paises" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedores_regiones_region_id') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "fk_proveedores_regiones_region_id"
      FOREIGN KEY ("region_id") REFERENCES "regiones" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_adjuntos_proveedores_proveedor_id') THEN
    ALTER TABLE "proveedor_adjuntos" ADD CONSTRAINT "fk_proveedor_adjuntos_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_colaboradores_proveedores_proveedor_id') THEN
    ALTER TABLE "proveedor_colaboradores" ADD CONSTRAINT "fk_proveedor_colaboradores_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_colaboradores_usuarios_usuario_id') THEN
    ALTER TABLE "proveedor_colaboradores" ADD CONSTRAINT "fk_proveedor_colaboradores_usuarios_usuario_id"
      FOREIGN KEY ("usuario_id") REFERENCES "usuarios" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_emails_proveedores_proveedor_id') THEN
    ALTER TABLE "proveedor_emails" ADD CONSTRAINT "fk_proveedor_emails_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_servicios_proveedores_proveedor_id') THEN
    ALTER TABLE "proveedor_servicios" ADD CONSTRAINT "fk_proveedor_servicios_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_servicios_servicios_servicio_id') THEN
    ALTER TABLE "proveedor_servicios" ADD CONSTRAINT "fk_proveedor_servicios_servicios_servicio_id"
      FOREIGN KEY ("servicio_id") REFERENCES "servicios" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proveedor_telefonos_proveedores_proveedor_id') THEN
    ALTER TABLE "proveedor_telefonos" ADD CONSTRAINT "fk_proveedor_telefonos_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyectos_clientes_cliente_id') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "fk_proyectos_clientes_cliente_id"
      FOREIGN KEY ("cliente_id") REFERENCES "clientes" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyectos_usuarios_created_by') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "fk_proyectos_usuarios_created_by"
      FOREIGN KEY ("created_by") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyectos_estados_proyecto_estado_id') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "fk_proyectos_estados_proyecto_estado_id"
      FOREIGN KEY ("estado_id") REFERENCES "estados_proyecto" ("id") ON DELETE NO ACTION;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyectos_usuarios_gerente_id') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "fk_proyectos_usuarios_gerente_id"
      FOREIGN KEY ("gerente_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_adjuntos_proyectos_proyecto_id') THEN
    ALTER TABLE "proyecto_adjuntos" ADD CONSTRAINT "fk_proyecto_adjuntos_proyectos_proyecto_id"
      FOREIGN KEY ("proyecto_id") REFERENCES "proyectos" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_equipo_proyectos_proyecto_id') THEN
    ALTER TABLE "proyecto_equipo" ADD CONSTRAINT "fk_proyecto_equipo_proyectos_proyecto_id"
      FOREIGN KEY ("proyecto_id") REFERENCES "proyectos" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_proveedores_proveedores_proveedor_id') THEN
    ALTER TABLE "proyecto_proveedores" ADD CONSTRAINT "fk_proyecto_proveedores_proveedores_proveedor_id"
      FOREIGN KEY ("proveedor_id") REFERENCES "proveedores" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_proveedores_proyectos_proyecto_id') THEN
    ALTER TABLE "proyecto_proveedores" ADD CONSTRAINT "fk_proyecto_proveedores_proyectos_proyecto_id"
      FOREIGN KEY ("proyecto_id") REFERENCES "proyectos" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_seguimiento_usuarios_autor_id') THEN
    ALTER TABLE "proyecto_seguimiento" ADD CONSTRAINT "fk_proyecto_seguimiento_usuarios_autor_id"
      FOREIGN KEY ("autor_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_proyecto_seguimiento_proyectos_proyecto_id') THEN
    ALTER TABLE "proyecto_seguimiento" ADD CONSTRAINT "fk_proyecto_seguimiento_proyectos_proyecto_id"
      FOREIGN KEY ("proyecto_id") REFERENCES "proyectos" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_regiones_paises_pais_id') THEN
    ALTER TABLE "regiones" ADD CONSTRAINT "fk_regiones_paises_pais_id"
      FOREIGN KEY ("pais_id") REFERENCES "paises" ("id") ON DELETE CASCADE;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_solicitudes_eliminacion_usuarios_aprobado_por_gerente_id') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "fk_solicitudes_eliminacion_usuarios_aprobado_por_gerente_id"
      FOREIGN KEY ("aprobado_por_gerente_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_solicitudes_eliminacion_usuarios_gerente_responsable_id') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "fk_solicitudes_eliminacion_usuarios_gerente_responsable_id"
      FOREIGN KEY ("gerente_responsable_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_solicitudes_eliminacion_usuarios_revisado_por_id') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "fk_solicitudes_eliminacion_usuarios_revisado_por_id"
      FOREIGN KEY ("revisado_por_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_solicitudes_eliminacion_usuarios_solicitado_por_id') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "fk_solicitudes_eliminacion_usuarios_solicitado_por_id"
      FOREIGN KEY ("solicitado_por_id") REFERENCES "usuarios" ("id") ON DELETE SET NULL;
  END IF;
END $nexit_sync$;

-- ---------- CHECK CONSTRAINTS ----------
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_clientes_estado') THEN
    ALTER TABLE "clientes" ADD CONSTRAINT "ck_clientes_estado" CHECK (estado IN ('Activo', 'Prospecto', 'Inactivo'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cliente_adjuntos_tipo') THEN
    ALTER TABLE "cliente_adjuntos" ADD CONSTRAINT "ck_cliente_adjuntos_tipo" CHECK (tipo IN ('link', 'file'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_etapas_cliente_porcentaje') THEN
    ALTER TABLE "etapas_cliente" ADD CONSTRAINT "ck_etapas_cliente_porcentaje" CHECK (porcentaje_proceso BETWEEN 0 AND 100);
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_historial_cambios_tipo_entidad') THEN
    ALTER TABLE "historial_cambios" ADD CONSTRAINT "ck_historial_cambios_tipo_entidad" CHECK (tipo_entidad IN ('proyecto', 'proveedor', 'cliente'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_informes_snapshot_tipo') THEN
    ALTER TABLE "informes_snapshot" ADD CONSTRAINT "ck_informes_snapshot_tipo" CHECK (tipo IN ('semanal', 'mensual'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_invitaciones_equipo_estado') THEN
    ALTER TABLE "invitaciones_equipo" ADD CONSTRAINT "ck_invitaciones_equipo_estado" CHECK (estado IN ('Pendiente', 'Aceptada', 'Rechazada'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_notificaciones_tipo') THEN
    ALTER TABLE "notificaciones" ADD CONSTRAINT "ck_notificaciones_tipo" CHECK (tipo IN ('solicitud_eliminacion_creada', 'solicitud_eliminacion_endosada', 'solicitud_eliminacion_decidida', 'invitacion_aceptada', 'invitacion_rechazada'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proveedores_cobertura') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "ck_proveedores_cobertura" CHECK (cobertura IS NULL OR cobertura IN ('Solo ciudad', 'Regional', 'Nacional', 'Internacional'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proveedores_presupuesto') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "ck_proveedores_presupuesto" CHECK (presupuesto IS NULL OR presupuesto IN ('$ Bajo (<20k)', '$$ Medio (20k–100k)', '$$$ Alto (100k–500k)', '$$$$ Premium (>500k)'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proveedores_score') THEN
    ALTER TABLE "proveedores" ADD CONSTRAINT "ck_proveedores_score" CHECK (score IS NULL OR score BETWEEN 1 AND 5);
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proveedor_adjuntos_tipo') THEN
    ALTER TABLE "proveedor_adjuntos" ADD CONSTRAINT "ck_proveedor_adjuntos_tipo" CHECK (tipo IN ('link', 'file'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_brief') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_brief" CHECK (estado_brief IN ('Pendiente por enviar', 'Entregado, a espera de respuesta', 'Requiere ajustes', 'Aprobado'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_pago') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_pago" CHECK (NOT pagado OR fecha_pago IS NOT NULL);
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_porcentaje') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_porcentaje" CHECK (porcentaje_avance BETWEEN 0 AND 100);
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_prioridad') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_prioridad" CHECK (prioridad IS NULL OR prioridad IN ('Alta', 'Media', 'Baja'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_propuesta') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_propuesta" CHECK (propuesta_estado IN ('No enviada', 'En proceso', 'Enviada'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyectos_tipo') THEN
    ALTER TABLE "proyectos" ADD CONSTRAINT "ck_proyectos_tipo" CHECK (tipo_proyecto IS NULL OR tipo_proyecto IN ('Corporativo', 'Evento social'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyecto_adjuntos_tipo') THEN
    ALTER TABLE "proyecto_adjuntos" ADD CONSTRAINT "ck_proyecto_adjuntos_tipo" CHECK (tipo IN ('link', 'file'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyecto_equipo_rol') THEN
    ALTER TABLE "proyecto_equipo" ADD CONSTRAINT "ck_proyecto_equipo_rol" CHECK (rol IN ('Ejecutivo', 'Comercial', 'Administrativo', 'Diseñador 3D', 'Diseñador gráfico'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_proyecto_seguimiento_area') THEN
    ALTER TABLE "proyecto_seguimiento" ADD CONSTRAINT "ck_proyecto_seguimiento_area" CHECK (area IN ('General', 'Creativo', 'Comercial', 'Administrativo'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_solicitudes_eliminacion_estado') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "ck_solicitudes_eliminacion_estado" CHECK (estado IN ('pendiente_gerente', 'pendiente_admin', 'aprobada', 'rechazada'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_solicitudes_eliminacion_tipo') THEN
    ALTER TABLE "solicitudes_eliminacion" ADD CONSTRAINT "ck_solicitudes_eliminacion_tipo" CHECK (tipo_entidad IN ('cliente', 'proveedor', 'proyecto', 'usuario'));
  END IF;
END $nexit_sync$;
DO $nexit_sync$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_usuarios_rol') THEN
    ALTER TABLE "usuarios" ADD CONSTRAINT "ck_usuarios_rol" CHECK (rol IN ('super_admin', 'admin', 'manager', 'miembro'));
  END IF;
END $nexit_sync$;

COMMIT;

-- ============================================================
-- Paso final, DESPUES de confirmar que todo lo anterior corrio
-- sin errores: marcar las 16 migraciones como aplicadas, para
-- que `dotnet ef database update` no intente recrear las tablas
-- desde cero la proxima vez. Ejecutar solo despues de verificar
-- el bloque de arriba -- si se corre antes, la base queda
-- marcada como "al dia" sin estarlo de verdad.
-- ============================================================

INSERT INTO "__EFMigrationsHistory" (migration_id, product_version) VALUES
  ('20260817212311_InitialCreate', '8.0.11'),
  ('20260817213731_AddUuidDefaults', '8.0.11'),
  ('20260817213841_AddEstadoProyectoUuidDefault', '8.0.11'),
  ('20260817214414_AddLocalDatabaseRules', '8.0.11'),
  ('20260817234434_AddConcurrencyAndAuditTracking', '8.0.11'),
  ('20260818015920_AddRbacFourTierRoles', '8.0.11'),
  ('20260824004948_AddNotificacionesHistorialColaboradores', '8.0.11'),
  ('20260824015113_AddInvitacionesEquipo', '8.0.11'),
  ('20260825014718_AddAdjuntoContentTypeYTamano', '8.0.11'),
  ('20260826184014_AddPresenciaUsuarios', '8.0.11'),
  ('20260901033958_AddContrasenaConfiguradaUsuarios', '8.0.11'),
  ('20260903143021_AddClienteUbicacionCatalogoYEstado', '8.0.11'),
  ('20260908222937_AddEliminacionUsuarioPorSolicitud', '8.0.11'),
  ('20260909172315_AddEntidadNombreSolicitudEliminacion', '8.0.11'),
  ('20260910012649_AddClienteYProyectoAdjuntos', '8.0.11'),
  ('20260910022914_AddEstadosProveedorCatalogo', '8.0.11')
ON CONFLICT (migration_id) DO NOTHING;