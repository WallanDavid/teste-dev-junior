using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Pedidos.Application.Abstractions;
using Pedidos.Application.Pedidos;
using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;
using static Pedidos.UnitTests.Builders;

namespace Pedidos.UnitTests.Application;

public class PedidoServiceTests
{
    private static readonly DateTimeOffset Agora = new(2026, 11, 13, 15, 30, 0, TimeSpan.Zero);

    private readonly IClienteRepository _clientes = Substitute.For<IClienteRepository>();
    private readonly IProdutoRepository _produtos = Substitute.For<IProdutoRepository>();
    private readonly IPedidoRepository _pedidos = Substitute.For<IPedidoRepository>();
    private readonly IPedidoQueries _queries = Substitute.For<IPedidoQueries>();
    private readonly PedidoService _service;

    public PedidoServiceTests()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(Agora);
        _service = new PedidoService(_clientes, _produtos, _pedidos, _queries, clock);
    }

    private static CriarPedidoRequest Request(int clienteId, params (int ProdutoId, int Quantidade)[] itens) =>
        new(clienteId, itens.Select(i => new ItemPedidoRequest(i.ProdutoId, i.Quantidade)).ToList());

    private void DadoCliente(int id, string nome = "Ana") =>
        _clientes.ObterPorIdAsync(id, Arg.Any<CancellationToken>()).Returns(Cliente(id, nome));

    private void DadosProdutos(params Produto[] produtos) =>
        _produtos.ObterPorIdsAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(produtos);

    // ------------------------------------------------------------------ CriarAsync

    [Fact]
    public async Task CriarAsync_SemItens_RetornaValidacaoSemConsultarBanco()
    {
        var resultado = await _service.CriarAsync(new CriarPedidoRequest(1, []), default);

        Assert.Equal(ErrorType.Validacao, Assert.Single(resultado.Errors).Type);
        await _clientes.DidNotReceiveWithAnyArgs().ObterPorIdAsync(default, default);
        await _pedidos.DidNotReceiveWithAnyArgs().SalvarAsync(default!, default);
    }

    [Fact]
    public async Task CriarAsync_ItensNulos_RetornaValidacao()
    {
        var resultado = await _service.CriarAsync(new CriarPedidoRequest(1, null), default);

        Assert.Equal("itens", Assert.Single(resultado.Errors).Campo);
    }

    [Fact]
    public async Task CriarAsync_ClienteInexistente_RetornaClienteNaoEncontradoENaoSalva()
    {
        _clientes.ObterPorIdAsync(42, Arg.Any<CancellationToken>()).Returns((Cliente?)null);

        var resultado = await _service.CriarAsync(Request(42, (1, 1)), default);

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal("cliente_nao_encontrado", erro.Code);
        Assert.Equal(ErrorType.Regra, erro.Type);
        await _pedidos.DidNotReceiveWithAnyArgs().SalvarAsync(default!, default);
    }

    [Fact]
    public async Task CriarAsync_ProdutoInexistente_RetornaProdutoNaoEncontrado()
    {
        DadoCliente(1);
        DadosProdutos(Produto(1));

        var resultado = await _service.CriarAsync(Request(1, (1, 1), (2, 1)), default);

        Assert.Equal("produto_nao_encontrado", Assert.Single(resultado.Errors).Code);
        await _pedidos.DidNotReceiveWithAnyArgs().SalvarAsync(default!, default);
    }

    [Fact]
    public async Task CriarAsync_EstoqueInsuficiente_RetornaErroENaoSalva()
    {
        DadoCliente(1);
        DadosProdutos(Produto(1, estoque: 2));

        var resultado = await _service.CriarAsync(Request(1, (1, 3)), default);

        Assert.Equal("estoque_insuficiente", Assert.Single(resultado.Errors).Code);
        await _pedidos.DidNotReceiveWithAnyArgs().SalvarAsync(default!, default);
    }

    [Fact]
    public async Task CriarAsync_ProdutoRepetido_ConsultaCadaProdutoUmaVez()
    {
        DadoCliente(1);
        DadosProdutos(Produto(1, estoque: 10));

        await _service.CriarAsync(Request(1, (1, 2), (1, 3)), default);

        await _produtos.Received(1).ObterPorIdsAsync(
            Arg.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 1 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CriarAsync_Valido_AdicionaSalvaERetornaPedidoComDataDoServidor()
    {
        DadoCliente(1, "Ana Souza");
        DadosProdutos(Produto(1, preco: 100m, estoque: 5, nome: "Monitor"));

        var resultado = await _service.CriarAsync(Request(1, (1, 2)), default);

        Assert.True(resultado.IsSuccess);
        var dto = resultado.Value;
        Assert.Equal("Ana Souza", dto.ClienteNome);
        Assert.Equal(200m, dto.ValorTotal);
        Assert.Equal(Agora.UtcDateTime, dto.DataPedido);
        var item = Assert.Single(dto.Itens);
        Assert.Equal("Monitor", item.ProdutoNome);
        Assert.Equal(200m, item.Subtotal);

        await _pedidos.Received(1).SalvarAsync(
            Arg.Is<Pedido>(p => p.ClienteId == 1 && p.Itens.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact] // RN10: outro pedido levou o último item entre a validação e a gravação
    public async Task CriarAsync_EstoqueEsgotaDuranteAGravacao_RetornaEstoqueInsuficienteComNomeDoProduto()
    {
        DadoCliente(1);
        DadosProdutos(Produto(1, estoque: 1, nome: "Mesa digitalizadora"));
        _pedidos.SalvarAsync(Arg.Any<Pedido>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EstoqueEsgotadoException(1));

        var resultado = await _service.CriarAsync(Request(1, (1, 1)), default);

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal("estoque_insuficiente", erro.Code);
        Assert.Equal(ErrorType.Regra, erro.Type);
        Assert.Contains("Mesa digitalizadora", erro.Message);
    }

    // ------------------------------------------------------------------ ListarAsync

    [Theory]
    [InlineData(0, 10, "pagina")]
    [InlineData(1, 0, "tamanhoPagina")]
    [InlineData(1, 101, "tamanhoPagina")]
    public async Task ListarAsync_PaginacaoInvalida_RetornaValidacao(int pagina, int tamanho, string campo)
    {
        var filtro = new FiltroPedidosRequest { Pagina = pagina, TamanhoPagina = tamanho };

        var resultado = await _service.ListarAsync(filtro, default);

        Assert.Equal(campo, Assert.Single(resultado.Errors).Campo);
        await _queries.DidNotReceiveWithAnyArgs().ListarAsync(default!, default);
    }

    [Fact]
    public async Task ListarAsync_DataInicioMaiorQueDataFim_RetornaValidacao()
    {
        var filtro = new FiltroPedidosRequest { DataInicio = new(2026, 5, 2), DataFim = new(2026, 5, 1) };

        var resultado = await _service.ListarAsync(filtro, default);

        Assert.Equal(Errors.Filtro.PeriodoInvalido, Assert.Single(resultado.Errors));
    }

    [Fact]
    public async Task ListarAsync_SemFiltros_UsaPadroes()
    {
        await _service.ListarAsync(new FiltroPedidosRequest(), default);

        await _queries.Received(1).ListarAsync(
            new ConsultaPedidos(null, null, null, 1, PedidoService.TamanhoPaginaPadrao),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListarAsync_ComPeriodo_ConverteParaIntervaloSemiabertoIncluindoUltimoDia()
    {
        var filtro = new FiltroPedidosRequest
        {
            ClienteId = 3, DataInicio = new(2026, 1, 1), DataFim = new(2026, 1, 31), Pagina = 2, TamanhoPagina = 20,
        };

        await _service.ListarAsync(filtro, default);

        await _queries.Received(1).ListarAsync(
            new ConsultaPedidos(
                3,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                2,
                20),
            Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ ObterPorIdAsync

    [Fact]
    public async Task ObterPorIdAsync_Inexistente_RetornaNaoEncontrado()
    {
        _queries.ObterPorIdAsync(9, Arg.Any<CancellationToken>()).Returns((PedidoDto?)null);

        var resultado = await _service.ObterPorIdAsync(9, default);

        Assert.Equal(ErrorType.NaoEncontrado, Assert.Single(resultado.Errors).Type);
    }
}
