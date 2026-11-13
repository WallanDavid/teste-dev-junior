using Pedidos.Application.Abstractions;
using Pedidos.Application.Common;
using Pedidos.Domain.Common;

namespace Pedidos.Application.Relatorios;

public sealed record FiltroPeriodoRequest
{
    public DateOnly? DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
}

public sealed record TopClienteDto(int ClienteId, string ClienteNome, int QuantidadePedidos, decimal ValorTotal);

public sealed class RelatorioService(IRelatorioQueries queries)
{
    public const int LimiteTopClientes = 5;

    public async Task<Result<IReadOnlyList<TopClienteDto>>> TopClientesAsync(FiltroPeriodoRequest filtro, CancellationToken ct)
    {
        var erros = new List<Error>();
        if (filtro.DataInicio is null)
            erros.Add(Errors.Filtro.DataInicioObrigatoria);
        if (filtro.DataFim is null)
            erros.Add(Errors.Filtro.DataFimObrigatoria);
        if (filtro.DataInicio > filtro.DataFim)
            erros.Add(Errors.Filtro.PeriodoInvalido);

        if (erros.Count > 0)
            return Result<IReadOnlyList<TopClienteDto>>.Failure(erros);

        var ranking = await queries.TopClientesAsync(
            Periodo.InicioUtc(filtro.DataInicio)!.Value,
            Periodo.FimExclusivoUtc(filtro.DataFim)!.Value,
            LimiteTopClientes,
            ct);

        return Result<IReadOnlyList<TopClienteDto>>.Success(ranking);
    }
}
