namespace Nexit.Core.Constants;

/// <summary>
/// Las listas editables desde Configuración y los valores que NO se pueden renombrar ni eliminar
/// porque la lógica de negocio los reconoce por nombre (ver <c>PrioridadProyectoCalculador</c>: "alta",
/// "media" y "No enviada" suman puntos). Agregar valores nuevos a cualquier lista siempre es seguro.
/// </summary>
public static class ListasConfigurables
{
    public const string TipoProyecto = "tipo-proyecto";
    public const string Prioridad = "prioridad";
    public const string SedeNext = "sede-next";
    public const string EstadoPropuesta = "estado-propuesta";
    public const string AreaSeguimiento = "area-seguimiento";

    public static readonly string[] Todas = [TipoProyecto, Prioridad, SedeNext, EstadoPropuesta, AreaSeguimiento];

    /// <summary>Valores iniciales (los mismos que estaban fijos antes) -- se siembran en docs/schema/31.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ValoresIniciales = new Dictionary<string, string[]>
    {
        [TipoProyecto] = ["Corporativo", "Evento social"],
        [Prioridad] = ["Alta", "Media", "Baja"],
        [SedeNext] = ["Bogotá", "Ciudad de México"],
        [EstadoPropuesta] = ["No enviada", "En proceso", "Enviada"],
        [AreaSeguimiento] = ["General", "Creativo", "Comercial", "Administrativo"],
    };

    public static readonly IReadOnlyDictionary<string, string[]> Protegidos = new Dictionary<string, string[]>
    {
        [Prioridad] = ["Alta", "Media"],
        [EstadoPropuesta] = ["No enviada"],
        [AreaSeguimiento] = ["General"],
    };

    public static bool EsProtegido(string lista, string valor) =>
        Protegidos.TryGetValue(lista, out var v) && v.Any(x => string.Equals(x, valor, StringComparison.OrdinalIgnoreCase));
}
