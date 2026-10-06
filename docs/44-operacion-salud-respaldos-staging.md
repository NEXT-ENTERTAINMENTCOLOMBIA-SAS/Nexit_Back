# 44 — Operación: salud, respaldos, staging y monitoreo (2026-10-05)

## 1. Chequeo de salud
| Ruta | Qué comprueba | Respuesta |
|---|---|---|
| `GET /health` | El proceso está vivo (no toca la base) | 200 `{"status":"ok"}` |
| `GET /health/ready` | Además, que la base responde (tope 5 s) | 200 si todo bien, **503** si la base falla |

Son anónimas y no usan el límite de peticiones. No devuelven claves ni cadenas de conexión.
**Cuando alguien diga "me sale error con los datos", abrir `https://<api>/health/ready` primero:** si dice `db: fail`, el problema es la conexión a la base (variables de entorno en Railway), no la pantalla.

En Railway: *Settings → Deploy → Healthcheck Path* = `/health`.

## 2. Respaldos
- **Automático:** Supabase hace respaldos diarios en planes de pago (Database → Backups). Verificar en el panel que estén activos.
- **Manual (antes de cada cambio grande de base):**
  `DATABASE_URL="postgresql://…" ./scripts/respaldo-db.sh` → crea `respaldos/nexit-FECHA.dump` (no se versiona: la carpeta `respaldos/` debe estar en `.gitignore`).
- **Prueba de restauración (hacerla al menos una vez por trimestre; un respaldo que nunca se restauró no es un respaldo):**
  `LOCAL_URL="postgresql://postgres:postgres@localhost:5432/postgres" ./scripts/probar-restauracion.sh respaldos/nexit-FECHA.dump`
  Restaura en una base temporal local, cuenta proyectos/clientes/proveedores/usuarios y la borra.
- **Restaurar de verdad (emergencia):** crear una base nueva (no pisar la actual), `pg_restore --no-owner --no-privileges --dbname=<nueva> archivo.dump`, apuntar `ConnectionStrings__Default` a la nueva y verificar con `/health/ready`.

## 3. Entorno de pruebas (staging)
Meta: probar cambios con datos de mentira antes de tocar producción.
1. **Supabase:** crear un segundo proyecto `nexit-staging` (gratis). Correr en su SQL Editor los scripts `docs/schema/*.sql` y `31`…`35` en orden. Cargar datos de prueba (nunca copiar datos reales de clientes).
2. **Railway:** duplicar el servicio del backend como `nexit-back-staging`, rama `staging`, con SUS propias variables (cadena de la base de staging, `Jwt__*` del proyecto de staging, `Sentry__Environment=staging`).
3. **Vercel:** el proyecto del front ya crea una *Preview* por cada rama; ponerle `NEXT_PUBLIC_API_URL` apuntando al backend de staging (Settings → Environment Variables → solo "Preview").
4. Flujo: rama → Preview + staging → probar → merge a `main` → producción.
Regla: **ninguna variable de producción se copia a staging** (claves, JWT, cadena de base).

## 4. Monitoreo de errores (Sentry)
- Backend: se activa solo si existe la variable `Sentry__Dsn` (opcional `Sentry__Environment`). Sin ella no hace nada. Reporta únicamente los errores 500 inesperados; sin datos personales.
- Frontend: se activa solo con `NEXT_PUBLIC_SENTRY_DSN`.
- Crear dos proyectos en sentry.io (uno .NET, uno Next.js), copiar cada DSN a Railway/Vercel y redeployar. Configurar una alerta por correo "nuevo error".

## 5. Rendimiento (cambios de esta fecha)
- Respuestas comprimidas (Brotli/Gzip): los listados bajan ~80–90 %.
- `GET api/proyectos` ya no trae la bitácora de seguimiento (solo la necesita el cálculo de prioridad).
- `GET api/proyectos/pagina?page&pageSize&q&estadoId&clienteId&tipo&gerenteId`: filtra y pagina en la base; devuelve también los conteos de las tarjetas.
- Roles y listas de Configuración se guardan 60 s en memoria (se invalidan al editar).
- `35_indices_rendimiento.sql`: índices para búsqueda y filtros.

## 6. Orden de despliegue
1. Respaldo manual. 2. Correr `34_dinero_numerico.sql` y `35_indices_rendimiento.sql` en Supabase. 3. Merge del backend a `main`. 4. Verificar `/health/ready`. 5. Merge del front.
