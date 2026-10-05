using Moq;
using Nexit.Application.DTOs.Proyectos;
using Nexit.Application.UseCases.Proyectos;
using Nexit.Application.Validators.Proyectos;
using Nexit.Core.Constants;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Tests;

public class ProyectosTests
{
    [Fact]
    public async Task CrearProyecto_persists_project_with_team_and_providers()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var clientes = new Mock<IClienteRepository>();
        var proveedores = new Mock<IProveedorRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var clienteId = Guid.NewGuid();
        var estadoId = Guid.NewGuid();
        var proveedorId = Guid.NewGuid();
        clientes.Setup(x => x.GetByIdAsync(clienteId, It.IsAny<CancellationToken>())).ReturnsAsync(new Cliente { Id = clienteId });
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });
        proveedores.Setup(x => x.GetByIdAsync(proveedorId, It.IsAny<CancellationToken>())).ReturnsAsync(new Proveedor { Id = proveedorId });

        var result = await new CrearProyectoUseCase(proyectos.Object, clientes.Object, proveedores.Object, catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>()).ExecuteAsync(
            new CrearProyectoDto { Nombre = "Lanzamiento", ClienteId = clienteId, EstadoId = estadoId, Equipo = [new ProyectoEquipoDto { Nombre = "Ana", Rol = "Ejecutivo" }], ProveedorIds = [proveedorId] }, Guid.NewGuid(), Roles.Admin);

        Assert.Equal("Lanzamiento", result.Nombre);
        proyectos.Verify(x => x.AddAsync(It.Is<Proyecto>(p => p.Equipo.Count == 1 && p.Proveedores.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CrearProyecto_rejects_unknown_state()
    {
        var estadoId = Guid.NewGuid();
        var catalogos = new Mock<ICatalogosRepository>();
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync((EstadoProyecto?)null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => new CrearProyectoUseCase(Mock.Of<IProyectoRepository>(), Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new CrearProyectoDto { Nombre = "Lanzamiento", EstadoId = estadoId }, Guid.NewGuid(), Roles.Admin));
    }

    [Fact]
    public async Task AgregarSeguimiento_assigns_author_and_saves()
    {
        var proyectoId = Guid.NewGuid();
        var autorId = Guid.NewGuid();
        var proyectos = new Mock<IProyectoRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        proyectos.Setup(x => x.GetByIdAsync(proyectoId, It.IsAny<CancellationToken>())).ReturnsAsync(new Proyecto { Id = proyectoId });

        var result = await new AgregarSeguimientoProyectoUseCase(proyectos.Object, unitOfWork.Object).ExecuteAsync(proyectoId, new CrearSeguimientoProyectoDto { Nota = "Proveedor confirmado" }, autorId);

        Assert.Equal(autorId, result.AutorId);
        Assert.Equal("General", result.Area);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActualizarProyecto_registers_who_made_the_edit()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var proyectoId = Guid.NewGuid();
        var editorId = Guid.NewGuid();
        var estadoId = Guid.NewGuid();
        var proyecto = new Proyecto { Id = proyectoId, Nombre = "Lanzamiento" };
        proyectos.Setup(x => x.GetByIdAsync(proyectoId, It.IsAny<CancellationToken>())).ReturnsAsync(proyecto);
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });

        await new ActualizarProyectoUseCase(proyectos.Object, Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), unitOfWork.Object)
            .ExecuteAsync(new ActualizarProyectoDto { Id = proyectoId, Nombre = "Lanzamiento Renovado", EstadoId = estadoId }, editorId, Roles.Admin);

        Assert.Equal(editorId, proyecto.UpdatedBy);
        Assert.NotNull(proyecto.UpdatedAt);
    }

    [Fact]
    public async Task CrearProyecto_by_a_manager_auto_assigns_them_as_the_owning_gerente()
    {
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoId = Guid.NewGuid();
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });
        var gerenteId = Guid.NewGuid();

        var result = await new CrearProyectoUseCase(Mock.Of<IProyectoRepository>(), Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new CrearProyectoDto { Nombre = "Lanzamiento", EstadoId = estadoId }, gerenteId, Roles.Manager);

        Assert.Equal(gerenteId, result.GerenteId);
    }

    [Fact]
    public async Task CrearProyecto_by_a_miembro_leaves_it_without_an_owning_gerente()
    {
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoId = Guid.NewGuid();
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });

        var result = await new CrearProyectoUseCase(Mock.Of<IProyectoRepository>(), Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new CrearProyectoDto { Nombre = "Lanzamiento", EstadoId = estadoId }, Guid.NewGuid(), Roles.Miembro);

        Assert.Null(result.GerenteId);
    }

    [Fact]
    public async Task CrearProyecto_by_an_admin_honors_the_explicit_gerente_assignment()
    {
        var catalogos = new Mock<ICatalogosRepository>();
        var estadoId = Guid.NewGuid();
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });
        var elegidoGerenteId = Guid.NewGuid();

        var result = await new CrearProyectoUseCase(Mock.Of<IProyectoRepository>(), Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new CrearProyectoDto { Nombre = "Lanzamiento", EstadoId = estadoId, GerenteId = elegidoGerenteId }, Guid.NewGuid(), Roles.Admin);

        Assert.Equal(elegidoGerenteId, result.GerenteId);
    }

    [Fact]
    public async Task ActualizarProyecto_rejects_a_manager_reassigning_the_owning_gerente()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var proyectoId = Guid.NewGuid();
        var estadoId = Guid.NewGuid();
        var proyecto = new Proyecto { Id = proyectoId, Nombre = "Lanzamiento", GerenteId = Guid.NewGuid() };
        proyectos.Setup(x => x.GetByIdAsync(proyectoId, It.IsAny<CancellationToken>())).ReturnsAsync(proyecto);
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });

        await Assert.ThrowsAsync<ForbiddenOperationException>(() => new ActualizarProyectoUseCase(proyectos.Object, Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), Mock.Of<IUnitOfWork>())
            .ExecuteAsync(new ActualizarProyectoDto { Id = proyectoId, Nombre = "Lanzamiento", EstadoId = estadoId, GerenteId = Guid.NewGuid() }, Guid.NewGuid(), Roles.Manager));
    }

    [Fact]
    public async Task ActualizarProyecto_allows_an_admin_to_reassign_the_owning_gerente()
    {
        var proyectos = new Mock<IProyectoRepository>();
        var catalogos = new Mock<ICatalogosRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        var proyectoId = Guid.NewGuid();
        var estadoId = Guid.NewGuid();
        var nuevoGerenteId = Guid.NewGuid();
        var proyecto = new Proyecto { Id = proyectoId, Nombre = "Lanzamiento", GerenteId = Guid.NewGuid() };
        proyectos.Setup(x => x.GetByIdAsync(proyectoId, It.IsAny<CancellationToken>())).ReturnsAsync(proyecto);
        catalogos.Setup(x => x.GetEstadoAsync(estadoId, It.IsAny<CancellationToken>())).ReturnsAsync(new EstadoProyecto { Id = estadoId });

        var result = await new ActualizarProyectoUseCase(proyectos.Object, Mock.Of<IClienteRepository>(), Mock.Of<IProveedorRepository>(), catalogos.Object, Mock.Of<IHistorialCambioRepository>(), unitOfWork.Object)
            .ExecuteAsync(new ActualizarProyectoDto { Id = proyectoId, Nombre = "Lanzamiento", EstadoId = estadoId, GerenteId = nuevoGerenteId }, Guid.NewGuid(), Roles.Admin);

        Assert.Equal(nuevoGerenteId, result.GerenteId);
    }

    [Fact]
    public async Task ConsultarSeguimiento_returns_the_full_log_newest_first()
    {
        // Bug real encontrado al conectar el frontend (2026-08-26): antes solo existía el POST
        // para agregar una entrada nueva a la bitácora, sin ningún GET para listar las que ya había.
        var proyectos = new Mock<IProyectoRepository>();
        var proyectoId = Guid.NewGuid();
        var proyecto = new Proyecto
        {
            Id = proyectoId,
            Nombre = "Lanzamiento",
            Seguimiento =
            [
                new ProyectoSeguimiento { ProyectoId = proyectoId, Area = "Logística", Nota = "Primera nota", Fecha = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc) },
                new ProyectoSeguimiento { ProyectoId = proyectoId, Area = "Producción", Nota = "Nota más reciente", Fecha = new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc) },
            ],
        };
        proyectos.Setup(x => x.GetByIdAsync(proyectoId, It.IsAny<CancellationToken>())).ReturnsAsync(proyecto);

        var result = await new ConsultarSeguimientoProyectoUseCase(proyectos.Object).ExecuteAsync(proyectoId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Nota más reciente", result[0].Nota);
        Assert.Equal("Primera nota", result[1].Nota);
    }

    [Fact]
    public async Task ConsultarSeguimiento_throws_when_the_project_does_not_exist()
    {
        var proyectos = new Mock<IProyectoRepository>();
        proyectos.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Proyecto?)null);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => new ConsultarSeguimientoProyectoUseCase(proyectos.Object).ExecuteAsync(Guid.NewGuid()));
    }

    // -- Agregar un miembro al equipo ahora es solo buscarlo por nombre (Alicia 2026-09-09): el
    // "Rol" funcional (Ejecutivo/Comercial/...) dejó de pedirse en el flujo nuevo, así que el
    // validador tiene que aceptar un Rol vacío sin rechazar el proyecto.
    [Fact]
    public void CrearProyectoValidator_allows_a_team_member_with_no_functional_role()
    {
        var dto = new CrearProyectoDto
        {
            Nombre = "Lanzamiento Q4",
            EstadoId = Guid.NewGuid(),
            PorcentajeAvance = 0,
            PropuestaEstado = "No enviada",
            Equipo = [new ProyectoEquipoDto { Nombre = "Juliana Pérez", Rol = "" }],
        };

        var result = new CrearProyectoValidator().Validate(dto);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CrearProyectoValidator_accepts_a_free_text_cargo()
    {
        var dto = new CrearProyectoDto
        {
            Nombre = "Lanzamiento Q4",
            EstadoId = Guid.NewGuid(),
            PorcentajeAvance = 0,
            PropuestaEstado = "No enviada",
            Equipo = [new ProyectoEquipoDto { Nombre = "Juliana Pérez", Rol = "Producción" }],
        };

        var result = new CrearProyectoValidator().Validate(dto);

        Assert.True(result.IsValid);
    }
}
