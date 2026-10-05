using ClosedXML.Excel;
using Nexit.Application.DTOs.Informes;
using Nexit.Application.Services;

namespace Nexit.Infrastructure.Services;

/// <summary>
/// Implementación con ClosedXML (licencia MIT, gratis para uso comercial -- ver
/// docs/07-calendario-e-informes-excel.md sobre por qué se eligió sobre EPPlus) de
/// <see cref="IInformeExcelExporter"/>. Dos hojas por archivo: Resumen (los 4 totales) y
/// Por estado (una fila por cada valor del diccionario).
/// </summary>
public class InformeExcelExporter : IInformeExcelExporter
{
    public byte[] Exportar(string titulo, InformeResumenDto datos)
    {
        using var workbook = new XLWorkbook();

        var resumen = workbook.Worksheets.Add("Resumen");
        resumen.Cell(1, 1).Value = titulo;
        resumen.Cell(1, 1).Style.Font.Bold = true;
        resumen.Cell(1, 1).Style.Font.FontSize = 14;
        var filas = new (string Etiqueta, int Valor)[]
        {
            ("Total proveedores", datos.TotalProveedores),
            ("Total clientes", datos.TotalClientes),
            ("Total proyectos", datos.TotalProyectos),
            ("Proyectos sin proveedor", datos.ProyectosSinProveedor),
        };
        for (var i = 0; i < filas.Length; i++)
        {
            resumen.Cell(3 + i, 1).Value = filas[i].Etiqueta;
            resumen.Cell(3 + i, 2).Value = filas[i].Valor;
        }
        resumen.Column(1).AdjustToContents();
        resumen.Column(2).AdjustToContents();

        AgregarHojaDeConteo(workbook, "Por estado", "Estado", datos.PorEstado);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AgregarHojaDeConteo(XLWorkbook workbook, string nombreHoja, string tituloColumna, IReadOnlyDictionary<string, int> datos)
    {
        var hoja = workbook.Worksheets.Add(nombreHoja);
        hoja.Cell(1, 1).Value = tituloColumna;
        hoja.Cell(1, 2).Value = "Cantidad";
        hoja.Range(1, 1, 1, 2).Style.Font.Bold = true;
        var fila = 2;
        foreach (var (clave, cantidad) in datos.OrderByDescending(x => x.Value))
        {
            hoja.Cell(fila, 1).Value = clave;
            hoja.Cell(fila, 2).Value = cantidad;
            fila++;
        }
        hoja.Column(1).AdjustToContents();
        hoja.Column(2).AdjustToContents();
    }
}
