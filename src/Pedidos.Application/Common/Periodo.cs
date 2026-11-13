namespace Pedidos.Application.Common;

/// <summary>
/// Converte datas inclusivas da API em intervalo UTC semiaberto [inicio, fim + 1 dia).
/// Assim o banco recebe um predicado sargável (<c>DataPedido &gt;= @ini AND DataPedido &lt; @fim</c>)
/// e o último dia é incluído inteiro, independente da precisão da coluna.
/// </summary>
public static class Periodo
{
    public static DateTime? InicioUtc(DateOnly? data) =>
        data?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public static DateTime? FimExclusivoUtc(DateOnly? data) =>
        data?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}
