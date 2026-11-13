using Pedidos.Api.Http;
using Pedidos.Application.Pedidos;

namespace Pedidos.Api.Endpoints;

public static class PedidoEndpoints
{
    public static IEndpointRouteBuilder MapPedidoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/pedidos").WithTags("Pedidos");

        group.MapPost("/", async (CriarPedidoRequest request, PedidoService service, CancellationToken ct) =>
                (await service.CriarAsync(request, ct)).ToCreatedResult(p => $"/pedidos/{p.Id}"))
            .WithName("CriarPedido")
            .WithSummary("Cria um pedido com itens, validando cliente e estoque")
            .Produces<PedidoDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", async ([AsParameters] FiltroPedidosRequest filtro, PedidoService service, CancellationToken ct) =>
                (await service.ListarAsync(filtro, ct)).ToHttpResult())
            .WithName("ListarPedidos")
            .WithSummary("Lista pedidos com filtros por cliente e período (datas inclusivas, UTC) e paginação")
            .Produces<PaginaResultado<PedidoDto>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:int}", async (int id, PedidoService service, CancellationToken ct) =>
                (await service.ObterPorIdAsync(id, ct)).ToHttpResult())
            .WithName("ObterPedido")
            .WithSummary("Obtém um pedido pelo id")
            .Produces<PedidoDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
