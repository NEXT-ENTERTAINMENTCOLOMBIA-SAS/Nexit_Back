# Contexto del proyecto -- Nexit Backend

> Documento vivo: describe el estado REAL y verificado del sistema, no el
> historial de como se llego hasta aqui. Si algo de aqui deja de ser cierto,
> se corrige este archivo en el mismo cambio que lo vuelve falso -- no se
> agrega una nota aparte diciendo "esto cambio". Los ~40 archivos numerados
> en `docs/` (01-analisis-fase1.md, 02-..., etc.) son la bitacora historica
> de decisiones de diseno; sirven para entender *por que* se tomo una
> decision, pero no se asuma que describen el estado actual del codigo --
> para eso esta este archivo.
>
> Ultima verificacion completa: 2026-09-23.

## Que es Nexit

Sistema de gestion de negocio (clientes, proveedores, proyectos) para K11
Technologies. Backend API REST + frontend web, con autenticacion y base de
datos en Supabase.

## Stack y topologia de despliegue

| Componente | Tecnologia | Donde corre |
|---|---|---|
| Backend | .NET 8, Clean Architecture, EF Core (Npgsql) | Railway (Docker, `ASPNETCORE_URLS=http://+:8080`) |
| Frontend | Next.js 16 | Vercel |
| Base de datos | PostgreSQL | Supabase |
| Auth | Supabase Auth (JWT) | Supabase |
| Storage de adjuntos | Supabase Storage | Supabase |

El `Dockerfile` es multi-stage (build con SDK completo, runtime final solo
con ASP.NET) y funciona en cualquier plataforma que despliegue un
Dockerfile (Railway, Render, Fly.io, DO App Platform).

CI: `.github/workflows/ci.yml` corre en cada push/PR a `main` --
`dotnet build` + `dotnet test` (incluye pruebas funcionales que levantan
Postgres real con Testcontainers) + auditoria de paquetes NuGet
vulnerables. No hay deploy automatico configurado en el workflow; el
despliegue a Railway/Vercel se dispara aparte.

## Arquitectura (Clean Architecture, 4 capas)

- `Nexit.Core` -- entidades de dominio, constantes (`Roles.cs`), interfaces
  de repositorios. No depende de nada mas.
- `Nexit.Application` -- DTOs, validadores (FluentValidation), mapeo
  (AutoMapper), casos de uso (un caso de uso = una operacion de negocio).
- `Nexit.Infrastructure` -- implementacion de EF Core, migraciones,
  repositorios concretos, servicios de infraestructura (correo, storage,
  background services).
- `Nexit.API` -- controladores HTTP, autenticacion JWT (emitido por
  Supabase), politicas de autorizacion, Swagger, middlewares.

## Modelo de datos y estado real de produccion

El `NexitDbContextModelSnapshot.cs` es la fuente de verdad del esquema
deseado (16 migraciones de EF Core acumuladas). La base de produccion en
Supabase **no se creo originalmente con `dotnet ef database update`**
-- se armo por otra via (muy probablemente SQL manual en el editor de
Supabase) y quedo con el mismo esquema de tablas/columnas pero sin las
foreign keys, check constraints ni el registro en `__EFMigrationsHistory`
que EF Core espera.

Estado verificado al 2026-09-22/23 (ver `docs/schema/29_sincronizar_fk_check_constraints_produccion.sql`,
ya corrido exitosamente contra produccion):

- 41 foreign keys + 23 check constraints del modelo de EF ya estan
  aplicadas en `public`.
- Hay una FK adicional, preexistente y esperada, que no maneja EF:
  `fk_usuarios_auth_users` (`usuarios.id -> auth.users.id ON DELETE CASCADE`),
  el enlace nativo de Supabase Auth.
- `__EFMigrationsHistory` tiene las 16 migraciones registradas.
- Row Level Security (RLS) esta activo en las 33 tablas de `public`, con
  ~30 politicas reales en `pg_policies`. El rol de la API (`nexit_app`) NO
  tiene `rolbypassrls` -- RLS aplica de verdad incluso a las consultas del
  backend, no solo a accesos directos desde el navegador.

Si se necesita volver a sincronizar (por ejemplo tras un cambio manual en
Supabase), el script `docs/schema/29_...sql` es idempotente (usa
`IF NOT EXISTS` / `ON CONFLICT DO NOTHING`) y se puede re-ejecutar sin
riesgo.

`docs/schema/*.sql` esta numerado en el orden en que se fue aplicando a
produccion; el numero mas alto es el cambio mas reciente conocido.

## Roles y autorizacion

4 roles de negocio, de mayor a menor privilegio (`Roles.cs`,
`ck_usuarios_rol`, ENUM `rol_usuario`):

1. `super_admin` -- una sola cuenta, sembrada directo en la base. **Nunca**
   asignable desde la app (`Roles.Asignables` la excluye a proposito,
   decision de Alicia 2026-09-08). Unica excepcion: puede editarse a si
   misma sin perder el rol.
2. `admin`
3. `manager`
4. `miembro`

Politicas de autorizacion (`Program.cs`):

- `DefaultPolicy` -- usuario autenticado y activo.
- `SuperAdminOnly` -- solo `super_admin`.
- `AdminOrAbove` -- `admin` o `super_admin`.

Eliminar clientes/proveedores/proyectos es `SuperAdminOnly` (decision de
Alicia, 2026-09-18 -- corrigio una version anterior donde manager/admin
tambien podian eliminar directo). Cualquier otro rol pasa por
`SolicitudesEliminacionController` (flujo de solicitud + aprobacion). La
politica `DirectorOrAbove` que existia para el esquema viejo ya no existe
en el codigo (se elimino 2026-09-22 junto con las ramas muertas que
dependian de ella en los casos de uso de eliminar).

Matriz completa de permisos por endpoint: `docs/06-modelo-permisos-roles.md`
(verificar que siga vigente antes de confiar en ella para algo nuevo).

## Convenciones y gotchas conocidos

- Nombres de columnas y constraints en la base son `snake_case`, forzado
  via Fluent API (`HasColumnName`, `HasConstraintName`) en el `DbContext`
  -- nunca escribir SQL con nombres en otro formato.
- Dollar-quoting en SQL crudo (`DO $$ ... $$`): si el contenido incluye un
  `$$` literal (por ejemplo un string con ese texto), colisiona con el tag
  de apertura/cierre. Usar una etiqueta unica (`$mi_tag$`) siempre que el
  contenido no este 100% controlado.
- Este repo se trabaja con Supabase en produccion conectado incluso en
  desarrollo local en algunos flujos -- cualquier script SQL nuevo debe
  probarse de forma idempotente y de solo-lectura antes de aplicarse.

## Pendiente conocido

- Los ~40 documentos numerados en `docs/` y varios scripts en
  `docs/schema/` son historial de decisiones; falta una pasada para
  archivar los que ya quedaron completamente obsoletos (pendiente,
  Alicia decide cuales).
