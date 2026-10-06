using ClosedXML.Excel;
using FluentValidation;
using Nexit.Application.DTOs.Clientes;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Clientes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Infrastructure.Services;

/// <summary>
/// Implementación con ClosedXML de <see cref="IClientesImportExporter"/> (docs/31, docs/35) -- ver
/// el comentario de la interfaz para el diseño general (una fila por registro, una fila inválida no
/// detiene el archivo). Reimportar: si ya existe un cliente con el mismo Nombre (sin distinguir
/// mayúsculas/espacios, ver <see cref="IClienteRepository.FindIdPorNombreAsync"/>), esta fila lo
/// ACTUALIZA en vez de crear uno nuevo -- un campo en blanco en el Excel NUNCA borra un dato que el
/// cliente ya tenía (se conserva el valor existente), y un campo con un valor SÍ lo reemplaza si es
/// distinto. Teléfono/Email son la excepción: como el Excel solo trae uno de cada uno, nunca se
/// borra ninguno de los que ya tenía el cliente -- si el de la fila es nuevo, se agrega a la lista;
/// si ya estaba, no se duplica.
/// "País" y "Estado" se agregaron 2026-09-03 al FINAL de las columnas (no intercaladas) para no
/// romper una plantilla de Excel que alguien ya tenga con el layout anterior. "Ciudad" se mantiene
/// como texto libre (no se resuelve contra el catálogo, a diferencia de Proveedores) -- Cliente
/// guarda ambas cosas (ver comentario de CiudadId en la entidad); "País" si se resuelve, porque es
/// la referencia que de verdad necesita el formulario en cascada del mockup.
/// </summary>
public class ClientesImportExporter(
    ICrearClienteUseCase crear,
    IActualizarClienteUseCase actualizar,
    IValidator<CreateClienteDto> validator,
    IValidator<UpdateClienteDto> updateValidator,
    IClienteRepository clienteRepository,
    ICatalogosRepository catalogos) : IClientesImportExporter
{
    private static readonly string[] Columnas =
    [
        "Nombre", "Sector", "Ciudad", "Dirección", "Web", "Contacto", "Cargo del contacto",
        "Email", "Valor de referencia", "Teléfono", "Notas", "País", "Estado",
    ];

    public byte[] Exportar(IReadOnlyList<ClienteResponseDto> clientes)
    {
        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Clientes");
        for (var i = 0; i < Columnas.Length; i++) hoja.Cell(1, i + 1).Value = Columnas[i];
        hoja.Range(1, 1, 1, Columnas.Length).Style.Font.Bold = true;

        for (var i = 0; i < clientes.Count; i++)
        {
            var c = clientes[i];
            var fila = i + 2;
            hoja.Cell(fila, 1).Value = c.Nombre;
            hoja.Cell(fila, 2).Value = c.Sector ?? "";
            hoja.Cell(fila, 3).Value = c.Ciudad ?? "";
            hoja.Cell(fila, 4).Value = c.Direccion ?? "";
            hoja.Cell(fila, 5).Value = c.Web ?? "";
            hoja.Cell(fila, 6).Value = c.Contacto ?? "";
            hoja.Cell(fila, 7).Value = c.CargoContacto ?? "";
            hoja.Cell(fila, 8).Value = c.Emails.Count > 0 ? c.Emails[0].Email : "";
            hoja.Cell(fila, 9).Value = TextoDinero(c.ValorReferenciaMonto, c.Moneda, c.ValorReferencia);
            hoja.Cell(fila, 10).Value = c.Telefonos.Count > 0 ? c.Telefonos[0].Telefono : "";
            hoja.Cell(fila, 11).Value = c.Notas ?? "";
            // País se exporta como texto (no Id) -- ver comentario de ProveedoresImportExporter.
            hoja.Cell(fila, 13).Value = c.Estado;
        }
        for (var i = 1; i <= Columnas.Length; i++) hoja.Column(i).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ImportarResultadoDto> ImportarAsync(Stream archivo, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var resultado = new ImportarResultadoDto();
        using var workbook = new XLWorkbook(archivo);
        var hoja = workbook.Worksheet(1);
        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 1;

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var celdas = hoja.Row(fila);
            if (celdas.IsEmpty()) continue;

            var nombrePais = TextoOpcional(celdas.Cell(12));
            Guid? paisId = null;
            if (nombrePais is not null)
            {
                paisId = await catalogos.FindPaisIdPorNombreAsync(nombrePais, cancellationToken);
                if (paisId is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = $"El país \"{nombrePais}\" no existe en Catálogos -- créalo ahí primero, o corrige el nombre." });
                    continue;
                }
            }

            var nombre = Texto(celdas.Cell(1));
            var telefono = Texto(celdas.Cell(10));
            var email = TextoOpcional(celdas.Cell(8));
            var sector = TextoOpcional(celdas.Cell(2));
            var ciudad = TextoOpcional(celdas.Cell(3));
            var direccion = TextoOpcional(celdas.Cell(4));
            var web = TextoOpcional(celdas.Cell(5));
            var contacto = TextoOpcional(celdas.Cell(6));
            var cargoContacto = TextoOpcional(celdas.Cell(7));
            var valorReferencia = TextoOpcional(celdas.Cell(9));
            decimal? valorReferenciaNum = null; var valorReferenciaMoneda = (string?)null;
            if (valorReferencia is not null && Nexit.Core.Utils.DineroParser.TryParse(valorReferencia, out var _m, out var _mon)) { valorReferenciaNum = _m; valorReferenciaMoneda = _mon; }
            var notas = TextoOpcional(celdas.Cell(11));
            var estado = TextoOpcional(celdas.Cell(13));

            // docs/35: reimportar no duplica -- si ya existe un cliente con este nombre, esta fila lo
            // actualiza en vez de crear uno nuevo.
            var existenteId = await clienteRepository.FindIdPorNombreAsync(nombre, cancellationToken);

            if (existenteId is null)
            {
                var dto = new CreateClienteDto
                {
                    Nombre = nombre,
                    Sector = sector,
                    PaisId = paisId,
                    Ciudad = ciudad,
                    Direccion = direccion,
                    Web = web,
                    Contacto = contacto,
                    CargoContacto = cargoContacto,
                    ValorReferencia = valorReferencia,
                    ValorReferenciaMonto = valorReferenciaNum,
                    Moneda = valorReferenciaMoneda ?? Nexit.Core.Constants.Monedas.Cop,
                    Notas = notas,
                    Estado = estado ?? "Activo",
                    Telefonos = string.IsNullOrWhiteSpace(telefono) ? [] : [new ClienteTelefonoDto { Telefono = telefono }],
                    // El Excel solo trae una columna "Email" -- se guarda como el único elemento de la
                    // lista. Si alguien necesita agregar un segundo correo, lo hace después desde el
                    // formulario (docs/34: "Pues hay que tener en cuenta que puede tener varios correos").
                    Emails = email is null ? [] : [new ClienteEmailDto { Email = email }],
                };

                var validacion = await validator.ValidateAsync(dto, cancellationToken);
                if (!validacion.IsValid)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = string.Join("; ", validacion.Errors.Select(e => e.ErrorMessage)) });
                    continue;
                }

                try
                {
                    await crear.ExecuteAsync(dto, usuarioId, cancellationToken);
                    resultado.Creados++;
                }
                catch (BusinessRuleException ex)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = ex.Message });
                }
            }
            else
            {
                // GetByIdAsync (no AsNoTracking) a propósito, no un método aparte: dentro del mismo scope
                // de request, ActualizarClienteUseCase vuelve a pedir este mismo cliente por Id más abajo
                // y EF Core devuelve la misma instancia ya rastreada (mapa de identidad) -- no se hace una
                // segunda consulta real a la base ni se corre riesgo de leer un dato desactualizado.
                var existente = await clienteRepository.GetByIdAsync(existenteId.Value, cancellationToken);
                if (existente is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = "El cliente encontrado por nombre ya no existe (se borró justo ahora) -- vuelve a intentar la importación." });
                    continue;
                }

                var dto = new UpdateClienteDto
                {
                    Id = existenteId.Value,
                    Nombre = nombre,
                    // Campo en blanco en el Excel = se conserva el valor que el cliente ya tenía; campo
                    // con un valor = lo reemplaza si es distinto (docs/35).
                    Sector = sector ?? existente.Sector,
                    PaisId = paisId ?? existente.PaisId,
                    // RegionId/CiudadId/EtapaId no vienen en el Excel (no hay columna para ellos) -- se
                    // conservan tal cual, nunca se tocan por importar.
                    RegionId = existente.RegionId,
                    CiudadId = existente.CiudadId,
                    EtapaId = existente.EtapaId,
                    Ciudad = ciudad ?? existente.Ciudad,
                    Direccion = direccion ?? existente.Direccion,
                    Web = web ?? existente.Web,
                    Contacto = contacto ?? existente.Contacto,
                    CargoContacto = cargoContacto ?? existente.CargoContacto,
                    ValorReferencia = valorReferencia ?? existente.ValorReferencia,
                    ValorReferenciaMonto = valorReferenciaNum ?? existente.ValorReferenciaMonto,
                    Moneda = valorReferenciaMoneda ?? existente.Moneda,
                    Notas = notas ?? existente.Notas,
                    Estado = estado ?? existente.Estado,
                    Telefonos = MergeTelefonos(existente.Telefonos, telefono),
                    Emails = MergeEmails(existente.Emails, email),
                };

                var validacion = await updateValidator.ValidateAsync(dto, cancellationToken);
                if (!validacion.IsValid)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = string.Join("; ", validacion.Errors.Select(e => e.ErrorMessage)) });
                    continue;
                }

                try
                {
                    await actualizar.ExecuteAsync(dto, usuarioId, cancellationToken);
                    resultado.Actualizados++;
                }
                catch (BusinessRuleException ex)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = ex.Message });
                }
            }
        }
        return resultado;
    }

    /// <summary>
    /// Conserva TODOS los teléfonos que el cliente ya tenía (con su Id real, para que
    /// ActualizarClienteUseCase los reconozca como existentes y no los reinserte) y agrega el del
    /// Excel solo si es nuevo -- así reimportar el mismo archivo dos veces no duplica el teléfono, y
    /// nunca se borra uno que se haya agregado desde el formulario.
    /// </summary>
    private static List<ClienteTelefonoDto> MergeTelefonos(IEnumerable<ClienteTelefono> existentes, string telefonoExcel)
    {
        var lista = existentes.Select(t => new ClienteTelefonoDto { Id = t.Id, Telefono = t.Telefono, Etiqueta = t.Etiqueta }).ToList();
        if (!string.IsNullOrWhiteSpace(telefonoExcel) && !lista.Any(t => string.Equals(t.Telefono.Trim(), telefonoExcel.Trim(), StringComparison.OrdinalIgnoreCase)))
            lista.Add(new ClienteTelefonoDto { Telefono = telefonoExcel });
        return lista;
    }

    /// <summary>Mismo criterio que <see cref="MergeTelefonos"/>, para los correos.</summary>
    private static List<ClienteEmailDto> MergeEmails(IEnumerable<ClienteEmail> existentes, string? emailExcel)
    {
        var lista = existentes.Select(e => new ClienteEmailDto { Id = e.Id, Email = e.Email, Etiqueta = e.Etiqueta }).ToList();
        if (!string.IsNullOrWhiteSpace(emailExcel) && !lista.Any(e => string.Equals(e.Email.Trim(), emailExcel.Trim(), StringComparison.OrdinalIgnoreCase)))
            lista.Add(new ClienteEmailDto { Email = emailExcel });
        return lista;
    }

    private static string Texto(IXLCell celda) => celda.GetString().Trim();
    private static string? TextoOpcional(IXLCell celda) { var texto = Texto(celda); return string.IsNullOrWhiteSpace(texto) ? null : texto; }

    private static string TextoDinero(decimal? valor, string moneda, string? textoLegado)
    {
        if (!valor.HasValue) return textoLegado ?? "";
        var n = valor.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return moneda == Nexit.Core.Constants.Monedas.Cop ? n : $"{moneda} {n}";
    }
}
