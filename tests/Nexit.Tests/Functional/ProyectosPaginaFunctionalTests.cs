using System.Net.Http.Json;
using Nexit.Application.DTOs.Proyectos;

namespace Nexit.Tests.Functional;

/// <summary>El listado paginado de proyectos filtra y cuenta en la base real (no en memoria).</summary>
public class ProyectosPaginaFunctionalTests(NexitFunctionalApiFactory factory) : FunctionalTestBase(factory)
{
    [Fact]
    public async Task La_pagina_filtra_por_texto_y_tipo_y_respeta_el_tamano()
    {
        var admin = ClientAs("admin");
        var marca = $"PagTest{Guid.NewGuid():N}"[..14];
        for (var i = 0; i < 3; i++)
            Assert.True((await admin.PostAsJsonAsync("/api/proyectos", new CrearProyectoDto { Nombre = $"{marca} {i}", EstadoId = EstadoProyectoId, TipoProyecto = i == 0 ? "Corporativo" : "Evento social", PropuestaEstado = "No enviada" })).IsSuccessStatusCode);

        var todos = await admin.GetFromJsonAsync<ProyectosPaginaDto>($"/api/proyectos/pagina?q={marca}&pageSize=2");
        Assert.NotNull(todos);
        Assert.Equal(3, todos!.Total);
        Assert.Equal(2, todos.Items.Count);

        var segunda = await admin.GetFromJsonAsync<ProyectosPaginaDto>($"/api/proyectos/pagina?q={marca}&pageSize=2&page=2");
        Assert.Single(segunda!.Items);

        var corporativos = await admin.GetFromJsonAsync<ProyectosPaginaDto>($"/api/proyectos/pagina?q={marca}&tipo=Corporativo");
        Assert.Equal(1, corporativos!.Total);

        Assert.True(todos.Resumen.Total >= 3);
        Assert.True(todos.Resumen.SinProveedor >= 3);
        Assert.True(todos.Resumen.SinGerente >= 0);

        var sinProveedor = await admin.GetFromJsonAsync<ProyectosPaginaDto>($"/api/proyectos/pagina?q={marca}&alerta=sinProveedor");
        Assert.Equal(3, sinProveedor!.Total);
        var proximos = await admin.GetFromJsonAsync<ProyectosPaginaDto>($"/api/proyectos/pagina?q={marca}&alerta=proximos7");
        Assert.Equal(0, proximos!.Total);
    }

    [Fact]
    public async Task Los_comodines_del_texto_se_tratan_como_literales()
    {
        var admin = ClientAs("admin");
        var r = await admin.GetFromJsonAsync<ProyectosPaginaDto>("/api/proyectos/pagina?q=%25&pageSize=5");
        Assert.Equal(0, r!.Total);
    }
}
