using Moq;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Core.Utils;

namespace Nexit.Tests;

/// <summary>
/// Nivel 1 del sistema de prioridad/sugerencias (docs/21, docs/22): la rúbrica de puntos pura
/// (<see cref="PrioridadProyectoCalculador"/>) y el caso de uso que la aplica a los proyectos
/// activos, ordenándolos de mayor a menor puntaje.
/// </summary>
public class PrioridadProyectosTests
{
    private static readonly DateTime Ahora = new(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc);

    private static Proyecto ProyectoBase() => new()
    {
        Nombre = "Lanzamiento", PropuestaEstado = "Enviada", Pagado = true
    };

    [Fact]
    public void Calcular_sin_ninguna_senal_da_puntaje_cero_sin_razones()
    {
        var proyecto = ProyectoBase();
        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad: Ahora, ahora: Ahora);

        Assert.Equal(0, resultado.Puntaje);
        Assert.Empty(resultado.Razones);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void Calcular_otorga_puntos_maximos_si_el_evento_es_en_la_semana(int diasHastaElEvento)
    {
        var proyecto = ProyectoBase();
        proyecto.FechaEvento = Ahora.AddDays(diasHastaElEvento);

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad: Ahora, ahora: Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosEventoEnLaSemana, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_otorga_menos_puntos_si_el_evento_es_dentro_del_mes_no_de_la_semana()
    {
        var proyecto = ProyectoBase();
        proyecto.FechaEvento = Ahora.AddDays(20);

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad: Ahora, ahora: Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosEventoEnElMes, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_no_otorga_puntos_por_un_evento_lejano_ni_por_uno_ya_pasado()
    {
        var lejano = ProyectoBase(); lejano.FechaEvento = Ahora.AddDays(45);
        var pasado = ProyectoBase(); pasado.FechaEvento = Ahora.AddDays(-3);

        Assert.Equal(0, PrioridadProyectoCalculador.Calcular(lejano, Ahora, Ahora).Puntaje);
        Assert.Equal(0, PrioridadProyectoCalculador.Calcular(pasado, Ahora, Ahora).Puntaje);
    }

    [Theory]
    [InlineData("Alta", 25)]
    [InlineData("alta", 25)]
    [InlineData("Media", 10)]
    [InlineData("Baja", 0)]
    [InlineData(null, 0)]
    public void Calcular_puntua_la_prioridad_manual_sin_importar_mayusculas(string? prioridad, int puntosEsperados)
    {
        var proyecto = ProyectoBase();
        proyecto.Prioridad = prioridad;

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, Ahora, Ahora);

        Assert.Equal(puntosEsperados, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_marca_estancado_cuando_pasan_5_dias_o_mas_sin_actividad()
    {
        var proyecto = ProyectoBase();
        var ultimaActividad = Ahora.AddDays(-5);

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad, Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosSinActividadReciente, resultado.Puntaje);
        Assert.Contains(resultado.Razones, r => r.Contains("Sin actividad"));
    }

    [Fact]
    public void Calcular_no_marca_estancado_con_actividad_reciente()
    {
        var proyecto = ProyectoBase();
        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad: Ahora.AddDays(-1), ahora: Ahora);

        Assert.Equal(0, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_puntua_si_la_propuesta_no_se_ha_enviado()
    {
        var proyecto = ProyectoBase();
        proyecto.PropuestaEstado = "No enviada";

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, Ahora, Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosPropuestaPendiente, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_no_penaliza_pago_pendiente_si_el_evento_todavia_esta_lejos()
    {
        var proyecto = ProyectoBase();
        proyecto.Pagado = false;
        proyecto.FechaEvento = Ahora.AddDays(30);

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, Ahora, Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosEventoEnElMes, resultado.Puntaje); // solo por el evento, no por el pago
    }

    [Fact]
    public void Calcular_penaliza_pago_pendiente_cuando_el_evento_ya_esta_cerca()
    {
        var proyecto = ProyectoBase();
        proyecto.Pagado = false;
        proyecto.FechaEvento = Ahora.AddDays(5);

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, Ahora, Ahora);

        Assert.Equal(PrioridadProyectoCalculador.PuntosEventoEnLaSemana + PrioridadProyectoCalculador.PuntosSinPagarConEventoCerca, resultado.Puntaje);
    }

    [Fact]
    public void Calcular_suma_todas_las_senales_que_apliquen_a_la_vez()
    {
        var proyecto = new Proyecto
        {
            Nombre = "Crítico", FechaEvento = Ahora.AddDays(2), Prioridad = "Alta",
            PropuestaEstado = "No enviada", Pagado = false
        };

        var resultado = PrioridadProyectoCalculador.Calcular(proyecto, ultimaActividad: Ahora.AddDays(-10), ahora: Ahora);

        var esperado = PrioridadProyectoCalculador.PuntosEventoEnLaSemana + PrioridadProyectoCalculador.PuntosPrioridadAlta
            + PrioridadProyectoCalculador.PuntosSinActividadReciente + PrioridadProyectoCalculador.PuntosPropuestaPendiente
            + PrioridadProyectoCalculador.PuntosSinPagarConEventoCerca;
        Assert.Equal(esperado, resultado.Puntaje);
        Assert.Equal(5, resultado.Razones.Count);
    }

    [Fact]
    public async Task ConsultarPrioridad_excludes_projects_in_a_terminal_state()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoActivo = new EstadoProyecto { Id = Guid.NewGuid(), Nombre = "En curso", Fase = 2 };
        var estadoFinalizado = new EstadoProyecto { Id = Guid.NewGuid(), Nombre = "Finalizado", Fase = 2 };
        catalogos.Setup(x => x.GetEstadosAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([estadoActivo, estadoFinalizado]);
        proyectos.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Proyecto { Nombre = "Activo", EstadoId = estadoActivo.Id, Prioridad = "Alta" },
            new Proyecto { Nombre = "Ya terminado", EstadoId = estadoFinalizado.Id, Prioridad = "Alta" },
        ]);

        var result = await new ConsultarPrioridadProyectosUseCase(proyectos.Object, catalogos.Object).ExecuteAsync();

        var item = Assert.Single(result);
        Assert.Equal("Activo", item.Nombre);
    }

    [Fact]
    public async Task ConsultarPrioridad_orders_by_score_descending_and_includes_reasons()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoActivo = new EstadoProyecto { Id = Guid.NewGuid(), Nombre = "En curso", Fase = 2 };
        catalogos.Setup(x => x.GetEstadosAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([estadoActivo]);
        proyectos.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new Proyecto { Nombre = "Sin señales", EstadoId = estadoActivo.Id, PropuestaEstado = "Enviada", Pagado = true },
            new Proyecto { Nombre = "Prioridad alta", EstadoId = estadoActivo.Id, Prioridad = "Alta", PropuestaEstado = "Enviada", Pagado = true },
        ]);

        var result = await new ConsultarPrioridadProyectosUseCase(proyectos.Object, catalogos.Object).ExecuteAsync();

        Assert.Equal("Prioridad alta", result[0].Nombre);
        Assert.Equal("Sin señales", result[1].Nombre);
        Assert.NotEmpty(result[0].Razones);
        Assert.Empty(result[1].Razones);
    }

    [Fact]
    public async Task ConsultarPrioridad_uses_project_creation_date_when_there_is_no_seguimiento_yet()
    {
        // Un proyecto recién creado sin ninguna entrada de bitácora todavía debe poder puntuarse --
        // no debe quedar "sin actividad = null" (docs/21).
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoActivo = new EstadoProyecto { Id = Guid.NewGuid(), Nombre = "En curso", Fase = 2 };
        catalogos.Setup(x => x.GetEstadosAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([estadoActivo]);
        var proyectoViejo = new Proyecto { Nombre = "Viejo sin bitácora", EstadoId = estadoActivo.Id, PropuestaEstado = "Enviada", Pagado = true, CreatedAt = DateTime.UtcNow.AddDays(-30) };
        proyectos.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([proyectoViejo]);

        var result = await new ConsultarPrioridadProyectosUseCase(proyectos.Object, catalogos.Object).ExecuteAsync();

        var item = Assert.Single(result);
        Assert.Equal(PrioridadProyectoCalculador.PuntosSinActividadReciente, item.Puntaje);
    }
}
