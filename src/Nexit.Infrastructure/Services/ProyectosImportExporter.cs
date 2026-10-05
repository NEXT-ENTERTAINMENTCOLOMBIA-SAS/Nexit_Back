using ClosedXML.Excel;
using FluentValidation;
using Nexit.Application.DTOs.Importacion;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.Services;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Infrastructure.Services;

/// <summary>
/// Implementación con ClosedXML de <see cref="IProyectosImportExporter"/> (docs/31, docs/35) -- ver
/// el comentario de <see cref="IClientesImportExporter"/> para el diseño general, y el de esa misma
/// clase para el criterio de reimportar. A propósito, la importación de proyectos NO incluye equipo,
/// proveedores asociados ni gerente explícito -- son relaciones, no datos planos de una fila, y
/// forzarlas a columnas de Excel (nombres separados por comas, con el riesgo de no encontrar a la
/// persona exacta) complicaría el archivo sin necesidad; esas tres cosas se completan después,
/// proyecto por proyecto, desde la pantalla de edición -- exactamente igual que si el proyecto se
/// hubiera creado a mano sin llenarlas todavía. Por eso, al ACTUALIZAR un proyecto ya existente, esas
/// tres cosas se conservan tal cual estaban -- reimportar el archivo nunca las vacía.
/// Reimportar: la llave para saber "esto ya lo tenemos" es la pareja (Cliente, Nombre), no el nombre
/// solo -- ver <see cref="IProyectoRepository.FindIdPorClienteYNombreAsync"/> -- porque el mismo
/// nombre de proyecto puede repetirse legítimamente para clientes distintos.
/// </summary>
public class ProyectosImportExporter(
    ICrearProyectoUseCase crear,
    IActualizarProyectoUseCase actualizar,
    IValidator<CrearProyectoDto> validator,
    IValidator<ActualizarProyectoDto> updateValidator,
    IClienteRepository clientes,
    IProyectoRepository proyectoRepository,
    ICatalogosRepository catalogos) : IProyectosImportExporter
{
    private static readonly string[] Columnas =
    [
        "Nombre", "Cliente", "Contacto del proyecto", "Tipo de proyecto", "Prioridad", "Ciudad",
        "Sede Next", "Fecha de solicitud", "Fecha del evento", "Estado", "% de avance",
        "Estado de la propuesta", "N.º de factura", "Pagado (Sí/No)",
        "Fecha de pago", "Notas",
    ];

    /// <summary>
    /// El Excel exportado trae el NOMBRE del cliente y del estado (no su Id) -- es lo único que le
    /// sirve a quien abre el archivo, y es exactamente lo que <see cref="ImportarAsync"/> vuelve a
    /// resolver al importar, así que un archivo exportado siempre se puede reimportar tal cual.
    /// </summary>
    public async Task<byte[]> ExportarAsync(IReadOnlyList<ProyectoResponseDto> proyectos, CancellationToken cancellationToken = default)
    {
        var nombresClientes = new Dictionary<Guid, string>();
        foreach (var clienteId in proyectos.Where(p => p.ClienteId.HasValue).Select(p => p.ClienteId!.Value).Distinct())
        {
            var cliente = await clientes.GetByIdAsync(clienteId, cancellationToken);
            if (cliente is not null) nombresClientes[clienteId] = cliente.Nombre;
        }
        var estados = await catalogos.GetEstadosAsync(null, cancellationToken);
        var nombresEstados = estados.ToDictionary(e => e.Id, e => e.Nombre);

        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Proyectos");
        for (var i = 0; i < Columnas.Length; i++) hoja.Cell(1, i + 1).Value = Columnas[i];
        hoja.Range(1, 1, 1, Columnas.Length).Style.Font.Bold = true;

        for (var i = 0; i < proyectos.Count; i++)
        {
            var p = proyectos[i];
            var fila = i + 2;
            hoja.Cell(fila, 1).Value = p.Nombre;
            hoja.Cell(fila, 2).Value = p.ClienteId.HasValue && nombresClientes.TryGetValue(p.ClienteId.Value, out var nombreCliente) ? nombreCliente : "";
            hoja.Cell(fila, 3).Value = p.ContactoProyecto ?? "";
            hoja.Cell(fila, 4).Value = p.TipoProyecto ?? "";
            hoja.Cell(fila, 5).Value = p.Prioridad ?? "";
            hoja.Cell(fila, 6).Value = p.Ciudad ?? "";
            hoja.Cell(fila, 7).Value = p.SedeNext ?? "";
            if (p.FechaSolicitud.HasValue) hoja.Cell(fila, 8).Value = p.FechaSolicitud.Value;
            if (p.FechaEvento.HasValue) hoja.Cell(fila, 9).Value = p.FechaEvento.Value;
            hoja.Cell(fila, 10).Value = nombresEstados.TryGetValue(p.EstadoId, out var nombreEstado) ? nombreEstado : "";
            hoja.Cell(fila, 11).Value = p.PorcentajeAvance;
            hoja.Cell(fila, 12).Value = p.PropuestaEstado;
            hoja.Cell(fila, 13).Value = p.NumeroFactura ?? "";
            hoja.Cell(fila, 14).Value = p.Pagado ? "Sí" : "No";
            if (p.FechaPago.HasValue) hoja.Cell(fila, 15).Value = p.FechaPago.Value;
            hoja.Cell(fila, 16).Value = p.Notas ?? "";
        }
        for (var i = 1; i <= Columnas.Length; i++) hoja.Column(i).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ImportarResultadoDto> ImportarAsync(Stream archivo, Guid usuarioId, string? usuarioRol, CancellationToken cancellationToken = default)
    {
        var resultado = new ImportarResultadoDto();
        using var workbook = new XLWorkbook(archivo);
        var hoja = workbook.Worksheet(1);
        var ultimaFila = hoja.LastRowUsed()?.RowNumber() ?? 1;

        for (var fila = 2; fila <= ultimaFila; fila++)
        {
            var celdas = hoja.Row(fila);
            if (celdas.IsEmpty()) continue;

            var nombreCliente = TextoOpcional(celdas.Cell(2));
            Guid? clienteId = null;
            if (nombreCliente is not null)
            {
                clienteId = await clientes.FindIdPorNombreAsync(nombreCliente, cancellationToken);
                if (clienteId is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = $"El cliente \"{nombreCliente}\" no existe -- créalo primero (en Clientes, o en una fila anterior de este mismo archivo si también lo estás importando)." });
                    continue;
                }
            }

            var nombreEstado = Texto(celdas.Cell(10));
            var estadoId = string.IsNullOrWhiteSpace(nombreEstado) ? null : await catalogos.FindEstadoIdPorNombreAsync(nombreEstado, cancellationToken);
            if (estadoId is null)
            {
                resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = string.IsNullOrWhiteSpace(nombreEstado) ? "El estado del proyecto es requerido." : $"El estado \"{nombreEstado}\" no existe en Catálogos -- corrige el nombre." });
                continue;
            }

            var nombre = Texto(celdas.Cell(1));
            var contactoProyecto = TextoOpcional(celdas.Cell(3));
            var tipoProyecto = TextoOpcional(celdas.Cell(4));
            var prioridad = TextoOpcional(celdas.Cell(5));
            var ciudad = TextoOpcional(celdas.Cell(6));
            var sedeNext = TextoOpcional(celdas.Cell(7));
            var fechaSolicitud = FechaOpcional(celdas.Cell(8));
            var fechaEvento = FechaOpcional(celdas.Cell(9));
            var porcentajeAvance = NumeroEnteroOpcional(celdas.Cell(11));
            var propuestaEstado = TextoOpcional(celdas.Cell(12));
            var numeroFactura = TextoOpcional(celdas.Cell(13));
            var pagadoExcel = EsSiONoOpcional(celdas.Cell(14));
            var notas = TextoOpcional(celdas.Cell(16));

            // docs/35: reimportar no duplica -- la llave es (Cliente, Nombre), no el nombre solo, porque
            // el mismo nombre de proyecto puede repetirse legítimamente para clientes distintos.
            var existenteId = await proyectoRepository.FindIdPorClienteYNombreAsync(clienteId, nombre, cancellationToken);

            if (existenteId is null)
            {
                var pagado = pagadoExcel ?? false;
                var dto = new CrearProyectoDto
                {
                    Nombre = nombre,
                    ClienteId = clienteId,
                    ContactoProyecto = contactoProyecto,
                    TipoProyecto = tipoProyecto,
                    Prioridad = prioridad,
                    Ciudad = ciudad,
                    SedeNext = sedeNext,
                    FechaSolicitud = fechaSolicitud,
                    FechaEvento = fechaEvento,
                    EstadoId = estadoId.Value,
                    PorcentajeAvance = porcentajeAvance ?? 0,
                    PropuestaEstado = propuestaEstado ?? "No enviada",
                    NumeroFactura = numeroFactura,
                    Pagado = pagado,
                    FechaPago = pagado ? (FechaOpcional(celdas.Cell(15)) ?? DateTime.UtcNow) : null,
                    Notas = notas,
                };

                var validacion = await validator.ValidateAsync(dto, cancellationToken);
                if (!validacion.IsValid)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = string.Join("; ", validacion.Errors.Select(e => e.ErrorMessage)) });
                    continue;
                }

                try
                {
                    await crear.ExecuteAsync(dto, usuarioId, usuarioRol, cancellationToken);
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
                var existente = await proyectoRepository.GetByIdAsync(existenteId.Value, cancellationToken);
                if (existente is null)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = "El proyecto encontrado ya no existe (se borró justo ahora) -- vuelve a intentar la importación." });
                    continue;
                }

                var pagado = pagadoExcel ?? existente.Pagado;
                var dto = new ActualizarProyectoDto
                {
                    Id = existenteId.Value,
                    Nombre = nombre,
                    ClienteId = clienteId,
                    // Campo en blanco en el Excel = se conserva el valor que el proyecto ya tenía; campo
                    // con un valor = lo reemplaza si es distinto (docs/35).
                    ContactoProyecto = contactoProyecto ?? existente.ContactoProyecto,
                    TipoProyecto = tipoProyecto ?? existente.TipoProyecto,
                    Prioridad = prioridad ?? existente.Prioridad,
                    Ciudad = ciudad ?? existente.Ciudad,
                    SedeNext = sedeNext ?? existente.SedeNext,
                    FechaSolicitud = fechaSolicitud ?? existente.FechaSolicitud,
                    FechaEvento = fechaEvento ?? existente.FechaEvento,
                    EstadoId = estadoId.Value,
                    PorcentajeAvance = porcentajeAvance ?? existente.PorcentajeAvance,
                    PropuestaEstado = propuestaEstado ?? existente.PropuestaEstado,
                    NumeroFactura = numeroFactura ?? existente.NumeroFactura,
                    Pagado = pagado,
                    FechaPago = pagado ? (FechaOpcional(celdas.Cell(15)) ?? existente.FechaPago ?? DateTime.UtcNow) : null,
                    Notas = notas ?? existente.Notas,
                    // Gerente, equipo y proveedores asociados no vienen en este Excel (ver el comentario
                    // de la clase) -- se conservan tal cual, nunca se vacían por reimportar.
                    GerenteId = existente.GerenteId,
                    Equipo = existente.Equipo.Select(e => new ProyectoEquipoDto { Id = e.Id, Rol = e.Rol, Nombre = e.Nombre }).ToList(),
                    ProveedorIds = existente.Proveedores.Select(pp => pp.ProveedorId).ToList(),
                };

                var validacion = await updateValidator.ValidateAsync(dto, cancellationToken);
                if (!validacion.IsValid)
                {
                    resultado.Errores.Add(new ImportarErrorDto { Fila = fila, Mensaje = string.Join("; ", validacion.Errors.Select(e => e.ErrorMessage)) });
                    continue;
                }

                try
                {
                    await actualizar.ExecuteAsync(dto, usuarioId, usuarioRol, cancellationToken);
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

    private static string Texto(IXLCell celda) => celda.GetString().Trim();
    private static string? TextoOpcional(IXLCell celda) { var texto = Texto(celda); return string.IsNullOrWhiteSpace(texto) ? null : texto; }
    private static int? NumeroEnteroOpcional(IXLCell celda) => celda.TryGetValue(out int numero) ? numero : null;
    // ClosedXML devuelve las fechas de Excel con Kind=Unspecified (Excel no guarda zona
    // horaria) -- Npgsql rechaza escribir eso en una columna "timestamp with time zone"
    // ("Cannot write DateTime with Kind=Unspecified... only UTC is supported"). Se marca
    // como Utc explícitamente
    // para este mismo problema -- no se le suma ni resta nada a la hora, solo se etiqueta.
    private static DateTime? FechaOpcional(IXLCell celda) =>
        celda.TryGetValue(out DateTime fecha) ? DateTime.SpecifyKind(fecha, DateTimeKind.Utc) : null;
    /// <summary>
    /// A diferencia de la versión anterior (que devolvía false para una celda en blanco), esta versión
    /// devuelve null cuando la celda está vacía -- así, al actualizar un proyecto ya existente, dejar
    /// esta columna en blanco en el Excel NO revierte "Pagado" a No por accidente; conserva lo que el
    /// proyecto ya tenía (ver el criterio general de reimportar, docs/35). Un texto que no es
    /// afirmativo ("no", cualquier otra cosa) sigue contando como No explícito, no como "en blanco".
    /// </summary>
    private static bool? EsSiONoOpcional(IXLCell celda)
    {
        var texto = Texto(celda).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return texto is "si" or "sí" or "yes" or "true" or "1";
    }
}
