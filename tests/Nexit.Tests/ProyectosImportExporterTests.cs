using ClosedXML.Excel;
using FluentValidation;
using Moq;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Application.Validators.Proyectos;
using Nexit.Core.Entities;
using Nexit.Core.Interfaces;
using Nexit.Infrastructure.Services;

namespace Nexit.Tests;

/// <summary>
/// Importar/exportar proyectos desde Excel (docs/31) -- la diferencia con clientes/proveedores es
/// que resuelve Cliente y Estado por NOMBRE (Estado es requerido, Cliente es opcional) y no incluye
/// equipo/proveedores/gerente (relaciones, se completan luego en la pantalla de edición).
///
/// Desde docs/35 el importador también ACTUALIZA en vez de duplicar cuando ya existe un proyecto con
/// la misma pareja (Cliente, Nombre) -- esa rama tiene su prueba al final.
/// </summary>
public class ProyectosImportExporterTests
{
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid EstadoId = Guid.NewGuid();

    private static Mock<IClienteRepository> ClientesConAcme()
    {
        var clientes = new Mock<IClienteRepository>();
        clientes.Setup(x => x.FindIdPorNombreAsync("Acme S.A.", It.IsAny<CancellationToken>())).ReturnsAsync(ClienteId);
        clientes.Setup(x => x.FindIdPorNombreAsync(It.Is<string>(n => n != "Acme S.A."), It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        return clientes;
    }

    private static Mock<ICatalogosRepository> CatalogosConEstadoPropuesta()
    {
        var catalogos = new Mock<ICatalogosRepository>();
        catalogos.Setup(x => x.FindEstadoIdPorNombreAsync("Propuesta enviada", It.IsAny<CancellationToken>())).ReturnsAsync(EstadoId);
        catalogos.Setup(x => x.FindEstadoIdPorNombreAsync(It.Is<string>(n => n != "Propuesta enviada"), It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        catalogos.Setup(x => x.GetEstadosAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EstadoProyecto> { new() { Id = EstadoId, Nombre = "Propuesta enviada", Fase = 1, Orden = 1 } });
        return catalogos;
    }

    /// <summary>
    /// Arma el importador con las siete piezas que pide desde docs/35. Por defecto el repositorio de
    /// proyectos no encuentra ninguno con la misma pareja (Cliente, Nombre)
    /// (`FindIdPorClienteYNombreAsync` devuelve null, el valor por defecto de Moq), así que se toma
    /// la rama de CREAR.
    /// </summary>
    private static ProyectosImportExporter Importer(
        Mock<ICrearProyectoUseCase> crear,
        Mock<IClienteRepository> clientes,
        Mock<ICatalogosRepository> catalogos,
        Mock<IProyectoRepository>? proyectos = null,
        Mock<IActualizarProyectoUseCase>? actualizar = null) =>
        new(crear.Object,
            (actualizar ?? new Mock<IActualizarProyectoUseCase>()).Object,
            new CrearProyectoValidator(),
            new ActualizarProyectoValidator(),
            clientes.Object,
            (proyectos ?? new Mock<IProyectoRepository>()).Object,
            catalogos.Object);

    private static Stream LibroConFila(params string?[] valores)
    {
        using var workbook = new XLWorkbook();
        var hoja = workbook.Worksheets.Add("Proyectos");
        string[] columnas = ["Nombre", "Cliente", "Contacto del proyecto", "Tipo de proyecto", "Prioridad", "Ciudad", "Sede Next", "Fecha de solicitud", "Fecha del evento", "Estado", "% de avance", "Estado de la propuesta", "N.º de factura", "Pagado (Sí/No)", "Fecha de pago", "Notas"];
        for (var i = 0; i < columnas.Length; i++) hoja.Cell(1, i + 1).Value = columnas[i];
        for (var i = 0; i < valores.Length; i++) if (valores[i] is not null) hoja.Cell(2, i + 1).Value = valores[i];
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task Importar_crea_una_fila_valida_resolviendo_cliente_y_estado_por_nombre()
    {
        var crear = new Mock<ICrearProyectoUseCase>();
        crear.Setup(x => x.ExecuteAsync(It.IsAny<CrearProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CrearProyectoDto dto, Guid _, string? _, CancellationToken _) => new ProyectoResponseDto { Nombre = dto.Nombre });
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta());

        using var archivo = LibroConFila("Lanzamiento producto", "Acme S.A.", null, null, null, null, null, null, null, "Propuesta enviada", null, null, null, null, null, null);
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(1, resultado.Creados);
        Assert.Empty(resultado.Errores);
        crear.Verify(x => x.ExecuteAsync(It.Is<CrearProyectoDto>(d => d.Nombre == "Lanzamiento producto" && d.ClienteId == ClienteId && d.EstadoId == EstadoId), It.IsAny<Guid>(), "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Importar_reporta_error_cuando_el_estado_no_existe()
    {
        var crear = new Mock<ICrearProyectoUseCase>();
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta());

        using var archivo = LibroConFila("Lanzamiento producto", null, null, null, null, null, null, null, null, "Estado inventado", null, null, null, null, null, null, null);
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(0, resultado.Creados);
        Assert.Single(resultado.Errores);
        Assert.Contains("Estado inventado", resultado.Errores[0].Mensaje);
    }

    [Fact]
    public async Task Importar_reporta_error_cuando_falta_el_estado()
    {
        var crear = new Mock<ICrearProyectoUseCase>();
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta());

        using var archivo = LibroConFila("Lanzamiento producto", null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(0, resultado.Creados);
        Assert.Single(resultado.Errores);
        Assert.Contains("requerido", resultado.Errores[0].Mensaje);
    }

    [Fact]
    public async Task Importar_reporta_error_cuando_el_cliente_nombrado_no_existe()
    {
        var crear = new Mock<ICrearProyectoUseCase>();
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta());

        using var archivo = LibroConFila("Lanzamiento producto", "Cliente inexistente", null, null, null, null, null, null, null, "Propuesta enviada", null, null, null, null, null, null);
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(0, resultado.Creados);
        Assert.Single(resultado.Errores);
        Assert.Contains("Cliente inexistente", resultado.Errores[0].Mensaje);
        crear.Verify(x => x.ExecuteAsync(It.IsAny<CrearProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Importar_interpreta_Si_como_pagado_true()
    {
        var crear = new Mock<ICrearProyectoUseCase>();
        CrearProyectoDto? capturado = null;
        crear.Setup(x => x.ExecuteAsync(It.IsAny<CrearProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<CrearProyectoDto, Guid, string?, CancellationToken>((dto, _, _, _) => capturado = dto)
            .ReturnsAsync((CrearProyectoDto dto, Guid _, string? _, CancellationToken _) => new ProyectoResponseDto { Nombre = dto.Nombre });
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta());

        using var archivo = LibroConFila("Lanzamiento producto", null, null, null, null, null, null, null, null, "Propuesta enviada", null, null, null, "Sí", "2026-09-01", null);
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(1, resultado.Creados);
        Assert.True(capturado!.Pagado);
        Assert.NotNull(capturado.FechaPago);
    }

    [Fact]
    public async Task Exportar_escribe_el_encabezado_resolviendo_nombre_de_cliente_y_estado()
    {
        var clientes = new Mock<IClienteRepository>();
        clientes.Setup(x => x.GetByIdAsync(ClienteId, It.IsAny<CancellationToken>())).ReturnsAsync(new Cliente { Id = ClienteId, Nombre = "Acme S.A." });
        var importer = new ProyectosImportExporter(
            Mock.Of<ICrearProyectoUseCase>(), Mock.Of<IActualizarProyectoUseCase>(),
            Mock.Of<IValidator<CrearProyectoDto>>(), Mock.Of<IValidator<ActualizarProyectoDto>>(),
            clientes.Object, Mock.Of<IProyectoRepository>(), CatalogosConEstadoPropuesta().Object);
        var proyectos = new List<ProyectoResponseDto> { new() { Nombre = "Lanzamiento producto", ClienteId = ClienteId, EstadoId = EstadoId, PropuestaEstado = "No enviada" } };

        var bytes = await importer.ExportarAsync(proyectos);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var hoja = workbook.Worksheet(1);
        Assert.Equal("Lanzamiento producto", hoja.Cell(2, 1).GetString());
        Assert.Equal("Acme S.A.", hoja.Cell(2, 2).GetString());
        Assert.Equal("Propuesta enviada", hoja.Cell(2, 10).GetString());
    }

    // --- Reimportar (docs/35): si el proyecto ya existe (mismo cliente + mismo nombre), la fila lo
    // actualiza en vez de duplicarlo.

    private static readonly Guid ProyectoExistenteId = Guid.NewGuid();

    [Fact]
    public async Task Reimportar_actualiza_el_proyecto_existente_y_conserva_gerente_equipo_y_proveedores()
    {
        var gerenteId = Guid.NewGuid();
        var proveedorId = Guid.NewGuid();
        var existente = new Proyecto
        {
            Id = ProyectoExistenteId,
            Nombre = "Lanzamiento producto",
            ClienteId = ClienteId,
            EstadoId = EstadoId,
            Notas = "Va bien",
            GerenteId = gerenteId,
            // "Ejecutivo" y no "Creativo": los roles válidos del equipo los fija CrearProyectoValidator.
            Equipo = [new ProyectoEquipo { Id = Guid.NewGuid(), Rol = "Ejecutivo", Nombre = "Ana" }],
            Proveedores = [new ProyectoProveedor { ProveedorId = proveedorId }],
        };
        var proyectos = new Mock<IProyectoRepository>();
        proyectos.Setup(x => x.FindIdPorClienteYNombreAsync(ClienteId, "Lanzamiento producto", It.IsAny<CancellationToken>())).ReturnsAsync(ProyectoExistenteId);
        proyectos.Setup(x => x.GetByIdAsync(ProyectoExistenteId, It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var crear = new Mock<ICrearProyectoUseCase>();
        var actualizar = new Mock<IActualizarProyectoUseCase>();
        actualizar.Setup(x => x.ExecuteAsync(It.IsAny<ActualizarProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActualizarProyectoDto dto, Guid _, string? _, CancellationToken _) => new ProyectoResponseDto { Nombre = dto.Nombre });
        var importer = Importer(crear, ClientesConAcme(), CatalogosConEstadoPropuesta(), proyectos, actualizar);

        // Solo cambia el contacto del proyecto; Notas va en blanco y no debe borrarse.
        using var archivo = LibroConFila("Lanzamiento producto", "Acme S.A.", "Ana Ruiz", null, null, null, null, null, null, "Propuesta enviada");
        var resultado = await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        Assert.Equal(0, resultado.Creados);
        Assert.Equal(1, resultado.Actualizados);
        Assert.Empty(resultado.Errores);
        crear.Verify(x => x.ExecuteAsync(It.IsAny<CrearProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        actualizar.Verify(x => x.ExecuteAsync(
            It.Is<ActualizarProyectoDto>(d => d.Id == ProyectoExistenteId
                && d.ContactoProyecto == "Ana Ruiz"
                && d.Notas == "Va bien"
                && d.GerenteId == gerenteId
                && d.Equipo.Count == 1
                && d.ProveedorIds.Count == 1 && d.ProveedorIds[0] == proveedorId),
            It.IsAny<Guid>(), "admin", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Reimportar_con_Pagado_en_blanco_no_revierte_un_proyecto_ya_pagado()
    {
        // Antes de docs/35 una celda vacía se leía como "No" y podía revertir un proyecto ya cobrado.
        var yaPagado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var existente = new Proyecto
        {
            Id = ProyectoExistenteId,
            Nombre = "Lanzamiento producto",
            ClienteId = ClienteId,
            EstadoId = EstadoId,
            Pagado = true,
            FechaPago = yaPagado,
        };
        var proyectos = new Mock<IProyectoRepository>();
        proyectos.Setup(x => x.FindIdPorClienteYNombreAsync(ClienteId, "Lanzamiento producto", It.IsAny<CancellationToken>())).ReturnsAsync(ProyectoExistenteId);
        proyectos.Setup(x => x.GetByIdAsync(ProyectoExistenteId, It.IsAny<CancellationToken>())).ReturnsAsync(existente);

        var actualizar = new Mock<IActualizarProyectoUseCase>();
        actualizar.Setup(x => x.ExecuteAsync(It.IsAny<ActualizarProyectoDto>(), It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActualizarProyectoDto dto, Guid _, string? _, CancellationToken _) => new ProyectoResponseDto { Nombre = dto.Nombre });
        var importer = Importer(new Mock<ICrearProyectoUseCase>(), ClientesConAcme(), CatalogosConEstadoPropuesta(), proyectos, actualizar);

        using var archivo = LibroConFila("Lanzamiento producto", "Acme S.A.", null, null, null, null, null, null, null, "Propuesta enviada");
        await importer.ImportarAsync(archivo, Guid.NewGuid(), "admin");

        actualizar.Verify(x => x.ExecuteAsync(
            It.Is<ActualizarProyectoDto>(d => d.Pagado && d.FechaPago == yaPagado),
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
