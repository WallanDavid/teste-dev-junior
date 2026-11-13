using Pedidos.Application.Abstractions;
using Pedidos.Application.Catalogo;

namespace Pedidos.Api.Endpoints;

/// <summary>Endpoints de apoio para o frontend (selects de cliente e produto).</summary>
public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/clientes", async (ICatalogoQueries queries, CancellationToken ct) =>
                Results.Ok(await queries.ListarClientesAsync(ct)))
            .WithTags("Catálogo")
            .WithName("ListarClientes")
            .Produces<IReadOnlyList<ClienteDto>>();

        app.MapGet("/produtos", async (ICatalogoQueries queries, CancellationToken ct) =>
                Results.Ok(await queries.ListarProdutosAsync(ct)))
            .WithTags("Catálogo")
            .WithName("ListarProdutos")
            .Produces<IReadOnlyList<ProdutoDto>>();

        return app;
    }
}
