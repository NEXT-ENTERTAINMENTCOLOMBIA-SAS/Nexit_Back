using Nexit.Application.DTOs.Clientes;
using Nexit.Application.Validators.Clientes;
using Nexit.Core.Utils;
using Xunit;

namespace Nexit.Tests;

public class DineroTests
{
    [Theory]
    [InlineData("$1.500.000", 1500000, "COP")]
    [InlineData("1,500,000", 1500000, "COP")]
    [InlineData("COP 2500000", 2500000, "COP")]
    [InlineData("USD 1200", 1200, "USD")]
    [InlineData("2500000", 2500000, "COP")]
    public void Parser_convierte_formatos_comunes(string texto, decimal esperado, string moneda)
    {
        Assert.True(DineroParser.TryParse(texto, out var monto, out var mon));
        Assert.Equal(esperado, monto);
        Assert.Equal(moneda, mon);
    }

    [Theory]
    [InlineData("")]
    [InlineData("desde $500k aprox")]
    [InlineData("a convenir")]
    public void Parser_rechaza_texto_ambiguo(string texto)
    {
        Assert.False(DineroParser.TryParse(texto, out _, out _));
    }

    [Fact]
    public void Validador_rechaza_monto_negativo_y_moneda_invalida()
    {
        var r = new CreateClienteValidator(new Moq.Mock<Nexit.Core.Interfaces.IClienteRepository>().Object).Validate(new CreateClienteDto { Nombre = "X", ValorReferenciaMonto = -5, Moneda = "ZZZ" });
        var props = r.Errors.Select(e => e.PropertyName).ToList();
        Assert.Contains("ValorReferenciaMonto", props);
        Assert.Contains("Moneda", props);
    }
}
