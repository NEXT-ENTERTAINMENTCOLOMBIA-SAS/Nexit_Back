using System.Net;
using System.Net.Http.Json;
using Nexit.Application.DTOs.Clientes;

namespace Nexit.Tests.Functional;

/// <summary>Notas internas de cliente de extremo a extremo contra Postgres real.</summary>
public class ClienteNotasFunctionalTests(NexitFunctionalApiFactory factory) : FunctionalTestBase(factory)
{
    [Fact]
    public async Task Agregar_listar_y_borrar_notas_de_un_cliente_persiste_de_verdad()
    {
        var admin = ClientAs("admin", UsuarioSembradoId("admin"));
        var creado = await (await admin.PostAsJsonAsync("/api/clientes", new CreateClienteDto { Nombre = $"Cliente notas {Guid.NewGuid():N}", Telefonos = [new ClienteTelefonoDto { Telefono = "3000000000" }] }))
            .Content.ReadFromJsonAsync<ClienteResponseDto>();

        var miembro = ClientAs("miembro", UsuarioSembradoId("miembro"));
        var post = await miembro.PostAsJsonAsync($"/api/clientes/{creado!.Id}/notas", new CrearClienteNotaDto { Area = "Comercial", Nota = "Pidió cotización para marzo" });
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var nota = await post.Content.ReadFromJsonAsync<ClienteNotaDto>();
        Assert.False(string.IsNullOrWhiteSpace(nota!.AutorNombre));

        // Se relee con otra petición: quedó en la base y cualquier rol la ve.
        var lista = await admin.GetFromJsonAsync<List<ClienteNotaDto>>($"/api/clientes/{creado.Id}/notas");
        var leida = Assert.Single(lista!);
        Assert.Equal("Comercial", leida.Area);
        Assert.Equal("Pidió cotización para marzo", leida.Nota);

        // Una nota vacía se rechaza.
        var vacia = await miembro.PostAsJsonAsync($"/api/clientes/{creado.Id}/notas", new CrearClienteNotaDto { Nota = "  " });
        Assert.Equal(HttpStatusCode.BadRequest, vacia.StatusCode);

        // Otro miembro no puede borrarla; su autor sí.
        var otro = ClientAs("manager", UsuarioSembradoId("manager"));
        Assert.Equal(HttpStatusCode.Conflict, (await otro.DeleteAsync($"/api/clientes/{creado.Id}/notas/{nota.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await miembro.DeleteAsync($"/api/clientes/{creado.Id}/notas/{nota.Id}")).StatusCode);
        Assert.Empty((await admin.GetFromJsonAsync<List<ClienteNotaDto>>($"/api/clientes/{creado.Id}/notas"))!);
    }
}
