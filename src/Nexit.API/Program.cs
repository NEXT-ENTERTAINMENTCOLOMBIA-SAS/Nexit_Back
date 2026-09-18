using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.RateLimiting;
using Nexit.API.Filters;
using Nexit.API.Middleware;
using Nexit.Application;
using Nexit.Infrastructure;
using Serilog;
using System.Threading.RateLimiting;

Log.Logger = new LoggerConfiguration().WriteTo.Console().WriteTo.File("logs/nexit-.log", rollingInterval: RollingInterval.Day).CreateLogger();
try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();
    builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 10 * 1024 * 1024);
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // JWT de Supabase Auth: hay dos modos posibles según cómo esté configurado el proyecto de Supabase
    // (Settings → API → JWT Keys). El modo recomendado es el de claves asimétricas (Authority/JWKS,
    // descubrimiento automático); el modo heredado usa un secreto compartido (HS256) y solo debe usarse
    // si el proyecto de Supabase todavía no fue migrado. Ver docs/05-plan-remediacion-seguridad.md (H1).
    var jwtAuthority = builder.Configuration["Jwt:Authority"];
    var jwtLegacySharedSecret = builder.Configuration["Jwt:LegacySharedSecret"];
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.Audience = builder.Configuration["Jwt:Audience"];
        var validateIssuer = builder.Configuration.GetValue("Jwt:ValidateIssuer", true);
        var validateAudience = builder.Configuration.GetValue("Jwt:ValidateAudience", true);
        if (!string.IsNullOrWhiteSpace(jwtLegacySharedSecret))
        {
            // Modo heredado: el proyecto Supabase todavía firma con el secreto compartido (HS256).
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = validateIssuer,
                ValidIssuer = jwtAuthority,
                ValidateAudience = validateAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtLegacySharedSecret))
            };
        }
        else
        {
            // Modo recomendado: claves de firma asimétricas de Supabase, descubiertas vía JWKS/OIDC.
            options.Authority = jwtAuthority;
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = validateIssuer, ValidateAudience = validateAudience, ValidateLifetime = true };
        }
    });
    // Modelo de 4 roles (super_admin > admin > manager > miembro) — ver docs/06-modelo-permisos-roles.md.
    // El claim de rol (app_role/user_role) lo agrega el Auth Hook de Supabase — ver docs/schema/03_auth_hook_custom_claims.sql.
    // "SuperAdminOnly": únicamente la super administradora crea/edita/elimina usuarios (ver el
    // directorio o un perfil individual ya NO es exclusivo de este rol -- ver UsuariosController).
    // "AdminOrAbove": administración directa de catálogos, eliminación directa (sin pasar por una
    // solicitud) de clientes/proveedores/proyectos/adjuntos — gerentes y miembros pasan por el flujo
    // de solicitudes de eliminación (SolicitudesEliminacionController) en vez de este atajo -- y (desde
    // 2026-08-26) listar el directorio completo de usuarios.
    bool HasRole(System.Security.Claims.ClaimsPrincipal user, string role) =>
        user.IsInRole(role) || user.HasClaim("app_role", role) || user.HasClaim("user_role", role);
    // Eliminación automática de usuarios inactivos (ver docs/17-eliminacion-automatica-usuarios.md):
    // el Auth Hook agrega el claim "user_active"="false" únicamente cuando usuarios.activo es false
    // -- la AUSENCIA del claim se trata como activa (fail-open), a propósito, para no invalidar de
    // golpe todos los tokens ya emitidos antes de desplegar este cambio; se cierra solo, en cuanto
    // esos tokens vencen y se renuevan (hasta ~1 hora). Una cuenta recién desactivada conserva
    // acceso durante el resto de la vida de su token actual -- es la única ventana conocida.
    bool IsActive(System.Security.Claims.ClaimsPrincipal user) => !user.HasClaim("user_active", "false");
    builder.Services.AddAuthorization(options =>
    {
        options.DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(context => IsActive(context.User)).Build();
        options.AddPolicy("SuperAdminOnly", policy => policy.RequireAuthenticatedUser().RequireAssertion(context => IsActive(context.User) && HasRole(context.User, "super_admin")));
        options.AddPolicy("AdminOrAbove", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            IsActive(context.User) && (HasRole(context.User, "admin") || HasRole(context.User, "super_admin"))));
        // Director (manager) también puede eliminar clientes/proveedores/proyectos directamente
        // (Alicia 2026-09-09) -- a diferencia de miembro, que sigue teniendo que pasar por
        // SolicitudesEliminacionController. Ver ClientesController/ProveedoresController/
        // ProyectosController.Delete: el administrador recibe una notificación cuando un director
        // usa este poder (ver NotificacionFactory.EliminacionDirectaDirector).
        options.AddPolicy("DirectorOrAbove", policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
            IsActive(context.User) && (HasRole(context.User, "manager") || HasRole(context.User, "admin") || HasRole(context.User, "super_admin"))));
    });
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    if (allowedOrigins.Length == 0 && !builder.Environment.IsDevelopment()) throw new InvalidOperationException("Configure Cors:AllowedOrigins for production.");
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy.WithOrigins(allowedOrigins.Length > 0 ? allowedOrigins : ["http://localhost:3000", "http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));

    // Cabeceras reenviadas por el proxy inverso del proveedor de despliegue (Railway, decidido 2026-09-10). Sin
    // esto, el rate limiting y cualquier log de IP verían solo la IP interna del proxy. ForwardedHeaders:KnownNetworks
    // ya queda en 100.0.0.0/8 en appsettings.Production.json (rango interno del edge de Railway para
    // X-Forwarded-For, confirmado en station.railway.com) — confiar en encabezados reenviados sin saber quién los
    // manda permitiría falsificar la IP de origen, por eso no se deja abierto a cualquier proxy.
    var forwardedHeadersOptions = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
    foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        if (System.Net.IPAddress.TryParse(proxy, out var proxyIp)) forwardedHeadersOptions.KnownProxies.Add(proxyIp);
    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var networkIp) && int.TryParse(parts[1], out var prefixLength))
            forwardedHeadersOptions.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(networkIp, prefixLength));
    }

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        // Todo endpoint de esta API exige autenticación (BaseController lleva [Authorize]), así que se puede
        // limitar por usuario en vez de por IP: evita que 20-25 personas trabajando desde la misma oficina
        // (misma IP pública) se bloqueen entre sí como si fueran un solo cliente (hallazgo H7). Si por alguna
        // razón la petición todavía no está autenticada (token ausente/ inválido), se cae de vuelta a la IP.
        options.AddPolicy("api", context =>
        {
            var userId = context.User.FindFirst("sub")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var partitionKey = userId is not null ? $"user:{userId}" : $"ip:{context.Connection.RemoteIpAddress}";
            return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
                _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:PermitLimit", 100), Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
        });
        // GET /api/auth/estado-cuenta (docs/30) es el único endpoint público (sin sesión) de toda
        // la API -- por eso no puede usar la política "api" de arriba (esa parte por usuario
        // autenticado, y aquí nunca hay uno). Se limita por IP, mucho más estricto que el fallback
        // por IP de "api" (100/min): es un endpoint que, por su naturaleza, confirma o descarta
        // correos, así que un límite generoso lo dejaría abierto a enumerar cuentas de la
        // organización probando una por una.
        options.AddPolicy("auth-anon", context =>
            RateLimitPartition.GetFixedWindowLimiter($"auth-anon:{context.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthAnonPermitLimit", 8), Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
    });
    // Filtro global: nadie sin perfil de negocio (o con la cuenta desactivada) pasa de acá, salvo
    // los endpoints marcados con [PermitirSinPerfil] -- ver Filters/PerfilRequeridoFilter.cs.
    builder.Services.AddControllers(options => options.Filters.Add<PerfilRequeridoFilter>());
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexit API", Version = "v1", Description = "API REST para la gestión operativa de Nexit." });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Name = "Authorization", In = ParameterLocation.Header, Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, [] } });
    });
    var app = builder.Build();
    app.UseForwardedHeaders(forwardedHeadersOptions);
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseCors();
    // La autenticación va antes que el rate limiter a propósito: así el partitioner de arriba
    // puede leer el usuario autenticado (context.User) y limitar por persona, no solo por IP.
    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();
    app.MapControllers().RequireRateLimiting("api");
    app.Run();
}
catch (Exception ex) { Log.Fatal(ex, "Nexit API terminated unexpectedly"); }
finally { Log.CloseAndFlush(); }

// Clase parcial pública requerida por WebApplicationFactory<Program> en las pruebas de integración
// (Nexit.Tests/Integration/*) — con top-level statements, la clase "Program" generada es internal
// por defecto y el proyecto de pruebas no puede referenciarla sin esto.
public partial class Program;
