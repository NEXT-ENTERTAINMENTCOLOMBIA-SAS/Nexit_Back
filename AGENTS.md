# Nexit Backend -- guia para agentes de IA

API REST en .NET 8 / Clean Architecture para clientes, proveedores y
proyectos de K11 Technologies. Antes de tocar codigo, lee
`docs/CONTEXTO-PROYECTO.md` (arquitectura, roles, estado real de la base
de produccion) -- este archivo es solo lo operativo.

## Comandos

- Restaurar: `dotnet restore Nexit.sln`
- Compilar: `dotnet build Nexit.sln -c Release`
- Pruebas: `dotnet test Nexit.sln -c Release` (unitarias + integracion +
  funcionales con Testcontainers/Docker -- ver `.github/workflows/ci.yml`)
- Migracion local: `dotnet ef database update --project src/Nexit.Infrastructure --startup-project src/Nexit.Infrastructure`
- Correr local: `dotnet run --project src/Nexit.API`

## Estructura

- `src/Nexit.Core` -- entidades, constantes (`Roles.cs`), interfaces de dominio.
- `src/Nexit.Application` -- DTOs, validadores, mapeo, casos de uso.
- `src/Nexit.Infrastructure` -- EF Core, migraciones, repositorios.
- `src/Nexit.API` -- controladores, JWT/politicas de autorizacion, Swagger.
- `tests/Nexit.Tests` -- unitarias, integracion y funcionales.
- `docs/schema/*.sql` -- scripts SQL aplicados a producción (numerados en
  orden de aplicacion; el ultimo es el estado mas reciente conocido).

## Convenciones

- Nombres de columnas/constraints en la base: `snake_case`, configurado via
  Fluent API (`HasColumnName`, `HasConstraintName`) -- nunca a mano en el DbContext.
- Un `DO $$ ... $$` en SQL crudo puede colisionar si el contenido incluye un
  `$$` literal (paso en `docs/schema/29_...sql`); usa una etiqueta unica
  (`$tag$`) en vez de `$$` pelado.
- `Roles.SuperAdmin` nunca debe quedar en `Roles.Asignables`: es una sola
  cuenta, sembrada directo en la base, no invitable/asignable desde la app.

## Limites (no hacer sin confirmar con Alicia)

- No corras SQL directo contra produccion sin que el script sea idempotente
  (`IF NOT EXISTS`, `ON CONFLICT DO NOTHING`) y sin verificacion de solo
  lectura antes (huerfanos, violaciones de CHECK).
- No commitees `appsettings.Production.json`, `.env`, ni nada con
  credenciales/connection strings reales.
- Este entorno de trabajo (sandbox) no tiene `dotnet` instalado y no puede
  hacer `git push` -- Alicia compila/prueba y sube los cambios.

Detalle completo de arquitectura, modelo de datos, roles y estado de
produccion: `docs/CONTEXTO-PROYECTO.md`. Matriz de permisos:
`docs/06-modelo-permisos-roles.md`.
