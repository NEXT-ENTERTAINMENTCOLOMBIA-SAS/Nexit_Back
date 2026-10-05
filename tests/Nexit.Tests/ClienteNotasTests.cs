using Moq;
using Nexit.Application.DTOs.Clientes;
using Nexit.Application.UseCases.Clientes;
using Nexit.Core.Entities;
using Nexit.Core.Exceptions;
using Nexit.Core.Interfaces;

namespace Nexit.Tests;

/// <summary>Notas internas de cliente: agregar, listar y borrar (autor o admin).</summary>
public class ClienteNotasTests
{
    private readonly Mock<IClienteRepository> _clientes = new();
    private readonly Mock<IClienteNotaRepository> _notas = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private static readonly Guid ClienteId = Guid.NewGuid();
    private static readonly Guid Autor = Guid.NewGuid();

    private ClienteNotasUseCase Caso()
    {
        _clientes.Setup(x => x.GetByIdAsync(ClienteId, It.IsAny<CancellationToken>())).ReturnsAsync(new Cliente { Id = ClienteId, Nombre = "Acme" });
        return new ClienteNotasUseCase(_clientes.Object, _notas.Object, _uow.Object);
    }

    [Fact]
    public async Task Agregar_guarda_la_nota_con_autor_y_area_y_devuelve_el_nombre_del_autor()
    {
        ClienteNota? guardada = null;
        _notas.Setup(x => x.AddAsync(It.IsAny<ClienteNota>(), It.IsAny<CancellationToken>())).Callback<ClienteNota, CancellationToken>((n, _) => guardada = n).Returns(Task.CompletedTask);
        _notas.Setup(x => x.GetByClienteIdAsync(ClienteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [new ClienteNota { Id = guardada!.Id, ClienteId = ClienteId, AutorId = Autor, Area = guardada.Area, Nota = guardada.Nota, Autor = new Usuario { Nombre = "Ana", Apellido = "Ruiz" } }]);

        var dto = await Caso().AgregarAsync(ClienteId, new CrearClienteNotaDto { Area = " Comercial ", Nota = "  Pidió cotización  " }, Autor);

        Assert.Equal("Comercial", dto.Area);
        Assert.Equal("Pidió cotización", dto.Nota);
        Assert.Equal("Ana Ruiz", dto.AutorNombre);
        _uow.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Agregar_a_un_cliente_inexistente_lanza_EntityNotFound()
    {
        _clientes.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Cliente?)null);
        var caso = new ClienteNotasUseCase(_clientes.Object, _notas.Object, _uow.Object);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => caso.AgregarAsync(Guid.NewGuid(), new CrearClienteNotaDto { Nota = "x" }, Autor));
    }

    [Theory]
    [InlineData("miembro", false)]
    [InlineData("admin", true)]
    [InlineData("super_admin", true)]
    public async Task Eliminar_una_nota_ajena_solo_lo_puede_hacer_un_admin(string rol, bool permitido)
    {
        var nota = new ClienteNota { Id = Guid.NewGuid(), ClienteId = ClienteId, AutorId = Guid.NewGuid() };
        _notas.Setup(x => x.GetByIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);

        var tarea = Caso().EliminarAsync(ClienteId, nota.Id, Autor, rol);

        if (permitido) { await tarea; _notas.Verify(x => x.DeleteAsync(nota.Id, It.IsAny<CancellationToken>()), Times.Once); }
        else await Assert.ThrowsAsync<BusinessRuleException>(() => tarea);
    }

    [Fact]
    public async Task El_autor_puede_borrar_su_propia_nota()
    {
        var nota = new ClienteNota { Id = Guid.NewGuid(), ClienteId = ClienteId, AutorId = Autor };
        _notas.Setup(x => x.GetByIdAsync(nota.Id, It.IsAny<CancellationToken>())).ReturnsAsync(nota);
        await Caso().EliminarAsync(ClienteId, nota.Id, Autor, "miembro");
        _notas.Verify(x => x.DeleteAsync(nota.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Validador_rechaza_nota_vacia_y_acepta_una_normal()
    {
        var v = new CrearClienteNotaValidator();
        Assert.False((await v.ValidateAsync(new CrearClienteNotaDto { Nota = "  " })).IsValid);
        Assert.True((await v.ValidateAsync(new CrearClienteNotaDto { Nota = "ok" })).IsValid);
    }
}
