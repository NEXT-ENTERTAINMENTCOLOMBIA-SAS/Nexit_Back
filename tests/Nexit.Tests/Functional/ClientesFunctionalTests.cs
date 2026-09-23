using System.Net;
using System.Net.Http.Json;
using Nexit.Application.DTOs.Clientes;

namespace Nexit.Tests.Functional;

/// <summary>
/// Pruebas funcionales de clientes (ver docs/08-tipos-de-pruebas.md) -- de extremo a extremo contra
/// Postgres real: la petición HTTP crea la fila de verdad, y se verifica leyéndola de vuelta con otra
/// petición HTTP independiente (no inspeccionando el objeto en memoria que devolvió el POST).
/// </summary>
public class ClientesFunctionalTests(NexitFunctionalApiFactory factory) : FunctionalTestBase(factory)
{
    [Fact]
    public async Task Crear_actualizar_y_eliminar_un_cliente_persiste_de_verdad_en_postgres()
    {
        var client = ClientAs("admin");
        var nombre = $"Cliente funcional {Guid.NewGuid():N}";

        var createResponse = await client.PostAsJsonAsync("/api/clientes", new CreateClienteDto { Nombre = nombre, Telefonos = [new ClienteTelefonoDto { Telefono = "3000000000" }] });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var creado = await createResponse.Content.ReadFromJsonAsync<ClienteResponseDto>();
        Assert.NotNull(creado);

        // Se relee con OTRA petición HTTP -- confirma que quedó de verdad en la base, no solo en la respuesta del POST.
        var getResponse = await client.GetAsync($"/api/clientes/{creado!.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var leido = await getResponse.Content.ReadFromJsonAsync<ClienteResponseDto>();
        Assert.Equal(nombre, leido!.Nombre);

        var updateResponse = await client.PutAsJsonAsync($"/api/clientes/{creado.Id}", new UpdateClienteDto { Id = creado.Id, Nombre = nombre + " (actualizado)", Telefonos = [new ClienteTelefonoDto { Telefono = "3000000000" }] });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var actualizado = await (await client.GetAsync($"/api/clientes/{creado.Id}")).Content.ReadFromJsonAsync<ClienteResponseDto>();
        Assert.Equal(nombre + " (actualizado)", actualizado!.Nombre);

        // Borrar es SuperAdminOnly desde el 2026-09-18 (Alicia corrigió la decisión: antes también
        // podían manager/admin/director) -- crear/editar arriba sigue probándose como "admin" porque
        // esos dos pasos siguen sin restricción de rol (ver ClientesController).
        var superAdmin = ClientAs("super_admin");
        var deleteResponse = await superAdmin.DeleteAsync($"/api/clientes/{creado.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var afterDelete = await client.GetAsync($"/api/clientes/{creado.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_crear_dos_clientes_con_el_mismo_email_en_la_base_real()
    {
        var client = ClientAs("admin");
        var email = $"{Guid.NewGuid():N}@nexit-test.com";
        var primero = await client.PostAsJsonAsync("/api/clientes", new CreateClienteDto { Nombre = "Cliente A", Emails = [new ClienteEmailDto { Email = email }], Telefonos = [new ClienteTelefonoDto { Telefono = "3000000000" }] });
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var segundo = await client.PostAsJsonAsync("/api/clientes", new CreateClienteDto { Nombre = "Cliente B", Emails = [new ClienteEmailDto { Email = email }], Telefonos = [new ClienteTelefonoDto { Telefono = "3000000001" }] });
        Assert.Equal(HttpStatusCode.BadRequest, segundo.StatusCode);
    }
}
