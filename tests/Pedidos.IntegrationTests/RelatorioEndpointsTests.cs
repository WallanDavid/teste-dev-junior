using System.Net;
using System.Net.Http.Json;
using Pedidos.Application.Catalogo;
using Pedidos.Application.Relatorios;
using Pedidos.IntegrationTests.Infrastructure;

namespace Pedidos.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class RelatorioEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // Cada teste usa um ano exclusivo para não ser afetado por pedidos de outros testes.

    [Fact] // CA13
    public async Task TopClientes_RetornaNoMaximo5OrdenadosPorValorDecrescenteSoDentroDoPeriodo()
    {
        var produto = await factory.CriarProdutoAsync(preco: 10m, estoque: 10_000);
        var clientes = new List<int>();
        // Cliente i compra i unidades (valor 10*i) em 2001 -> ranking esperado: 6, 5, 4, 3, 2
        for (var i = 1; i <= 6; i++)
        {
            var c = await factory.CriarClienteAsync($"Ranking {i}");
            clientes.Add(c.Id);
            await factory.CriarPedidoAsync(c.Id, new DateTime(2001, 6, i, 10, 0, 0, DateTimeKind.Utc), (produto.Id, i));
        }
        // Fora do período: deixaria o cliente 1 em primeiro se fosse contado.
        await factory.CriarPedidoAsync(clientes[0], new DateTime(2002, 1, 1, 0, 0, 0, DateTimeKind.Utc), (produto.Id, 500));
        // Segundo pedido do cliente 2 no período: 20 + 50 = 70 -> passa para o topo.
        await factory.CriarPedidoAsync(clientes[1], new DateTime(2001, 12, 31, 23, 0, 0, DateTimeKind.Utc), (produto.Id, 5));

        var ranking = await _client.GetFromJsonAsync<List<TopClienteDto>>(
            "/relatorios/top-clientes?dataInicio=2001-01-01&dataFim=2001-12-31");

        Assert.Equal(5, ranking!.Count);
        Assert.Equal(new[] { clientes[1], clientes[5], clientes[4], clientes[3], clientes[2] }, ranking.Select(r => r.ClienteId));
        Assert.Equal(70m, ranking[0].ValorTotal);
        Assert.Equal(2, ranking[0].QuantidadePedidos);
        Assert.DoesNotContain(ranking, r => r.ClienteId == clientes[0]);
    }

    [Fact] // CA14
    public async Task TopClientes_ClientesHomonimos_NaoSaoSomadosJuntos()
    {
        var produto = await factory.CriarProdutoAsync(preco: 100m, estoque: 1000);
        var joao1 = await factory.CriarClienteAsync("João Homônimo");
        var joao2 = await factory.CriarClienteAsync("João Homônimo");
        await factory.CriarPedidoAsync(joao1.Id, new DateTime(2003, 3, 1, 0, 0, 0, DateTimeKind.Utc), (produto.Id, 1));
        await factory.CriarPedidoAsync(joao2.Id, new DateTime(2003, 3, 2, 0, 0, 0, DateTimeKind.Utc), (produto.Id, 2));

        var ranking = await _client.GetFromJsonAsync<List<TopClienteDto>>(
            "/relatorios/top-clientes?dataInicio=2003-01-01&dataFim=2003-12-31");

        Assert.Equal(2, ranking!.Count);
        Assert.Equal((joao2.Id, 200m), (ranking[0].ClienteId, ranking[0].ValorTotal));
        Assert.Equal((joao1.Id, 100m), (ranking[1].ClienteId, ranking[1].ValorTotal));
    }

    [Fact]
    public async Task TopClientes_PeriodoSemPedidos_RetornaListaVazia()
    {
        var ranking = await _client.GetFromJsonAsync<List<TopClienteDto>>(
            "/relatorios/top-clientes?dataInicio=1990-01-01&dataFim=1990-12-31");

        Assert.Empty(ranking!);
    }

    [Theory] // CA15
    [InlineData("")]
    [InlineData("?dataInicio=2026-01-01")]
    [InlineData("?dataFim=2026-01-01")]
    [InlineData("?dataInicio=2026-12-31&dataFim=2026-01-01")]
    public async Task TopClientes_PeriodoAusenteOuInvalido_Retorna400(string query)
    {
        var response = await _client.GetAsync($"/relatorios/top-clientes{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("validacao", await response.LerCodigoDeErroAsync());
    }

    [Fact]
    public async Task Catalogo_ListaClientesEProdutosCriados()
    {
        var cliente = await factory.CriarClienteAsync("Catálogo Cliente");
        var produto = await factory.CriarProdutoAsync(preco: 1.99m, estoque: 7, nome: "Catálogo Produto");

        var clientes = await _client.GetFromJsonAsync<List<ClienteDto>>("/clientes");
        var produtos = await _client.GetFromJsonAsync<List<ProdutoDto>>("/produtos");

        Assert.Contains(clientes!, c => c.Id == cliente.Id && c.Nome == "Catálogo Cliente");
        Assert.Contains(produtos!, p => p.Id == produto.Id && p.Preco == 1.99m && p.Estoque == 7);
    }

    [Fact]
    public async Task Health_BancoNoAr_Retorna200()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
