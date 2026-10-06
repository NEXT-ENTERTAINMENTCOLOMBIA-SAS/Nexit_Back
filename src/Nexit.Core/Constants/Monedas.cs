namespace Nexit.Core.Constants;

/// <summary>Monedas con las que trabaja Next. COP por defecto; los montos se guardan numéricos junto a su moneda.</summary>
public static class Monedas
{
    public const string Cop = "COP";
    public static readonly IReadOnlyList<string> Todas = ["COP", "MXN", "USD", "EUR"];
    public static bool EsValida(string? moneda) => moneda is not null && Todas.Contains(moneda);
}
