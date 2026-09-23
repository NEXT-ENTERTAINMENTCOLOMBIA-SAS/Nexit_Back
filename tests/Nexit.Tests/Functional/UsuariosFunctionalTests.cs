using System.Net;
using System.Net.Http.Json;
using Nexit.Application.DTOs.Usuarios;

namespace Nexit.Tests.Functional;

/// <summary>
/// Pruebas funcionales de la validación de dominio de correo al registrar el perfil de negocio de
/// un usuario (ver docs/09-crear-proyecto-supabase-paso-a-paso.md) -- el respaldo de aplicación al
/// trigger de Postgres check_usuario_dominio_correo.
/// </summary>
public class UsuariosFunctionalTests(NexitFunctionalApiFactory factory) : FunctionalTestBase(factory)
{
    [Fact]
    public async Task No_se_puede_registrar_un_usuario_con_correo_de_dominio_no_permitido()
    {
        var client = ClientAs("super_admin");

        var response = await client.PostAsJsonAsync("/api/usuarios", new CreateUsuarioDto
        {
            Id = Guid.NewGuid(),
            Nombre = "Persona",
            Apellido = "Externa",
            Email = $"{Guid.NewGuid():N}@dominio-no-permitido.com",
            Rol = "miembro"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Si_se_puede_registrar_un_usuario_con_correo_de_dominio_permitido()
    {
        var client = ClientAs("super_admin");

        var response = await client.PostAsJsonAsync("/api/usuarios", new CreateUsuarioDto
        {
            Id = Guid.NewGuid(),
            Nombre = "Persona",
            Apellido = "Del equipo",
            Email = $"{Guid.NewGuid():N}@nexit-test.com",
            Rol = "miembro"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // --- GET /api/usuarios/me (agregado 2026-08-26): cualquier autenticado ve su propio perfil,
    // no solo super_admin -- antes esto era imposible porque toda la clase exigía SuperAdminOnly.

    [Fact]
    public async Task Un_miembro_puede_ver_su_propio_perfil_con_me()
    {
        var client = ClientAs("miembro");

        var response = await client.GetAsync("/api/usuarios/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var perfil = await response.Content.ReadFromJsonAsync<UsuarioResponseDto>();
        Assert.Equal(UsuarioSembradoId("miembro"), perfil!.Id);
    }

    [Fact]
    public async Task Un_gerente_puede_ver_su_propio_perfil_con_me()
    {
        var client = ClientAs("manager");

        var response = await client.GetAsync("/api/usuarios/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var perfil = await response.Content.ReadFromJsonAsync<UsuarioResponseDto>();
        Assert.Equal(UsuarioSembradoId("manager"), perfil!.Id);
    }

    [Fact]
    public async Task Un_miembro_no_puede_listar_todos_los_usuarios()
    {
        var client = ClientAs("miembro");

        var response = await client.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Ampliado 2026-08-26: la usuaria pidió que un administrador (no solo super_admin) pueda ver
    // el directorio completo, y que cualquiera pueda mirar (sin editar) el perfil de otra persona,
    // como el directorio de Microsoft Teams -- ver docs/06, sección 6.

    [Fact]
    public async Task Un_administrador_puede_listar_todos_los_usuarios()
    {
        var client = ClientAs("admin");

        var response = await client.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lista = await response.Content.ReadFromJsonAsync<List<UsuarioResponseDto>>();
        Assert.NotEmpty(lista!);
    }

    [Fact]
    public async Task Un_miembro_puede_ver_el_perfil_de_otra_persona_por_id_pero_solo_para_mirar()
    {
        var client = ClientAs("miembro");
        var otroId = UsuarioSembradoId("admin");

        var response = await client.GetAsync($"/api/usuarios/{otroId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var perfil = await response.Content.ReadFromJsonAsync<UsuarioResponseDto>();
        Assert.Equal(otroId, perfil!.Id);
    }

    [Fact]
    public async Task Un_miembro_no_puede_editar_el_perfil_de_otra_persona()
    {
        var client = ClientAs("miembro");
        var otroId = UsuarioSembradoId("admin");

        var response = await client.PutAsJsonAsync($"/api/usuarios/{otroId}", new UpdateUsuarioDto
        {
            Nombre = "Intento",
            Apellido = "De edición",
            Rol = "admin",
            Activo = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // --- Ya NO existe DELETE /api/usuarios/{id} (2026-09-08, ver el comentario al final de
    // UsuariosController): eliminar a alguien pasa por SolicitudesEliminacionController. Antes había
    // acá dos pruebas ("un miembro/administrador no puede eliminar a otro usuario") que esperaban 403
    // sobre esa ruta -- ahora responde 405 sin importar el rol, así que probar eso ya no dice nada
    // sobre permisos; se quitaron en vez de dejarlas comprobando un detalle de enrutamiento.

    // --- Actualizado 2026-09-09 (Alicia: "van a haber varias administradoras que necesitan manejar
    // usuarios ellas mismas, ya no exclusivo de super_admin"): crear/editar pasó de SuperAdminOnly a
    // AdminOrAbove -- un admin ya puede hacer ambas cosas. Lo único que sigue exclusivo de la super
    // administradora sembrada en la base es asignarle el rol super_admin a alguien (ver
    // CrearUsuarioUseCase/ActualizarUsuarioUseCase y Roles.Asignables).

    [Fact]
    public async Task Un_administrador_puede_crear_un_usuario_con_un_rol_normal()
    {
        var client = ClientAs("admin");

        var response = await client.PostAsJsonAsync("/api/usuarios", new CreateUsuarioDto
        {
            Id = Guid.NewGuid(),
            Nombre = "Persona",
            Apellido = "Nueva",
            Email = $"{Guid.NewGuid():N}@nexit-test.com",
            Rol = "miembro",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Un_administrador_no_puede_crear_un_usuario_con_rol_super_admin()
    {
        var client = ClientAs("admin");

        var response = await client.PostAsJsonAsync("/api/usuarios", new CreateUsuarioDto
        {
            Id = Guid.NewGuid(),
            Nombre = "Persona",
            Apellido = "Nueva",
            Email = $"{Guid.NewGuid():N}@nexit-test.com",
            Rol = "super_admin",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Un_administrador_puede_editar_el_perfil_de_otra_persona_con_un_rol_normal()
    {
        var client = ClientAs("admin");
        // Un usuario adicional, NO el "miembro" sembrado compartido con el resto de la clase -- esta
        // prueba sí persiste el cambio (a diferencia del rechazo de abajo), y mutar el rol del
        // sembrado dejaría a otras pruebas de este archivo corriendo con datos distintos a los que
        // esperan.
        var otroId = await CrearUsuarioAdicionalAsync("miembro");

        var response = await client.PutAsJsonAsync($"/api/usuarios/{otroId}", new UpdateUsuarioDto
        {
            Nombre = "Intento",
            Apellido = "De edición",
            Rol = "manager",
            Activo = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Un_administrador_no_puede_asignarle_el_rol_super_admin_a_otra_persona()
    {
        var client = ClientAs("admin");
        var otroId = UsuarioSembradoId("miembro");

        var response = await client.PutAsJsonAsync($"/api/usuarios/{otroId}", new UpdateUsuarioDto
        {
            Nombre = "Intento",
            Apellido = "De edición",
            Rol = "super_admin",
            Activo = true,
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
