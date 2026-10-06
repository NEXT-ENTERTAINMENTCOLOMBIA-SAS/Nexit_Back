using System.Net;

namespace Nexit.Tests.Integration;

/// <summary>El chequeo de salud debe responder sin token (lo usa el healthcheck de la plataforma).</summary>
public class HealthIntegrationTests(NexitApiFactory factory) : IClassFixture<NexitApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Health_responde_200_sin_token()
    {
        var response = await _client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"ok\"", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HealthReady_no_exige_token_ni_devuelve_401()
    {
        var response = await _client.GetAsync("/health/ready");
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
