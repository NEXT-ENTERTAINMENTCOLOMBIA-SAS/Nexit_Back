using System.Linq.Expressions;
using FluentValidation;
using Nexit.Core.Constants;

namespace Nexit.Application.Validators;

/// <summary>Reglas comunes de los campos de dinero (monto numérico + moneda) -- se reutilizan en proyecto, proveedor y cliente.</summary>
public static class DineroRules
{
    public const decimal MontoMaximo = 9_999_999_999_999m;

    public static void AddDineroRules<T>(this AbstractValidator<T> validator, Expression<Func<T, decimal?>> monto, Expression<Func<T, string>> moneda)
    {
        validator.RuleFor(monto).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(MontoMaximo).When(_ => true).WithMessage("El monto no es válido (debe ser un número entre 0 y 9.999.999.999.999).");
        validator.RuleFor(moneda).Must(m => Monedas.EsValida(m)).WithMessage($"La moneda no es válida. Usa: {string.Join(", ", Monedas.Todas)}.");
    }
}
