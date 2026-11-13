using System.Net;
using System.Net.Http.Json;
using Pedidos.Application.Pedidos;
using Pedidos.IntegrationTests.Infrastructure;

namespace Pedidos.IntegrationTests;

[Collection(ApiCollection.Nome)]
public class PedidoEndpointsTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static object Pedido(int clienteId, params (int ProdutoId, int Quantidade)[] itens) => new
    {
        clienteId,
        itens = itens.Select(i => new { produtoId = i.ProdutoId, quantidade = i.Quantidade }),
    };

    // ---------------------------------------------------------------- POST /pedidos

    [Fact] // CA01 + CA08
    public async Task Post_PedidoValido_Retorna201ComLocationGravaEBaixaEstoque()
    {
        var cliente = await factory.CriarClienteAsync("Ana Integração");
        var teclado = await factory.CriarProdutoAsync(preco: 459.90m, estoque: 10, nome: "Teclado");
        var mouse = await factory.CriarProdutoAsync(preco: 129.90m, estoque: 5, nome: "Mouse");

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id, (teclado.Id, 2), (mouse.Id, 1)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var criado = await response.Content.ReadFromJsonAsync<PedidoDto>();
        Assert.NotNull(criado);
        Assert.Equal($"/pedidos/{criado.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Ana Integração", criado.ClienteNome);
        Assert.Equal(2 * 459.90m + 129.90m, criado.ValorTotal);
        Assert.Equal(459.90m, criado.Itens.Single(i => i.ProdutoId == teclado.Id).PrecoUnitario);

        Assert.Equal(8, await factory.ObterEstoqueAsync(teclado.Id));
        Assert.Equal(4, await factory.ObterEstoqueAsync(mouse.Id));

        var lido = await _client.GetFromJsonAsync<PedidoDto>($"/pedidos/{criado.Id}");
        Assert.Equal(criado.ValorTotal, lido!.ValorTotal);
        Assert.Equal(2, lido.Itens.Count);
    }

    [Fact] // CA02
    public async Task Post_SemItens_Retorna400ENaoGrava()
    {
        var cliente = await factory.CriarClienteAsync();

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("itens", await response.LerCamposInvalidosAsync());
        Assert.Equal(0, await factory.ContarPedidosDoClienteAsync(cliente.Id));
    }

    [Theory] // CA03
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Post_QuantidadeInvalida_Retorna400ComCampoDoItem(int quantidade)
    {
        var cliente = await factory.CriarClienteAsync();
        var produto = await factory.CriarProdutoAsync();

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id, (produto.Id, quantidade)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("itens[0].quantidade", await response.LerCamposInvalidosAsync());
    }

    [Fact]
    public async Task Post_JsonMalformado_Retorna400()
    {
        var response = await _client.PostAsync("/pedidos", new StringContent("{ \"clienteId\": ", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact] // CA04
    public async Task Post_ClienteInexistente_Retorna422()
    {
        var produto = await factory.CriarProdutoAsync(estoque: 5);

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(999_999, (produto.Id, 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("cliente_nao_encontrado", await response.LerCodigoDeErroAsync());
        Assert.Equal(5, await factory.ObterEstoqueAsync(produto.Id));
    }

    [Fact] // CA05
    public async Task Post_ProdutoInexistente_Retorna422()
    {
        var cliente = await factory.CriarClienteAsync();

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id, (999_999, 1)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("produto_nao_encontrado", await response.LerCodigoDeErroAsync());
    }

    [Fact] // CA06
    public async Task Post_EstoqueInsuficiente_Retorna422EMantemEstoqueDeTodosOsProdutos()
    {
        var cliente = await factory.CriarClienteAsync();
        var comEstoque = await factory.CriarProdutoAsync(estoque: 10);
        var semEstoque = await factory.CriarProdutoAsync(estoque: 5);

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id, (comEstoque.Id, 2), (semEstoque.Id, 6)));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("estoque_insuficiente", await response.LerCodigoDeErroAsync());
        Assert.Equal(10, await factory.ObterEstoqueAsync(comEstoque.Id));
        Assert.Equal(5, await factory.ObterEstoqueAsync(semEstoque.Id));
        Assert.Equal(0, await factory.ContarPedidosDoClienteAsync(cliente.Id));
    }

    [Fact] // CA07
    public async Task Post_ProdutoRepetido_GravaUmUnicoItemComQuantidadeSomada()
    {
        var cliente = await factory.CriarClienteAsync();
        var produto = await factory.CriarProdutoAsync(estoque: 10);

        var response = await _client.PostAsJsonAsync("/pedidos", Pedido(cliente.Id, (produto.Id, 2), (produto.Id, 3)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = Assert.Single((await response.Content.ReadFromJsonAsync<PedidoDto>())!.Itens);
        Assert.Equal(5, item.Quantidade);
        Assert.Equal(5, await factory.ObterEstoqueAsync(produto.Id));
    }

    [Fact] // RN10
    public async Task Post_PedidosConcorrentesPeloUltimoItem_ApenasUmEhGravado()
    {
        var produto = await factory.CriarProdutoAsync(estoque: 1);
        var clientes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => factory.CriarClienteAsync()));

        var respostas = await Task.WhenAll(clientes.Select(c =>
            factory.CreateClient().PostAsJsonAsync("/pedidos", Pedido(c.Id, (produto.Id, 1)))));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(
            respostas.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
        Assert.Equal(0, await factory.ObterEstoqueAsync(produto.Id));
        Assert.Equal(1, (await Task.WhenAll(clientes.Select(c => factory.ContarPedidosDoClienteAsync(c.Id)))).Sum());
    }

    [Fact] // RN10 — regressão: o e2e paralelo pegou 409 com estoque sobrando (concorrência otimista)
    public async Task Post_PedidosConcorrentesComEstoqueSobrando_TodosSaoGravados()
    {
        var produto = await factory.CriarProdutoAsync(estoque: 100);
        var clientes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => factory.CriarClienteAsync()));
        // 5 × 2 = 10 unidades de um estoque de 100: nenhum pedido deveria falhar.

        var respostas = await Task.WhenAll(clientes.Select(c =>
            factory.CreateClient().PostAsJsonAsync("/pedidos", Pedido(c.Id, (produto.Id, 2)))));

        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(90, await factory.ObterEstoqueAsync(produto.Id));
    }

    // ---------------------------------------------------------------- GET /pedidos

    [Fact] // CA09 + CA10
    public async Task Get_FiltroPorCliente_RetornaSoPedidosDoClienteDoMaisRecenteParaOMaisAntigoPaginados()
    {
        var cliente = await factory.CriarClienteAsync();
        var outro = await factory.CriarClienteAsync();
        var produto = await factory.CriarProdutoAsync(estoque: 1000);
        var ids = new List<int>();
        for (var dia = 1; dia <= 12; dia++)
            ids.Add(await factory.CriarPedidoAsync(cliente.Id, new DateTime(2025, 3, dia, 12, 0, 0, DateTimeKind.Utc), (produto.Id, 1)));
        await factory.CriarPedidoAsync(outro.Id, new DateTime(2025, 3, 5, 12, 0, 0, DateTimeKind.Utc), (produto.Id, 1));

        var pagina1 = await _client.GetFromJsonAsync<PaginaResultado<PedidoDto>>($"/pedidos?clienteId={cliente.Id}");
        var pagina2 = await _client.GetFromJsonAsync<PaginaResultado<PedidoDto>>($"/pedidos?clienteId={cliente.Id}&pagina=2");

        Assert.Equal(12, pagina1!.TotalItens);
        Assert.Equal(2, pagina1.TotalPaginas);
        Assert.Equal(10, pagina1.Itens.Count);
        Assert.Equal(2, pagina2!.Itens.Count);
        Assert.All(pagina1.Itens.Concat(pagina2.Itens), p => Assert.Equal(cliente.Id, p.ClienteId));
        Assert.Equal(ids.AsEnumerable().Reverse(), pagina1.Itens.Concat(pagina2.Itens).Select(p => p.Id));
    }

    [Fact] // CA11
    public async Task Get_FiltroPorPeriodo_IncluiPedidosDoUltimoDiaInteiro()
    {
        var cliente = await factory.CriarClienteAsync();
        var produto = await factory.CriarProdutoAsync(estoque: 100);
        await factory.CriarPedidoAsync(cliente.Id, new DateTime(2025, 6, 9, 23, 59, 59, DateTimeKind.Utc), (produto.Id, 1));  // antes
        var noInicio = await factory.CriarPedidoAsync(cliente.Id, new DateTime(2025, 6, 10, 0, 0, 0, DateTimeKind.Utc), (produto.Id, 1));
        var noFimDoUltimoDia = await factory.CriarPedidoAsync(cliente.Id, new DateTime(2025, 6, 20, 23, 59, 59, 999, DateTimeKind.Utc), (produto.Id, 1));
        await factory.CriarPedidoAsync(cliente.Id, new DateTime(2025, 6, 21, 0, 0, 0, DateTimeKind.Utc), (produto.Id, 1));   // depois

        var resultado = await _client.GetFromJsonAsync<PaginaResultado<PedidoDto>>(
            $"/pedidos?clienteId={cliente.Id}&dataInicio=2025-06-10&dataFim=2025-06-20");

        Assert.Equal(new[] { noFimDoUltimoDia, noInicio }, resultado!.Itens.Select(p => p.Id));
        Assert.All(resultado.Itens, p => Assert.Equal(DateTimeKind.Utc, p.DataPedido.Kind));
    }

    [Theory] // CA12
    [InlineData("tamanhoPagina=0", "tamanhoPagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("dataInicio=2026-02-01&dataFim=2026-01-01", "dataInicio")]
    public async Task Get_ParametrosInvalidos_Retorna400(string query, string campo)
    {
        var response = await _client.GetAsync($"/pedidos?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(campo, await response.LerCamposInvalidosAsync());
    }

    [Fact]
    public async Task Get_DataEmFormatoInvalido_Retorna400()
    {
        var response = await _client.GetAsync("/pedidos?dataInicio=31-12-2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetPorId_Inexistente_Retorna404()
    {
        var response = await _client.GetAsync("/pedidos/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("pedido_nao_encontrado", await response.LerCodigoDeErroAsync());
    }
}
