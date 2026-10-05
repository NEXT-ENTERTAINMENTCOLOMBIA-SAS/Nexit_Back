# 43 - Configuración editable y panel de Project Managers (2026-10-05)

## Qué cambió
- **Se quitó el módulo Informes**: `InformesController`, repositorio, exportador a Excel, casos de uso, DTOs y sus pruebas. Ya no hay capturas, exportes ni informes semanales guardados. En el frontend, `/informe` redirige a `/project-managers`.
- **Panel de Project Managers**: `GET api/panel/project-managers` (admin o super_admin). Agrupa los proyectos por `Proyecto.GerenteId` y devuelve, por cada PM: proyectos, personas distintas (`ProyectoEquipo.Nombre`), clientes y proveedores. Los proyectos sin PM salen en una tarjeta "Sin Project Manager".
- **Configuración editable** (`api/configuracion/...`):
  - Roles: nombre y descripción editables (`roles_config`). Las claves técnicas `super_admin/admin/manager/miembro` NO cambian: las políticas, el CHECK de `usuarios.rol` y el Auth Hook dependen de ellas.
  - Listas de proyecto (`opciones_config`): tipo, prioridad, sede, estado de la propuesta y área de seguimiento. Renombrar un valor actualiza los proyectos que ya lo usan. Valores protegidos (no se renombran ni eliminan): prioridad Alta/Media, estado de propuesta "No enviada", área "General" (el cálculo de prioridad y los valores por defecto dependen de ellos).
  - Dominios de correo permitidos: solo super_admin.
  - Se quitaron los 4 CHECK de esas listas; la validación pasó a FluentValidation (texto libre, máx. 100).

## Cómo desplegar
1. Supabase, SQL Editor: ejecutar `schema/32_diagnostico_produccion_solo_lectura.sql` (solo lee) y revisar el resultado.
2. Ejecutar `schema/31_configuracion_editable.sql` (idempotente; crea tablas con RLS y política `solo_nexit_app`, siembra valores y registra la migración `20261005210309_ConfiguracionEditable`).
3. Desplegar backend (Railway) y frontend (Vercel). Verificar que `Cors` en Railway incluya el dominio de Vercel.
