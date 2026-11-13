using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pedidos.Api.Http;

/// <summary>
/// O SQL Server devolve <c>Quantidade * PrecoUnitario</c> com escala 4 (ex.: 399.0000).
/// Todo decimal da API é dinheiro com 2 casas, então normalizamos na saída.
/// </summary>
internal sealed class DinheiroJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDecimal();

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(decimal.Round(value, 2, MidpointRounding.AwayFromZero));
}
