using Moq;
using Nexit.Application.DTOs.Configuracion;
using Nexit.Application.UseCases.Configuracion;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Tests;

/// <summary>Configuración editable: nombres de rol, listas de Proyectos y dominios de correo permitidos.</summary>
public class ConfiguracionTests
{
    private readonly Mock<IConfiguracionRepository> _repo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private ConfiguracionService Servicio() => new(_repo.Object, _uow.Object, new ConfiguracionCache());

    [Fact]
    public async Task GetRoles_devuelve_los_4_roles_con_texto_por_defecto_si_la_tabla_esta_vacia()
    {
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var roles = await Servicio().GetRolesAsync();

        Assert.Equal(Roles.Todos, roles.Select(r => r.Rol));
        Assert.All(roles, r => Assert.False(string.IsNullOrWhiteSpace(r.Etiqueta)));
    }

    [Fact]
    public async Task GetRoles_usa_el_nombre_guardado_por_encima_del_de_respaldo()
    {
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new RolConfig { Rol = "manager", Etiqueta = "PM Senior", Descripcion = "d" }]);

        var roles = await Servicio().GetRolesAsync();

        Assert.Equal("PM Senior", roles.Single(r => r.Rol == "manager").Etiqueta);
    }

    [Fact]
    public async Task ActualizarRol_guarda_el_nuevo_nombre()
    {
        var existente = new RolConfig { Rol = "manager", Etiqueta = "Director", Descripcion = "x" };
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existente]);
        _repo.Setup(x => x.GetRolAsync("manager", It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var dto = await Servicio().ActualizarRolAsync("manager", new ActualizarRolConfigDto { Etiqueta = "  Project Manager ", Descripcion = "Dirige proyectos" });

        Assert.Equal("Project Manager", dto.Etiqueta);
        Assert.Equal("Project Manager", existente.Etiqueta);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarRol_crea_la_fila_si_no_existia()
    {
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _repo.Setup(x => x.GetRolAsync("miembro", It.IsAny<CancellationToken>())).ReturnsAsync((RolConfig?)null);

        await Servicio().ActualizarRolAsync("miembro", new ActualizarRolConfigDto { Etiqueta = "Colaborador", Descripcion = "" });

        _repo.Verify(x => x.AddRolAsync(It.Is<RolConfig>(r => r.Rol == "miembro" && r.Etiqueta == "Colaborador"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("jefe")]
    [InlineData("")]
    public async Task ActualizarRol_rechaza_una_clave_que_no_es_un_rol(string rol) =>
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().ActualizarRolAsync(rol, new ActualizarRolConfigDto { Etiqueta = "X" }));

    [Fact]
    public async Task ActualizarRol_rechaza_nombre_vacio_o_demasiado_largo()
    {
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().ActualizarRolAsync("admin", new ActualizarRolConfigDto { Etiqueta = "   " }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().ActualizarRolAsync("admin", new ActualizarRolConfigDto { Etiqueta = new string('a', 61) }));
    }

    [Fact]
    public async Task ActualizarRol_rechaza_un_nombre_que_ya_usa_otro_rol()
    {
        _repo.Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);   // por defecto: admin = "Admin"

        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().ActualizarRolAsync("manager", new ActualizarRolConfigDto { Etiqueta = "admin" }));
    }

    [Fact]
    public async Task GetOpciones_devuelve_todas_las_listas_aun_vacias()
    {
        _repo.Setup(x => x.GetOpcionesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([new OpcionConfig { Lista = "prioridad", Valor = "Alta", Orden = 1 }]);

        var listas = await Servicio().GetOpcionesAsync();

        Assert.Equal(ListasConfigurables.Todas.OrderBy(x => x), listas.Keys.OrderBy(x => x));
        Assert.True(listas["prioridad"].Single().Protegido);
        Assert.Empty(listas["sede-next"]);
    }

    [Fact]
    public async Task CrearOpcion_agrega_al_final_de_la_lista()
    {
        _repo.Setup(x => x.OpcionExisteAsync("sede-next", "Medellín", null, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _repo.Setup(x => x.SiguienteOrdenAsync("sede-next", It.IsAny<CancellationToken>())).ReturnsAsync((short)3);

        var dto = await Servicio().CrearOpcionAsync("sede-next", new GuardarOpcionDto { Valor = " Medellín " });

        Assert.Equal("Medellín", dto.Valor);
        Assert.Equal(3, dto.Orden);
        _repo.Verify(x => x.AddOpcionAsync(It.Is<OpcionConfig>(o => o.Lista == "sede-next" && o.Valor == "Medellín"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearOpcion_rechaza_duplicados_listas_inexistentes_y_textos_invalidos()
    {
        _repo.Setup(x => x.OpcionExisteAsync("prioridad", "Alta", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().CrearOpcionAsync("prioridad", new GuardarOpcionDto { Valor = "Alta" }));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => Servicio().CrearOpcionAsync("inventada", new GuardarOpcionDto { Valor = "X" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().CrearOpcionAsync("prioridad", new GuardarOpcionDto { Valor = "  " }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().CrearOpcionAsync("prioridad", new GuardarOpcionDto { Valor = new string('x', 101) }));
    }

    [Fact]
    public async Task ActualizarOpcion_renombra_tambien_los_proyectos_que_la_usaban()
    {
        var opcion = new OpcionConfig { Lista = "tipo-proyecto", Valor = "Corporativo", Orden = 1 };
        _repo.Setup(x => x.GetOpcionAsync("tipo-proyecto", opcion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(opcion);
        _repo.Setup(x => x.OpcionExisteAsync("tipo-proyecto", "Empresarial", opcion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Servicio().ActualizarOpcionAsync("tipo-proyecto", opcion.Id, new GuardarOpcionDto { Valor = "Empresarial" });

        _repo.Verify(x => x.RenombrarOpcionAsync(opcion, "Empresarial", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task No_se_puede_renombrar_ni_eliminar_un_valor_que_la_prioridad_de_proyectos_necesita()
    {
        var alta = new OpcionConfig { Lista = "prioridad", Valor = "Alta", Orden = 1 };
        _repo.Setup(x => x.GetOpcionAsync("prioridad", alta.Id, It.IsAny<CancellationToken>())).ReturnsAsync(alta);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().ActualizarOpcionAsync("prioridad", alta.Id, new GuardarOpcionDto { Valor = "Urgente" }));
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().EliminarOpcionAsync("prioridad", alta.Id));
        _repo.Verify(x => x.RenombrarOpcionAsync(It.IsAny<OpcionConfig>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(x => x.RemoveOpcion(It.IsAny<OpcionConfig>()), Times.Never);
    }

    [Fact]
    public async Task EliminarOpcion_quita_una_opcion_normal()
    {
        var baja = new OpcionConfig { Lista = "prioridad", Valor = "Baja", Orden = 3 };
        _repo.Setup(x => x.GetOpcionAsync("prioridad", baja.Id, It.IsAny<CancellationToken>())).ReturnsAsync(baja);

        await Servicio().EliminarOpcionAsync("prioridad", baja.Id);

        _repo.Verify(x => x.RemoveOpcion(baja), Times.Once);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("@Empresa.com", "empresa.com")]
    [InlineData("  K11Technologies.com ", "k11technologies.com")]
    public async Task CrearDominio_normaliza_el_texto(string entrada, string esperado)
    {
        _repo.Setup(x => x.DominioExisteAsync(esperado, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var dto = await Servicio().CrearDominioAsync(new GuardarDominioDto { Dominio = entrada });

        Assert.Equal(esperado, dto.Dominio);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-punto")]
    [InlineData("con espacio.com")]
    [InlineData("a@b.com")]
    [InlineData(".com")]
    public async Task CrearDominio_rechaza_dominios_invalidos(string entrada) =>
        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().CrearDominioAsync(new GuardarDominioDto { Dominio = entrada }));

    [Fact]
    public async Task CrearDominio_rechaza_uno_ya_permitido()
    {
        _repo.Setup(x => x.DominioExisteAsync("empresa.com", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().CrearDominioAsync(new GuardarDominioDto { Dominio = "empresa.com" }));
    }

    [Fact]
    public async Task EliminarDominio_no_deja_la_lista_en_cero()
    {
        var d = new DominioCorreoPermitido { Dominio = "empresa.com" };
        _repo.Setup(x => x.GetDominioAsync(d.Id, It.IsAny<CancellationToken>())).ReturnsAsync(d);
        _repo.Setup(x => x.ContarDominiosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => Servicio().EliminarDominioAsync(d.Id));
        _repo.Verify(x => x.RemoveDominio(It.IsAny<DominioCorreoPermitido>()), Times.Never);
    }

    [Fact]
    public async Task EliminarDominio_quita_uno_si_quedan_otros()
    {
        var d = new DominioCorreoPermitido { Dominio = "viejo.com" };
        _repo.Setup(x => x.GetDominioAsync(d.Id, It.IsAny<CancellationToken>())).ReturnsAsync(d);
        _repo.Setup(x => x.ContarDominiosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);

        await Servicio().EliminarDominioAsync(d.Id);

        _repo.Verify(x => x.RemoveDominio(d), Times.Once);
    }
}
