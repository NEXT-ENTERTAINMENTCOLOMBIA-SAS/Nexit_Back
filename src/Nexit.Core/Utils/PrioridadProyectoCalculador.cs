using Nexit.Core.Entities;

namespace Nexit.Core.Utils;

/// <summary>
/// Rúbrica de puntos para "a qué proyecto atender primero" (docs/21 y docs/22) -- Nivel 1 de la
/// propuesta: reglas simples y auditables sobre datos que ya existen en Proyecto, sin IA. Puntaje
/// de 0 a 100, siempre acompañado de la lista de razones que lo explican (nunca solo un número).
///
/// Los pesos de acá son el punto de partida que se propuso en docs/21 -- la usuaria pidió
/// explícitamente probar primero este algoritmo de reglas antes de sumarle una capa de IA (docs/21
/// Nivel 2, todavía sin construir). Se espera ajustar estos pesos con casos reales una vez que se
/// use un tiempo -- por eso están todos centralizados como constantes acá arriba, fáciles de tocar
/// sin tener que entender el resto de la lógica.
/// </summary>
public static class PrioridadProyectoCalculador
{
    public const int PuntosEventoEnLaSemana = 30;
    public const int PuntosEventoEnElMes = 15;
    public const int PuntosPrioridadAlta = 25;
    public const int PuntosPrioridadMedia = 10;
    public const int PuntosSinActividadReciente = 20;
    public const int DiasSinActividadParaAlertar = 5;
    public const int PuntosPropuestaPendiente = 15;
    public const int PuntosSinPagarConEventoCerca = 10;
    public const int DiasParaConsiderarEventoCerca = 14;

    public record Resultado(int Puntaje, IReadOnlyList<string> Razones);

    /// <param name="proyecto">El proyecto a puntuar.</param>
    /// <param name="ultimaActividad">
    /// Fecha de la entrada más reciente de la bitácora de seguimiento del proyecto (o, si nunca
    /// tuvo ninguna, la fecha en que se creó el proyecto) -- ver cómo se calcula en
    /// <c>ConsultarPrioridadProyectosUseCase</c>. Siempre tiene un valor: un proyecto recién creado
    /// sin ninguna entrada de bitácora también debe poder puntuarse.
    /// </param>
    /// <param name="ahora">Momento actual (UTC) -- se recibe como parámetro, no se calcula acá adentro, para que esto sea puro y fácil de probar con fechas fijas.</param>
    public static Resultado Calcular(Proyecto proyecto, DateTime ultimaActividad, DateTime ahora)
    {
        var puntaje = 0;
        var razones = new List<string>();

        // 1) Qué tan cerca está la fecha del evento.
        if (proyecto.FechaEvento.HasValue)
        {
            var dias = (proyecto.FechaEvento.Value.Date - ahora.Date).Days;
            if (dias is >= 0 and <= 7) { puntaje += PuntosEventoEnLaSemana; razones.Add(dias == 0 ? "El evento es hoy." : $"El evento es en {dias} día(s)."); }
            else if (dias is > 7 and <= 30) { puntaje += PuntosEventoEnElMes; razones.Add("El evento es dentro del próximo mes."); }
        }

        // 2) Prioridad marcada manualmente (campo de texto libre, ver docs/21).
        if (!string.IsNullOrWhiteSpace(proyecto.Prioridad))
        {
            var prioridad = proyecto.Prioridad.Trim();
            if (Contiene(prioridad, "alta")) { puntaje += PuntosPrioridadAlta; razones.Add("Marcado con prioridad alta."); }
            else if (Contiene(prioridad, "media")) { puntaje += PuntosPrioridadMedia; razones.Add("Marcado con prioridad media."); }
        }

        // 3) Sin actividad reciente en la bitácora -- proyectos "estancados".
        var diasSinActividad = (ahora.Date - ultimaActividad.Date).Days;
        if (diasSinActividad >= DiasSinActividadParaAlertar)
        {
            puntaje += PuntosSinActividadReciente;
            razones.Add($"Sin actividad registrada hace {diasSinActividad} días.");
        }

        // 4) Propuesta todavía pendiente de enviar.
        var propuestaPendiente = string.Equals(proyecto.PropuestaEstado?.Trim(), "No enviada", StringComparison.OrdinalIgnoreCase);
        if (propuestaPendiente)
        {
            puntaje += PuntosPropuestaPendiente;
            razones.Add("La propuesta todavía no se ha enviado.");
        }

        // 5) Sin pagar y con el evento ya cerca.
        if (!proyecto.Pagado && proyecto.FechaEvento.HasValue)
        {
            var dias = (proyecto.FechaEvento.Value.Date - ahora.Date).Days;
            if (dias is >= 0 and <= DiasParaConsiderarEventoCerca)
            {
                puntaje += PuntosSinPagarConEventoCerca;
                razones.Add("Todavía no está pagado y el evento ya está cerca.");
            }
        }

        return new Resultado(puntaje, razones);
    }

    private static bool Contiene(string texto, string buscado) => texto.Contains(buscado, StringComparison.OrdinalIgnoreCase);
}
