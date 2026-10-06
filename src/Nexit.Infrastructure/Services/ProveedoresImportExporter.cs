using ClosedXML.Excel;
using FluentValidation;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.DTOs.Proveedores;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Proveedores;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Infrastructure.Services;

/// <summary>
/// Implementación con ClosedXML de <see cref="IProveedoresImportExporter"/> (docs/31, docs/35) -- ver
/// el comentario de <see cref="IClientesImportExporter"/> para el diseño general, y el de esa misma
/// clase para el criterio de reimportar (upsert por Nombre; en blanco no borra, con valor sí
/// reemplaza; Teléfono/Email nunca se borran, solo se agregan si son nuevos). La diferencia con
/// clientes: País y Categoría son obligatorios y se guardan como referencia (Guid) a un catálogo, no
/// como texto libre -- el Excel trae el NOMBRE (lo que la usuaria realmente tiene a mano), y esta
/// clase lo resuelve al Id correspondiente antes de crear/actualizar el proveedor. Si el nombre no
/// coincide con ningún país/categoría/ciudad existente (por ejemplo, un error de tipeo), la fila
/// queda marcada como error con el nombre exacto que no se encontró -- no se crea el catálogo solo,
/// ni se adivina el más parecido, para no terminar con "Colombia" y "colombiaa" como dos países
/// distintos. Los servicios asociados (ServicioIds) no vienen en este Excel -- al actualizar, se
/// conservan tal cual los que el proveedor ya tenía, nunca se vacían por reimportar.
/// </summary>
public class ProveedoresImportExporter(
    ICrearProveedorUseCase crear,
    IActualizarProveedorUseCase actualizar,
    IValidator<CreateProveedorDto> validator,
    IValidator<UpdateProveedorDto> updateValidator,
    IProveedorRepository proveedorRepository,
    ICatalogosRepository catalogos) : IProveedoresImportExporter
{
    private static readonly string[] Columnas =
    [
        "Nombre", "País", "Ciudad", "Categoría", "Estado", "Contacto", "Cargo del contacto", "Email",
        "Web", "Dirección", "Aforo", "Costo de referencia", "Score (1-5)", "Presupuesto", "Cobertura",
        "Teléfono", "Notas",
    ];

    public byte[] Exportar(IReadOnlyList<ProveedorResponseDto> proveedores)
    {
        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Proveedores");
        for (var i = 0; i < Columnas.Length; i++) hoja.Cell(1, i + 1).Value = Columnas[i];
        hoja.Range(1, 1, 1, Columnas.Length).Style.Font.Bold = true;

        for (var i = 0; i < proveedores.Count; i++)
        {
            var p = proveedores[i];
            var fila = i + 2;
            hoja.Cell(fila, 1).Value = p.Nombre;
            // País/Ciudad/Categoría se exportan como texto (no Id) a propósito -- es lo que se puede
            // volver a importar; el Id no le sirve de nada a quien abre el Excel.
            hoja.Cell(fila, 5).Value = p.Estado;
            hoja.Cell(fila, 6).Value = p.Contacto ?? "";
            hoja.Cell(fila, 7).Value = p.CargoContacto ?? "";
            hoja.Cell(fila, 8).Value = p.Emails.Count > 0 ? p.Emails[0].Email : "";
            hoja.Cell(fila, 9).Value = p.Web ?? "";
            hoja.Cell(fila, 10).Value = p.Direccion ?? "";
            if (p.Aforo.HasValue) hoja.Cell(fila, 11).Value = p.Aforo.Value;
            hoja.Cell(fila, 12).Value = TextoDinero(p.CostoReferenciaValor, p.Moneda, p.CostoReferencia);
            if (p.Score.HasValue) hoja.Cell(fila, 13).Value = p.Score.Value;
            hoja.Cell(fila, 14).Value = p.Presupuesto ?? "";
            hoja.Cell(fila, 15).Value = p.Cobertura ?? "";
            hoja.Cell(fila, 16).Value = p.Telefonos.Count > 0 ? p.Telefonos[0].Telefono : "";
            hoja.Cell(fila, 17).Value = p.Notas ?? "";
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

            var nombrePais = Texto(celdas.Cell(2));
            var nombreCiudad = TextoOpcional(celdas.Cell(3));
            var nombreCategoria = Texto(celdas.Cell(4));

            var paisId = string.IsNullOrWhiteSpace(nombrePais) ? null : await catalogos.FindPaisIdPorNombreAsync(nombrePais, cancellationToken);
            var categoriaId = string.IsNullOrWhiteSpace(nombreCategoria) ? null : await catalogos.FindCategoriaIdPorNombreAsync(nombreCategoria, cancellationToken);
            if (!string.IsNullOrWhiteSpace(nombrePais) && paisId is null)
            {
                resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = $"El país \"{nombrePais}\" no existe en Catálogos -- créalo ahí primero, o corrige el nombre." });
                continue;
            }
            if (!string.IsNullOrWhiteSpace(nombreCategoria) && categoriaId is null)
            {
                resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = $"La categoría \"{nombreCategoria}\" no existe en Catálogos -- créala ahí primero, o corrige el nombre." });
                continue;
            }

            Guid? ciudadId = null, regionId = null;
            if (!string.IsNullOrWhiteSpace(nombreCiudad))
            {
                if (string.IsNullOrWhiteSpace(nombrePais))
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = "La ciudad requiere también el país en esa misma fila." });
                    continue;
                }
                var ciudad = await catalogos.FindCiudadPorNombreAsync(nombrePais, nombreCiudad, cancellationToken);
                if (ciudad is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = $"La ciudad \"{nombreCiudad}\" no existe en Catálogos dentro de \"{nombrePais}\" -- créala ahí primero, o corrige el nombre." });
                    continue;
                }
                ciudadId = ciudad.Value.CiudadId;
                regionId = ciudad.Value.RegionId;
            }

            var nombre = Texto(celdas.Cell(1));
            var telefono = Texto(celdas.Cell(16));
            var email = TextoOpcional(celdas.Cell(8));
            var estado = TextoOpcional(celdas.Cell(5));
            var contacto = TextoOpcional(celdas.Cell(6));
            var cargoContacto = TextoOpcional(celdas.Cell(7));
            var web = TextoOpcional(celdas.Cell(9));
            var direccion = TextoOpcional(celdas.Cell(10));
            var aforo = NumeroEnteroOpcional(celdas.Cell(11));
            var costoReferencia = TextoOpcional(celdas.Cell(12));
            decimal? costoReferenciaNum = null; var costoReferenciaMoneda = (string?)null;
            if (costoReferencia is not null && Nexit.Core.Utils.DineroParser.TryParse(costoReferencia, out var _m, out var _mon)) { costoReferenciaNum = _m; costoReferenciaMoneda = _mon; }
            var score = NumeroEnteroOpcional(celdas.Cell(13));
            var presupuesto = TextoOpcional(celdas.Cell(14));
            var cobertura = TextoOpcional(celdas.Cell(15));
            var notas = TextoOpcional(celdas.Cell(17));

            // docs/35: reimportar no duplica -- si ya existe un proveedor con este nombre, esta fila lo
            // actualiza en vez de crear uno nuevo.
            var existenteId = await proveedorRepository.FindIdPorNombreAsync(nombre, cancellationToken);

            if (existenteId is null)
            {
                var dto = new CreateProveedorDto
                {
                    Nombre = nombre,
                    PaisId = paisId ?? Guid.Empty,
                    RegionId = regionId,
                    CiudadId = ciudadId,
                    CategoriaId = categoriaId ?? Guid.Empty,
                    Estado = estado ?? "Activo",
                    Contacto = contacto,
                    CargoContacto = cargoContacto,
                    Web = web,
                    Direccion = direccion,
                    Aforo = aforo,
                    CostoReferencia = costoReferencia,
                    CostoReferenciaValor = costoReferenciaNum,
                    Moneda = costoReferenciaMoneda ?? Nexit.Core.Constants.Monedas.Cop,
                    Score = score,
                    Presupuesto = presupuesto,
                    Cobertura = cobertura,
                    Notas = notas,
                    Telefonos = string.IsNullOrWhiteSpace(telefono) ? [] : [new ProveedorTelefonoDto { Telefono = telefono }],
                    // Ver el comentario equivalente en ClientesImportExporter -- el Excel solo trae una
                    // columna "Email".
                    Emails = email is null ? [] : [new ProveedorEmailDto { Email = email }],
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
                // Ver el comentario equivalente en ClientesImportExporter sobre por qué este GetByIdAsync
                // no hace una segunda consulta real (mapa de identidad de EF Core dentro del mismo scope).
                var existente = await proveedorRepository.GetByIdAsync(existenteId.Value, cancellationToken);
                if (existente is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = "El proveedor encontrado por nombre ya no existe (se borró justo ahora) -- vuelve a intentar la importación." });
                    continue;
                }

                var dto = new UpdateProveedorDto
                {
                    Id = existenteId.Value,
                    Nombre = nombre,
                    // País y Categoría son obligatorios en cada fila (igual que al crear) -- si la fila
                    // no los trae, ya se rechazó arriba como error, así que acá siempre vienen resueltos.
                    PaisId = paisId ?? Guid.Empty,
                    CategoriaId = categoriaId ?? Guid.Empty,
                    // Región/Ciudad sí son opcionales -- en blanco, se conserva lo que ya tenía.
                    RegionId = regionId ?? existente.RegionId,
                    CiudadId = ciudadId ?? existente.CiudadId,
                    Estado = estado ?? existente.Estado,
                    Contacto = contacto ?? existente.Contacto,
                    CargoContacto = cargoContacto ?? existente.CargoContacto,
                    Web = web ?? existente.Web,
                    Direccion = direccion ?? existente.Direccion,
                    Aforo = aforo ?? existente.Aforo,
                    CostoReferencia = costoReferencia ?? existente.CostoReferencia,
                    CostoReferenciaValor = costoReferenciaNum ?? existente.CostoReferenciaValor,
                    Moneda = costoReferenciaMoneda ?? existente.Moneda,
                    Score = score ?? existente.Score,
                    Presupuesto = presupuesto ?? existente.Presupuesto,
                    Cobertura = cobertura ?? existente.Cobertura,
                    Notas = notas ?? existente.Notas,
                    Telefonos = MergeTelefonos(existente.Telefonos, telefono),
                    Emails = MergeEmails(existente.Emails, email),
                    // Servicios no vienen en este Excel -- se conservan tal cual, nunca se vacían por
                    // reimportar (ver el comentario de la clase).
                    ServicioIds = existente.Servicios.Select(s => s.ServicioId).ToList(),
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

    /// <summary>Ver el comentario equivalente en ClientesImportExporter.MergeTelefonos.</summary>
    private static List<ProveedorTelefonoDto> MergeTelefonos(IEnumerable<ProveedorTelefono> existentes, string telefonoExcel)
    {
        var lista = existentes.Select(t => new ProveedorTelefonoDto { Id = t.Id, Telefono = t.Telefono, Etiqueta = t.Etiqueta }).ToList();
        if (!string.IsNullOrWhiteSpace(telefonoExcel) && !lista.Any(t => string.Equals(t.Telefono.Trim(), telefonoExcel.Trim(), StringComparison.OrdinalIgnoreCase)))
            lista.Add(new ProveedorTelefonoDto { Telefono = telefonoExcel });
        return lista;
    }

    /// <summary>Ver el comentario equivalente en ClientesImportExporter.MergeEmails.</summary>
    private static List<ProveedorEmailDto> MergeEmails(IEnumerable<ProveedorEmail> existentes, string? emailExcel)
    {
        var lista = existentes.Select(e => new ProveedorEmailDto { Id = e.Id, Email = e.Email, Etiqueta = e.Etiqueta }).ToList();
        if (!string.IsNullOrWhiteSpace(emailExcel) && !lista.Any(e => string.Equals(e.Email.Trim(), emailExcel.Trim(), StringComparison.OrdinalIgnoreCase)))
            lista.Add(new ProveedorEmailDto { Email = emailExcel });
        return lista;
    }

    private static string Texto(IXLCell celda) => celda.GetString().Trim();
    private static string? TextoOpcional(IXLCell celda) { var texto = Texto(celda); return string.IsNullOrWhiteSpace(texto) ? null : texto; }
    private static int? NumeroEnteroOpcional(IXLCell celda) => celda.TryGetValue(out int numero) ? numero : null;

    private static string TextoDinero(decimal? valor, string moneda, string? textoLegado)
    {
        if (!valor.HasValue) return textoLegado ?? "";
        var n = valor.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        return moneda == Nexit.Core.Constants.Monedas.Cop ? n : $"{moneda} {n}";
    }
}
