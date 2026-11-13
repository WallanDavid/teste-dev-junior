using Pedidos.Api.Http;
using Pedidos.Application.Relatorios;

namespace Pedidos.Api.Endpoints;

public static class RelatorioEndpoints
{
    public static IEndpointRouteBuilder MapRelatorioEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/relatorios").WithTags("Relatórios");

        group.MapGet("/top-clientes", async ([AsParameters] FiltroPeriodoRequest filtro, RelatorioService service, CancellationToken ct) =>
                (await service.TopClientesAsync(filtro, ct)).ToHttpResult())
            .WithName("TopClientes")
            .WithSummary("Top 5 clientes por valor total de pedidos no período (datas inclusivas, obrigatórias)")
            .Produces<IReadOnlyList<TopClienteDto>>()
            .ProducesValidationProblem();

        return app;
    }
}
