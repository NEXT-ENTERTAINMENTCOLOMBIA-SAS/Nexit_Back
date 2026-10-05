using ClosedXML.Excel;
using Moq;
using Nexit.Application.DTOs.Informes;
using Nexit.Application.UseCases.Informes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Services;

namespace Nexit.Tests;

public class InformesTests
{
    [Fact]
    public async Task GenerarSnapshot_persists_current_totals()
    {
        var repository = new Mock<IInformesRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        repository.Setup(x => x.GetByPeriodoAsync("mensual", "2026-08", It.IsAny<CancellationToken>())).ReturnsAsync((InformeSnapshot?)null);
        repository.Setup(x => x.ObtenerDatosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InformeDatos(3, 2, 4, 1, new Dictionary<string, int> { ["En curso"] = 2 }, new Dictionary<string, int> { ["Aprobado"] = 4 }));

        var result = await new GenerarInformeSnapshotUseCase(repository.Object, unitOfWork.Object).ExecuteAsync(new CrearInformeSnapshotDto { Tipo = "mensual", PeriodoKey = "2026-08" }, Guid.NewGuid());

        Assert.Equal(4, result.TotalProyectos);
        Assert.Equal(2, result.PorEstado["En curso"]);
        repository.Verify(x => x.AddAsync(It.Is<InformeSnapshot>(s => s.TotalClientes == 2 && s.PeriodoKey == "2026-08"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerarSnapshot_rejects_an_invalid_type()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => new GenerarInformeSnapshotUseCase(Mock.Of<IInformesRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new CrearInformeSnapshotDto { Tipo = "diario", PeriodoKey = "2026-08-17" }, Guid.NewGuid()));
    }
}

/// <summary>
/// Exportación a Excel de un informe (ver docs/07-calendario-e-informes-excel.md) -- se abre el
/// .xlsx generado con ClosedXML para verificar de verdad su contenido, no solo que el arreglo de
/// bytes no esté vacío.
/// </summary>
public class InformeExcelExporterTests
{
    private static InformeResumenDto DatosDeEjemplo() => new()
    {
        TotalProveedores = 10, TotalClientes = 5, TotalProyectos = 8, ProyectosSinProveedor = 2,
        PorEstado = new Dictionary<string, int> { ["En curso"] = 5, ["Finalizado"] = 3 }
    };

    [Fact]
    public void Exportar_produces_a_valid_workbook_with_two_sheets()
    {
        var bytes = new InformeExcelExporter().Exportar("Informe mensual — 2026-08", DatosDeEjemplo());

        Assert.NotEmpty(bytes);
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(["Resumen", "Por estado"], workbook.Worksheets.Select(w => w.Name));
    }

    [Fact]
    public void Exportar_writes_the_totals_on_the_resumen_sheet()
    {
        var bytes = new InformeExcelExporter().Exportar("Informe mensual — 2026-08", DatosDeEjemplo());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var resumen = workbook.Worksheet("Resumen");
        Assert.Equal("Informe mensual — 2026-08", resumen.Cell(1, 1).GetString());
        var valores = resumen.RangeUsed()!.Rows().Skip(2).ToDictionary(r => r.Cell(1).GetString(), r => r.Cell(2).GetValue<int>());
        Assert.Equal(10, valores["Total proveedores"]);
        Assert.Equal(5, valores["Total clientes"]);
        Assert.Equal(8, valores["Total proyectos"]);
        Assert.Equal(2, valores["Proyectos sin proveedor"]);
    }

    [Fact]
    public void Exportar_writes_one_row_per_estado_ordered_by_count_descending()
    {
        var bytes = new InformeExcelExporter().Exportar("Informe mensual — 2026-08", DatosDeEjemplo());

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var hoja = workbook.Worksheet("Por estado");
        Assert.Equal("Estado", hoja.Cell(1, 1).GetString());
        Assert.Equal("En curso", hoja.Cell(2, 1).GetString());
        Assert.Equal(5, hoja.Cell(2, 2).GetValue<int>());
        Assert.Equal("Finalizado", hoja.Cell(3, 1).GetString());
        Assert.Equal(3, hoja.Cell(3, 2).GetValue<int>());
    }

    [Fact]
    public void Exportar_handles_empty_dictionaries_without_throwing()
    {
        var datos = new InformeResumenDto { TotalProveedores = 0, TotalClientes = 0, TotalProyectos = 0, ProyectosSinProveedor = 0, PorEstado = new Dictionary<string, int>() };

        var bytes = new InformeExcelExporter().Exportar("Informe vacío", datos);

        Assert.NotEmpty(bytes);
    }
}
