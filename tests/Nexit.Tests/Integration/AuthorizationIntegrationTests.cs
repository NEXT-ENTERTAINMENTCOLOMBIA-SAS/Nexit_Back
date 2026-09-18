using System.Net;
using System.Net.Http.Json;

namespace Nexit.Tests.Integration;

/// <summary>
/// H8 — pruebas de integración que levantan la aplicación completa (pipeline de middleware real,
/// incluida autenticación y autorización) en vez de invocar casos de uso de forma aislada.
/// El objetivo es detectar, por ejemplo, que alguien quite un [Authorize] por accidente en un
/// refactor futuro, algo que las pruebas unitarias existentes no pueden ver.
/// </summary>
public class AuthorizationIntegrationTests(NexitApiFactory factory) : IClassFixture<NexitApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task GetClientes_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetClientes_with_any_authenticated_role_is_not_blocked_by_authorization()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");

        var response = await _client.GetAsync("/api/clientes");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCliente_without_a_token_returns_401()
    {
        var response = await _client.DeleteAsync($"/api/clientes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCliente_with_miembro_role_returns_403()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");

        var response = await _client.DeleteAsync($"/api/clientes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Manager/admin/director elimina directo desde 2026-09-09 hasta 2026-09-18, cuando Alicia corrigió
    // la decisión: solo super_admin elimina directo, todos los demás -- admin incluido -- tienen que
    // pasar por SolicitudesEliminacionController con motivo. Ver SuperAdminOnly en Program.cs.
    [Theory]
    [InlineData("manager")]
    [InlineData("admin")]
    public async Task DeleteCliente_with_a_role_below_super_admin_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.DeleteAsync($"/api/clientes/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCliente_with_super_admin_role_passes_authorization()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "super_admin");

        var response = await _client.DeleteAsync($"/api/clientes/{Guid.NewGuid()}");

        // No hay base de datos real en el entorno de pruebas, así que la petición puede fallar más
        // adelante en el pipeline (p. ej. 500 al no poder conectar a Postgres) — lo que importa aquí
        // es que la autorización ya no la bloquea (nunca 401/403).
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task CrearPais_catalogo_with_a_non_admin_role_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.PostAsJsonAsync("/api/catalogos/paises", new { nombre = "Colombia", tipoDivision = "Departamento" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Every_response_still_carries_the_security_headers_even_on_401()
    {
        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").First());
    }

    // Modelo de 4 roles (docs/06-modelo-permisos-roles.md, sección 6) — listar el directorio
    // completo es AdminOrAbove desde 2026-08-26 (antes era exclusivo de super_admin); manager y
    // miembro siguen sin poder verlo.
    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task GetUsuarios_with_manager_or_miembro_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task GetUsuarios_with_admin_or_super_admin_role_passes_authorization(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.GetAsync("/api/usuarios");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Crear/editar usuarios pasó de SuperAdminOnly a AdminOrAbove (Alicia 2026-09-09: varias
    // administradoras van a manejar usuarios ellas mismas).
    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task CrearUsuario_with_manager_or_miembro_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.PostAsJsonAsync("/api/usuarios", new { id = Guid.NewGuid(), nombre = "X", apellido = "Y", email = "x@k11technologies.com", rol = "miembro" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task CrearUsuario_with_admin_or_super_admin_role_passes_authorization(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.PostAsJsonAsync("/api/usuarios", new { id = Guid.NewGuid(), nombre = "X", apellido = "Y", email = "x@k11technologies.com", rol = "miembro" });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // GetById (perfil individual, agregado 2026-08-26) es de lectura libre para cualquier
    // autenticado -- ver el perfil de otra persona no es "gestionar usuarios" (eso sigue siendo
    // crear/editar/eliminar, exclusivo de super_admin). Cubierto a fondo (con datos reales) en
    // UsuariosFunctionalTests -- aquí solo se confirma que la autorización no lo bloquea.
    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task GetUsuarioById_with_any_authenticated_role_is_not_blocked_by_authorization(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/usuarios");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Cualquier usuario autenticado (gerente o miembro) puede pedir una eliminación — la restricción
    // está en quién la aprueba, no en quién la solicita.
    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task CrearSolicitudEliminacion_with_any_authenticated_role_is_not_blocked_by_authorization(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.PostAsJsonAsync("/api/solicitudeseliminacion", new { tipoEntidad = "cliente", entidadId = Guid.NewGuid() });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task AprobarSolicitudEliminacion_como_admin_with_a_non_admin_role_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.PutAsJsonAsync($"/api/solicitudeseliminacion/{Guid.NewGuid()}/aprobar", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AprobarGerente_endpoint_is_reachable_by_any_authenticated_role()
    {
        // La verificación de que sea EL gerente responsable ocurre dentro del caso de uso (403 vía
        // ForbiddenOperationException), no en la política estática de autorización — cualquier
        // gerente autenticado debe poder llegar al endpoint.
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "manager");

        var response = await _client.PutAsJsonAsync($"/api/solicitudeseliminacion/{Guid.NewGuid()}/aprobar-gerente", new { });

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Informes semanal/mensual (ver docs/07-calendario-e-informes-excel.md) — exclusivo de
    // super_admin/admin, igual que la gestión de usuarios pero un nivel más abajo.
    [Theory]
    [InlineData("miembro")]
    [InlineData("manager")]
    public async Task GetInformesResumen_with_a_non_admin_role_returns_403(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.GetAsync("/api/informes/resumen");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("super_admin")]
    public async Task GetInformesResumen_with_admin_or_above_passes_authorization(string role)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, role);

        var response = await _client.GetAsync("/api/informes/resumen");

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExportarInformeResumen_with_a_non_admin_role_returns_403()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");

        var response = await _client.GetAsync("/api/informes/resumen/exportar");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetInformesResumen_without_a_token_returns_401()
    {
        var response = await _client.GetAsync("/api/informes/resumen");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Eliminación automática de usuarios inactivos (docs/17-eliminacion-automatica-usuarios.md): el
    // claim user_active=false debe bloquear TODO, incluido un endpoint de solo [Authorize] sin rol
    // específico -- no solo las políticas SuperAdminOnly/AdminOrAbove. Sin esto, "desactivar" a
    // alguien sería puramente cosmético.
    [Fact]
    public async Task GetClientes_with_an_inactive_account_returns_403_even_though_the_role_would_normally_pass()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestActiveHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "admin");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestActiveHeader, "false");

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetUsuarios_with_an_inactive_super_admin_returns_403()
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestActiveHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "super_admin");
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestActiveHeader, "false");

        var response = await _client.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetClientes_with_an_active_account_is_not_blocked_by_the_active_check()
    {
        // Ausencia del claim (o explícitamente activo) sigue pasando -- no es un bloqueo por defecto.
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestAuthHeader);
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.TestActiveHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.TestAuthHeader, "miembro");

        var response = await _client.GetAsync("/api/clientes");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
