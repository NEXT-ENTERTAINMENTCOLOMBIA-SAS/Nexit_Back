using System.Net;
using Nexit.Tests.Integration;

namespace Nexit.Tests.Security;

/// <summary>
/// Prueba de "consumo de recursos" (OWASP API4:2023 -- Unrestricted Resource Consumption, ver
/// docs/08-tipos-de-pruebas.md): comprueba que el limitador de peticiones configurado en Program.cs
/// (100 peticiones/minuto por usuario, hallazgo H7) realmente bloquea con 429 después del límite, en
/// vez de solo confiar en que la configuración "se ve bien" en el código. Usa un endpoint que rechaza
/// por autorización (403) antes de tocar la base de datos -- el limitador corre ANTES que la
/// autorización en el pipeline (Program.cs: UseAuthentication → UseRateLimiter → UseAuthorization), así
/// que cuenta igual sin necesitar Postgres real, y por eso esta prueba vive en el nivel de integración
/// (sin base de datos) y no en el funcional.
/// </summary>
public class RateLimitingIntegrationTests(NexitApiFactory factory) : IClassFixture<NexitApiFactory>
{
    [Fact]
    public async Task Pasado_el_limite_configurado_el_servidor_responde_429_para_el_mismo_usuario()
    {
        var client = factory.CreateClient();
        var userId = Guid.NewGuid().ToString();
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");
        client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, userId);

        // Límite por defecto: 100/minuto por usuario (RateLimiting:PermitLimit en Program.cs). Se pide
        // un endpoint de solo-administradores con un rol sin permiso -- siempre 403, nunca toca la base
        // de datos, así que se puede golpear 100+ veces rápido y sin flakiness por infraestructura.
        var estados = new List<HttpStatusCode>();
        for (var i = 0; i < 105; i++)
        {
            var respuesta = await client.GetAsync("/api/configuracion/dominios-correo");
            estados.Add(respuesta.StatusCode);
        }

        Assert.All(estados.Take(100), s => Assert.Equal(HttpStatusCode.Forbidden, s));
        Assert.Contains((HttpStatusCode)429, estados);
    }

    [Fact]
    public async Task Dos_usuarios_distintos_no_comparten_el_mismo_cupo()
    {
        // El limitador particiona por usuario autenticado (Program.cs), justamente para que varias
        // personas de la misma oficina no se bloqueen entre sí (hallazgo H7 original) -- se confirma
        // aquí que agotar el cupo de un usuario no afecta para nada al de otro.
        var clienteA = factory.CreateClient();
        clienteA.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");
        clienteA.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
        for (var i = 0; i < 100; i++) await clienteA.GetAsync("/api/configuracion/dominios-correo");
        var agotado = await clienteA.GetAsync("/api/configuracion/dominios-correo");
        Assert.Equal((HttpStatusCode)429, agotado.StatusCode);

        var clienteB = factory.CreateClient();
        clienteB.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");
        clienteB.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
        var primeraDeB = await clienteB.GetAsync("/api/configuracion/dominios-correo");
        Assert.Equal(HttpStatusCode.Forbidden, primeraDeB.StatusCode); // 403 por rol, no 429 -- su cupo sigue intacto.
    }
}
