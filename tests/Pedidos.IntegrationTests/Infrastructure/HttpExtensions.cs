using System.Net.Http.Json;
using System.Text.Json;

namespace Pedidos.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    /// <summary>Lê a propriedade "code" do ProblemDetails.</summary>
    public static async Task<string?> LerCodigoDeErroAsync(this HttpResponseMessage response)
    {
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    /// <summary>Lê os campos de "errors" de um ValidationProblemDetails.</summary>
    public static async Task<string[]> LerCamposInvalidosAsync(this HttpResponseMessage response)
    {
        using var doc = await response.Content.ReadFromJsonAsync<JsonDocument>();
        return doc!.RootElement.TryGetProperty("errors", out var errors)
            ? errors.EnumerateObject().Select(p => p.Name).ToArray()
            : [];
    }
}
