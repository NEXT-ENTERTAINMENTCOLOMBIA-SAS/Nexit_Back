using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexit.Application.DTOs.Configuracion;
using Nexit.Application.DTOs.PanelPm;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Core.Entities;
using Nexit.Infrastructure.Data;

namespace Nexit.Tests.Functional;

/// <summary>
/// Configuración editable (roles, listas de Proyectos, dominios) y panel de Project Managers contra
/// Postgres real: comprueba que la migración ConfiguracionEditable aplica limpio, que siembra los
/// datos iniciales y que ya no hay CHECK que impidan valores nuevos en las listas.
/// </summary>
public class ConfiguracionFunctionalTests(NexitFunctionalApiFactory factory) : FunctionalTestBase(factory)
{
    [Fact]
    public async Task La_migracion_siembra_los_4_roles_y_el_admin_puede_renombrar_uno()
    {
        var admin = ClientAs("admin");

        var roles = await admin.GetFromJsonAsync<List<RolConfigDto>>("/api/configuracion/roles");
        Assert.Equal(["super_admin", "admin", "manager", "miembro"], roles!.Select(r => r.Rol));
        Assert.Equal("Director", roles.Single(r => r.Rol == "manager").Etiqueta);

        var put = await admin.PutAsJsonAsync("/api/configuracion/roles/manager", new ActualizarRolConfigDto { Etiqueta = "Líder de proyecto", Descripcion = "Lidera proyectos" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Cualquier autenticado ve el nombre nuevo.
        var comoMiembro = await ClientAs("miembro").GetFromJsonAsync<List<RolConfigDto>>("/api/configuracion/roles");
        Assert.Equal("Líder de proyecto", comoMiembro!.Single(r => r.Rol == "manager").Etiqueta);

        await admin.PutAsJsonAsync("/api/configuracion/roles/manager", new ActualizarRolConfigDto { Etiqueta = "Director", Descripcion = "Director de sus proyectos: endosa la eliminación de los que tiene a cargo." });
    }

    [Fact]
    public async Task Renombrar_un_rol_a_un_nombre_ya_usado_responde_conflicto()
    {
        var resp = await ClientAs("admin").PutAsJsonAsync("/api/configuracion/roles/miembro", new ActualizarRolConfigDto { Etiqueta = "Admin" });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Las_listas_de_proyectos_vienen_sembradas_y_se_pueden_ampliar()
    {
        var admin = ClientAs("admin");
        var listas = await admin.GetFromJsonAsync<Dictionary<string, List<OpcionConfigDto>>>("/api/configuracion/opciones");
        Assert.Equal(["Alta", "Media", "Baja"], listas!["prioridad"].Select(o => o.Valor));
        // "Contains" y no "Equal": otras pruebas de esta misma clase agregan/renombran opciones de tipo.
        Assert.Subset(listas["tipo-proyecto"].Select(o => o.Valor).ToHashSet(), new HashSet<string> { "Corporativo", "Evento social" });
        Assert.True(listas["prioridad"].Single(o => o.Valor == "Alta").Protegido);

        var creada = await admin.PostAsJsonAsync("/api/configuracion/opciones/sede-next", new GuardarOpcionDto { Valor = "Medellín" });
        Assert.Equal(HttpStatusCode.OK, creada.StatusCode);
        var opcion = (await creada.Content.ReadFromJsonAsync<OpcionConfigDto>())!;

        var duplicada = await admin.PostAsJsonAsync("/api/configuracion/opciones/sede-next", new GuardarOpcionDto { Valor = "medellín" });
        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/configuracion/opciones/sede-next/{opcion.Id}")).StatusCode);
    }

    [Fact]
    public async Task Un_proyecto_acepta_una_prioridad_nueva_porque_ya_no_hay_check_en_la_base()
    {
        var admin = ClientAs("admin");
        var resp = await admin.PostAsJsonAsync("/api/proyectos", new CrearProyectoDto { Nombre = "Con prioridad nueva", EstadoId = EstadoProyectoId, Prioridad = "Urgente", TipoProyecto = "Feria", PropuestaEstado = "Aprobada" });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
    }

    [Fact]
    public async Task Renombrar_una_opcion_actualiza_los_proyectos_que_la_usaban_y_no_toca_las_protegidas()
    {
        var admin = ClientAs("admin");
        var nueva = await (await admin.PostAsJsonAsync("/api/configuracion/opciones/tipo-proyecto", new GuardarOpcionDto { Valor = "Lanzamiento" })).Content.ReadFromJsonAsync<OpcionConfigDto>();
        var creado = await admin.PostAsJsonAsync("/api/proyectos", new CrearProyectoDto { Nombre = "Proyecto a renombrar tipo", EstadoId = EstadoProyectoId, TipoProyecto = "Lanzamiento" });
        var proyecto = (await creado.Content.ReadFromJsonAsync<ProyectoResponseDto>())!;

        var put = await admin.PutAsJsonAsync($"/api/configuracion/opciones/tipo-proyecto/{nueva!.Id}", new GuardarOpcionDto { Valor = "Activación de marca" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexitDbContext>();
            var guardado = await db.Proyectos.FindAsync(proyecto.Id);
            Assert.Equal("Activación de marca", guardado!.TipoProyecto);
        }

        // Limpieza: esta prueba deja la lista de tipos como estaba.
        await admin.DeleteAsync($"/api/configuracion/opciones/tipo-proyecto/{nueva.Id}");

        var listas = await admin.GetFromJsonAsync<Dictionary<string, List<OpcionConfigDto>>>("/api/configuracion/opciones");
        var alta = listas!["prioridad"].Single(o => o.Valor == "Alta");
        var protegida = await admin.PutAsJsonAsync($"/api/configuracion/opciones/prioridad/{alta.Id}", new GuardarOpcionDto { Valor = "Urgente" });
        Assert.Equal(HttpStatusCode.Conflict, protegida.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.DeleteAsync($"/api/configuracion/opciones/prioridad/{alta.Id}")).StatusCode);
    }

    [Fact]
    public async Task Solo_el_super_admin_gestiona_los_dominios_de_correo()
    {
        var superAdmin = ClientAs("super_admin");
        var creado = await superAdmin.PostAsJsonAsync("/api/configuracion/dominios-correo", new GuardarDominioDto { Dominio = "@Otra-Empresa.com" });
        Assert.Equal(HttpStatusCode.OK, creado.StatusCode);
        var dominio = (await creado.Content.ReadFromJsonAsync<DominioCorreoDto>())!;
        Assert.Equal("otra-empresa.com", dominio.Dominio);

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientAs("admin").GetAsync("/api/configuracion/dominios-correo")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await superAdmin.PostAsJsonAsync("/api/configuracion/dominios-correo", new GuardarDominioDto { Dominio = "otra-empresa.com" })).StatusCode);

        // Con el dominio nuevo, ya se puede registrar a alguien de esa empresa.
        var registro = await superAdmin.PostAsJsonAsync("/api/usuarios", new Nexit.Application.DTOs.Usuarios.CreateUsuarioDto { Id = Guid.NewGuid(), Nombre = "Nueva", Apellido = "Persona", Email = $"{Guid.NewGuid():N}@otra-empresa.com", Rol = "miembro" });
        Assert.Equal(HttpStatusCode.Created, registro.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await superAdmin.DeleteAsync($"/api/configuracion/dominios-correo/{dominio.Id}")).StatusCode);
    }

    [Fact]
    public async Task El_panel_de_project_managers_cuenta_proyectos_personas_clientes_y_proveedores()
    {
        var manager = await CrearUsuarioAdicionalAsync("manager");
        var admin = ClientAs("admin");
        var creado = await admin.PostAsJsonAsync("/api/proyectos", new CrearProyectoDto
        {
            Nombre = "Proyecto del panel", EstadoId = EstadoProyectoId, GerenteId = manager,
            Equipo = [new ProyectoEquipoDto { Nombre = "Laura Gómez", Rol = "Producción" }, new ProyectoEquipoDto { Nombre = "Pedro Ruiz", Rol = "Diseño" }],
        });
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);

        var panel = (await admin.GetFromJsonAsync<PanelPmDto>("/api/panel/project-managers"))!;
        var tarjeta = panel.ProjectManagers.Single(p => p.Id == manager);
        Assert.Equal(1, tarjeta.TotalProyectos);
        Assert.Equal(2, tarjeta.TotalPersonas);
        Assert.Contains(tarjeta.Personas, p => p.Nombre == "Laura Gómez" && p.Cargo == "Producción");
        Assert.True(panel.Resumen.TotalProyectos >= 1);
    }
}
