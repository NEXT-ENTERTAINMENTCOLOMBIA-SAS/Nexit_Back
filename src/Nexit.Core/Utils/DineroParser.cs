using System.Globalization;
using System.Text.RegularExpressions;
using Nexit.Core.Constants;

namespace Nexit.Core.Utils;

/// <summary>
/// Convierte los montos que antes se escribían como texto libre ("$1.500.000", "1,500,000", "COP 2500000")
/// a número + moneda. Conservador a propósito: si el texto no es claramente un monto (trae palabras,
/// rangos, "k", "aprox."...) devuelve false y el texto original se conserva sin tocar.
/// </summary>
public static partial class DineroParser
{
    public static bool TryParse(string? texto, out decimal monto, out string moneda)
    {
        monto = 0; moneda = Monedas.Cop;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var t = texto.Trim().ToUpperInvariant();

        if (t.Contains("USD") || t.Contains("US$") || t.Contains("DOLAR")) moneda = "USD";
        else if (t.Contains("MXN") || t.Contains("MX$") || t.Contains("PESO MEX")) moneda = "MXN";
        else if (t.Contains("EUR") || t.Contains('€')) moneda = "EUR";

        // Quita monedas y símbolos; si queda cualquier letra no es un monto limpio.
        t = QuitarMoneda().Replace(t, "").Replace("$", "").Replace("€", "").Replace(" ", "").Replace(" ", "");
        if (t.Length == 0 || !SoloNumero().IsMatch(t)) return false;

        string normalizado;
        var puntos = t.Count(c => c == '.'); var comas = t.Count(c => c == ',');
        if (puntos > 0 && comas > 0)
        {
            // El último separador es el decimal; el otro es de miles.
            var decimalEsComa = t.LastIndexOf(',') > t.LastIndexOf('.');
            normalizado = decimalEsComa ? t.Replace(".", "").Replace(',', '.') : t.Replace(",", "");
        }
        else if (puntos > 1 || comas > 1) normalizado = t.Replace(".", "").Replace(",", "");
        else if (puntos == 1 || comas == 1)
        {
            var sep = puntos == 1 ? '.' : ',';
            var despues = t.Length - t.IndexOf(sep) - 1;
            // Exactamente 3 dígitos después = separador de miles ("1.500"); si no, decimales ("1500.50").
            normalizado = despues == 3 ? t.Replace(sep.ToString(), "") : t.Replace(sep, '.');
        }
        else normalizado = t;

        if (!decimal.TryParse(normalizado, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out monto) || monto < 0 || monto > 9_999_999_999_999m) { monto = 0; return false; }
        return true;
    }

    [GeneratedRegex(@"COP|USD|MXN|EUR|US\$|MX\$")] private static partial Regex QuitarMoneda();
    [GeneratedRegex(@"^[0-9][0-9.,]*$")] private static partial Regex SoloNumero();
}
