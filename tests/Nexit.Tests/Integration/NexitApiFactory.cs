using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Data;
using Nexit.Infrastructure.Repositories;

namespace Nexit.Tests.Integration;

/// <summary>
/// Levanta la aplicación completa (pipeline de middleware real, incluida la autorización) para las
/// pruebas H8, reemplazando únicamente la autenticación JWT de Supabase por <see cref="TestAuthHandler"/>.
/// Todo lo demás (políticas de autorización, orden del pipeline, rate limiting, etc.) es el mismo código
/// que corre en producción.
/// </summary>
public class NexitApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // 2026-09-21: ver SiemprePerfilActivoUsuarioRepository -- TestAuthHandler no siembra fila
            // en `usuarios`, así que sin esto PerfilRequeridoFilter rechazaría con 403 todas las
            // peticiones autenticadas de esta factory (última registración de IUsuarioRepository gana).
            services.AddScoped<IUsuarioRepository>(sp =>
                new SiemprePerfilActivoUsuarioRepository(new UsuarioRepository(sp.GetRequiredService<NexitDbContext>())));
        });
    }
}
