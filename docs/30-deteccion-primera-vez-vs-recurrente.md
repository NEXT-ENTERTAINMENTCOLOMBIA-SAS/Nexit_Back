# Detección de "primera vez" vs. "recurrente" en el login

Hasta ahora la pantalla de login (`docs/10`, sección 2.2) le pedía a la propia persona que
decidiera manualmente si era su primera vez ("Enviar código") o si ya tenía contraseña ("¿Ya
tienes contraseña? Inicia sesión") -- una decisión deliberada en su momento, documentada así en
`docs/10` línea 47. Esto reemplaza esa decisión manual por una automática, sin tocar el resto del
flujo de autenticación (que sigue siendo 100% frontend↔Supabase, `docs/10` sección 2.3).

## Por qué no se puede preguntarle esto a Supabase directamente

La Admin API de Supabase Auth (la única forma de consultar cuentas desde el backend, con la
`ServiceRoleKey` que ya usa `SupabaseAuthAdminService` para invitar/eliminar cuentas -- `docs/17`,
`docs/25`) no expone si una cuenta tiene o no una contraseña configurada. `listUsers`/`getUserById`
devuelven metadata del usuario (fechas de confirmación, identidades, etc.), pero nunca el estado de
la contraseña -- ni siquiera un booleano, y mucho menos el hash. No hay ningún endpoint de Supabase
que responda esa pregunta.

## La solución: una marca propia, puesta por el propio frontend

En vez de preguntarle a Supabase, `usuarios` ahora tiene una columna propia,
`contrasena_configurada` (boolean, `false` por defecto -- ver `docs/schema/12`), que Nexit_Front
marca en `true` la primera vez que alguien termina de crear o restablecer su contraseña **dentro
de Nexit** (justo después de que `supabase.auth.updateUser({ password })` responde sin error, tanto
en el flujo de "crear contraseña" como en el de "recuperar contraseña").

Esto es una aproximación, no una fuente de verdad perfecta: si alguien llegara a establecer su
contraseña completamente fuera de Nexit_Front sin pasar nunca por esos dos flujos, la marca nunca
se pondría y el login la seguiría tratando como "primera vez" -- no es un error grave, solo hace
que esa persona vea una vez más la pantalla de código en vez de la de contraseña directamente. Por
eso el login conserva un enlace manual de respaldo en ambas direcciones (ver "Qué se conserva del
diseño anterior" más abajo).

## Endpoints nuevos

| Endpoint | Quién | Qué hace |
|---|---|---|
| `GET /api/auth/estado-cuenta?email=...` | **Público, sin sesión** | `{ "tieneContrasena": true\|false }` -- nunca distingue "no existe" de "existe pero sin contraseña aún" |
| `POST /api/auth/confirmar-contrasena` | Cualquier autenticado | Marca `contrasena_configurada = true` para quien llama (best-effort, sin error si aún no tiene fila en `usuarios`) |

`GET /api/auth/estado-cuenta` es, a propósito, **el único endpoint público de toda la API** --
`BaseController` exige `[Authorize]` a nivel de clase; esta acción lo anula explícitamente con
`[AllowAnonymous]`. La prueba de seguridad que antes garantizaba "ningún controlador ni acción
tiene `AllowAnonymous`" (`ControllersRequierenAutorizacionTests`) se actualizó a propósito para
permitir esta única excepción, documentada ahí mismo -- no se debilitó en general.

## Por qué la respuesta nunca revela si el correo existe

Mismo principio ya establecido en `docs/12` (HU-04, recuperación de contraseña): un correo que no
existe y un correo que existe pero aún no tiene contraseña dan **exactamente la misma respuesta**
(`tieneContrasena: false`). Ninguno de los dos casos le dice a quien pregunta "sí, ese correo es de
alguien de la organización" -- evita que el endpoint sirva para enumerar cuentas reales probando
direcciones una por una.

## Límite de tasa propio, más estricto

Por ser el único endpoint sin sesión, no puede usar la política de límite de tasa por defecto
("api" en `Program.cs`, que parte por usuario autenticado y cae a IP solo como respaldo -- pensada
para 100 peticiones/minuto de gente que ya inició sesión). `estado-cuenta` usa una política nueva,
`"auth-anon"`, por IP siempre, con un límite mucho más bajo (`RateLimiting:AuthAnonPermitLimit`,
8/minuto por defecto) -- suficiente para uso normal (una persona escribiendo su correo una vez),
demasiado bajo para automatizar una enumeración seria.

## Qué se conserva del diseño anterior

- El enlace manual **"¿Ya tienes contraseña? Inicia sesión"** se mantiene en el paso de correo
  (visible mientras se resuelve la consulta y como respaldo si la detección se equivoca).
- La pantalla de contraseña conserva un enlace equivalente en la otra dirección
  ("¿Prefieres iniciar con código?") por si alguien fue clasificado como "recurrente" mostrando la
  marca `contrasena_configurada` en un estado que no corresponde a la realidad de su cuenta en
  Supabase.
- El correo recordado (`nexit_last_email`, ya existente) se sigue usando para prellenar el campo, y
  ahora además dispara la consulta de `estado-cuenta` automáticamente al cargar la pantalla, para
  que alguien recurrente con el correo ya recordado vea directamente el paso de contraseña sin
  tener que escribir nada.

## Verificación

`dotnet build` limpio (0 errores/advertencias) y suite de pruebas completa: 252 de 269 pasan: las
17 que no corren en este entorno son, sin excepción, las mismas de siempre que dependen de Docker
(Testcontainers contra Postgres real -- ver `docs/25`, "el mismo grupo de siempre"), confirmado
comparando los nombres de prueba fallidos uno por uno contra ese patrón conocido, no solo por el
conteo. Las 74 pruebas de `Security`/`Auth` (incluida la actualizada
`Ningun_controlador_ni_accion_tiene_AllowAnonymous`) pasan en su totalidad.

## Actualizacion 2026-10-23: la marca tambien vive en Supabase

`contrasena_configurada` solo se puede poner cuando ya existe la fila en `usuarios`. Quien creaba su
contrasena en `/login` antes de registrarse quedaba sin marca y `/registro` se la volvia a pedir. Nexit_Front
ahora guarda ademas `user_metadata.password_set = true` en Supabase en el mismo `updateUser` que crea la
contrasena, y la usa para no pedirla dos veces; cuando el perfil ya existe vuelve a llamar a
`POST /api/auth/confirmar-contrasena`. El backend no cambio. Detalle en `Nexit_Front/docs/ESTADO-ACTUAL.md`.
