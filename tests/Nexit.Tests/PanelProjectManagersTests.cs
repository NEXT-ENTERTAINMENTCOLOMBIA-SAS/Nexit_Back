using Moq;
using Nexit.Application.UseCases.PanelPm;
using Nexit.Core.Interfaces;

namespace Nexit.Tests;

/// <summary>Panel de Project Managers (reemplaza a Informes): qué proyectos, personas, clientes y proveedores tiene cada PM.</summary>
public class PanelProjectManagersTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Beto = Guid.NewGuid();
    private static readonly Guid ClienteA = Guid.NewGuid();
    private static readonly Guid ClienteB = Guid.NewGuid();
    private static readonly Guid Prov1 = Guid.NewGuid();
    private static readonly Guid Prov2 = Guid.NewGuid();

    private static PanelProyectoFila Proyecto(string nombre, Guid? gerente, Guid? cliente, string? clienteNombre,
        (string, string)[]? equipo = null, (Guid, string)[]? proveedores = null) =>
        new(Guid.NewGuid(), nombre, gerente, cliente, clienteNombre, "En curso", 50, null, null,
            (equipo ?? []).ToList(), (proveedores ?? []).ToList());

    private static ConsultarPanelProjectManagersUseCase Caso(IReadOnlyList<PanelProyectoFila> proyectos, IReadOnlyList<PanelUsuarioFila> usuarios)
    {
        var repo = new Mock<IPanelProjectManagersRepository>();
        repo.Setup(x => x.GetProyectosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(proyectos);
        repo.Setup(x => x.GetPosiblesProjectManagersAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(usuarios);
        return new ConsultarPanelProjectManagersUseCase(repo.Object);
    }

    [Fact]
    public async Task Agrupa_proyectos_personas_clientes_y_proveedores_por_project_manager()
    {
        var proyectos = new[]
        {
            Proyecto("Lanzamiento", Ana, ClienteA, "Acme", [("Laura", "Producción"), ("Pedro", "Diseño")], [(Prov1, "Luces SAS")]),
            Proyecto("Feria", Ana, ClienteB, "Beta", [("laura ", "Logística")], [(Prov1, "Luces SAS"), (Prov2, "Sonido Pro")]),
            Proyecto("Gala", Beto, ClienteA, "Acme", [("Pedro", "Diseño")]),
        };
        var usuarios = new[]
        {
            new PanelUsuarioFila(Ana, "Ana", "Ruiz", "ana@k11.com", "manager", true, "AR"),
            new PanelUsuarioFila(Beto, "Beto", "Gil", "beto@k11.com", "manager", true, "BG"),
        };

        var panel = await Caso(proyectos, usuarios).ExecuteAsync();

        var ana = Assert.Single(panel.ProjectManagers, p => p.Id == Ana);
        Assert.Equal(2, ana.TotalProyectos);
        Assert.Equal(2, ana.TotalPersonas);       // Laura (una sola vez, sin distinguir mayúsculas/espacios) y Pedro
        Assert.Equal(2, ana.TotalClientes);
        Assert.Equal(2, ana.TotalProveedores);
        Assert.Equal(2, ana.Proveedores.Single(x => x.Id == Prov1).Proyectos);
        var laura = ana.Personas.Single(x => x.Nombre == "Laura");
        Assert.Equal(2, laura.Proyectos);
        Assert.Contains("Producción", laura.Cargo);
        Assert.Contains("Logística", laura.Cargo);
        Assert.Equal("Ana Ruiz", ana.Nombre);

        Assert.Equal(2, panel.Resumen.ProjectManagersConProyectos);
        Assert.Equal(3, panel.Resumen.TotalProyectos);
        Assert.Equal(2, panel.Resumen.PersonasEnProyectos);   // Laura y Pedro, sin duplicar entre PMs
        Assert.Equal(2, panel.Resumen.ClientesActivos);
        Assert.Equal(2, panel.Resumen.ProveedoresActivos);
        Assert.Null(panel.SinProjectManager);
    }

    [Fact]
    public async Task Ordena_por_mayor_carga_primero()
    {
        var proyectos = new[] { Proyecto("A", Beto, null, null), Proyecto("B", Ana, null, null), Proyecto("C", Ana, null, null) };
        var usuarios = new[]
        {
            new PanelUsuarioFila(Beto, "Beto", "Gil", "b@k11.com", "manager", true, null),
            new PanelUsuarioFila(Ana, "Ana", "Ruiz", "a@k11.com", "manager", true, null),
        };

        var panel = await Caso(proyectos, usuarios).ExecuteAsync();

        Assert.Equal(["Ana Ruiz", "Beto Gil"], panel.ProjectManagers.Select(p => p.Nombre));
    }

    [Fact]
    public async Task Los_proyectos_sin_gerente_van_a_la_tarjeta_sin_project_manager()
    {
        var proyectos = new[] { Proyecto("Huérfano", null, ClienteA, "Acme"), Proyecto("Con PM", Ana, null, null) };
        var usuarios = new[] { new PanelUsuarioFila(Ana, "Ana", "Ruiz", "a@k11.com", "manager", true, null) };

        var panel = await Caso(proyectos, usuarios).ExecuteAsync();

        Assert.NotNull(panel.SinProjectManager);
        Assert.Equal(1, panel.SinProjectManager!.TotalProyectos);
        Assert.Equal("Sin Project Manager", panel.SinProjectManager.Nombre);
        Assert.Equal(1, panel.Resumen.ProyectosSinProjectManager);
    }

    [Fact]
    public async Task Un_project_manager_activo_sin_proyectos_se_cuenta_aparte()
    {
        var usuarios = new[] { new PanelUsuarioFila(Ana, "Ana", "Ruiz", "a@k11.com", "manager", true, null) };

        var panel = await Caso([], usuarios).ExecuteAsync();

        Assert.Single(panel.ProjectManagers);
        Assert.Equal(0, panel.Resumen.ProjectManagersConProyectos);
        Assert.Equal(1, panel.Resumen.ProjectManagersSinProyectos);
        Assert.Null(panel.SinProjectManager);
    }

    [Fact]
    public async Task Un_gerente_que_ya_no_existe_como_usuario_cae_en_sin_project_manager()
    {
        var proyectos = new[] { Proyecto("Antiguo", Guid.NewGuid(), null, null) };

        var panel = await Caso(proyectos, []).ExecuteAsync();

        Assert.Equal(1, panel.SinProjectManager!.TotalProyectos);
    }
}
