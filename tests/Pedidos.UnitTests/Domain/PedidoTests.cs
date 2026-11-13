using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;
using static Pedidos.UnitTests.Builders;

namespace Pedidos.UnitTests.Domain;

public class PedidoTests
{
    private static readonly DateTime Agora = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Criar_SemItens_RetornaErroDeValidacao()
    {
        var resultado = Pedido.Criar(1, Itens(), [Produto(1)], Agora);

        Assert.True(resultado.IsFailure);
        var erro = Assert.Single(resultado.Errors);
        Assert.Equal(ErrorType.Validacao, erro.Type);
        Assert.Equal("itens", erro.Campo);
    }

    [Fact]
    public void ValidarEntrada_ItensNulos_RetornaErroDeValidacao()
    {
        var resultado = Pedido.ValidarEntrada(1, null);

        Assert.Contains(Errors.Pedido.SemItens, resultado.Errors);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Errors.Pedido.MaxQuantidadePorItem + 1)]
    public void Criar_QuantidadeForaDaFaixa_RetornaErroDeValidacaoNoCampoDoItem(int quantidade)
    {
        var resultado = Pedido.Criar(1, Itens((1, 2), (2, quantidade)), [Produto(1), Produto(2)], Agora);

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal(ErrorType.Validacao, erro.Type);
        Assert.Equal("itens[1].quantidade", erro.Campo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void ValidarEntrada_ClienteIdInvalido_RetornaErroDeValidacao(int clienteId)
    {
        var resultado = Pedido.ValidarEntrada(clienteId, Itens((1, 1)));

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal("clienteId", erro.Campo);
    }

    [Fact]
    public void ValidarEntrada_VariosErros_RetornaTodos()
    {
        var resultado = Pedido.ValidarEntrada(0, Itens((0, 1), (2, 0)));

        Assert.Equal(3, resultado.Errors.Count);
    }

    [Fact]
    public void ValidarEntrada_MaisItensQueOLimite_RetornaErro()
    {
        var itens = Enumerable.Range(1, Errors.Pedido.MaxItens + 1).Select(i => new ItemSolicitado(i, 1)).ToList();

        var resultado = Pedido.ValidarEntrada(1, itens);

        Assert.Contains(Errors.Pedido.ItensDemais, resultado.Errors);
    }

    [Fact]
    public void Criar_ProdutoInexistente_RetornaProdutoNaoEncontrado()
    {
        var resultado = Pedido.Criar(1, Itens((1, 1), (99, 1)), [Produto(1)], Agora);

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal("produto_nao_encontrado", erro.Code);
        Assert.Equal(ErrorType.Regra, erro.Type);
    }

    [Fact]
    public void Criar_EstoqueInsuficiente_RetornaErroComDisponivelESolicitado()
    {
        var resultado = Pedido.Criar(1, Itens((1, 6)), [Produto(1, estoque: 5, nome: "Teclado")], Agora);

        var erro = Assert.Single(resultado.Errors);
        Assert.Equal("estoque_insuficiente", erro.Code);
        Assert.Contains("Teclado", erro.Message);
        Assert.Contains("disponível 5", erro.Message);
        Assert.Contains("solicitado 6", erro.Message);
    }

    [Fact]
    public void Criar_UmItemSemEstoque_NaoBaixaEstoqueDeNenhumProduto()
    {
        var comEstoque = Produto(1, estoque: 10);
        var semEstoque = Produto(2, estoque: 1);

        var resultado = Pedido.Criar(1, Itens((1, 3), (2, 2)), [comEstoque, semEstoque], Agora);

        Assert.True(resultado.IsFailure);
        Assert.Equal(10, comEstoque.Estoque);
        Assert.Equal(1, semEstoque.Estoque);
    }

    [Fact]
    public void Criar_EstoqueExatamenteIgualAQuantidade_Permite()
    {
        var produto = Produto(1, estoque: 5);

        var resultado = Pedido.Criar(1, Itens((1, 5)), [produto], Agora);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(0, produto.Estoque);
    }

    [Fact]
    public void Criar_ProdutoRepetido_SomaQuantidadesEmUmUnicoItem()
    {
        var produto = Produto(1, estoque: 10);

        var resultado = Pedido.Criar(1, Itens((1, 2), (1, 3)), [produto], Agora);

        var item = Assert.Single(resultado.Value.Itens);
        Assert.Equal(5, item.Quantidade);
        Assert.Equal(5, produto.Estoque);
    }

    [Fact]
    public void Criar_ProdutoRepetidoCujaSomaExcedeOEstoque_RetornaEstoqueInsuficiente()
    {
        // Cada linha isolada caberia no estoque (3 <= 4), mas a soma (6) não.
        var resultado = Pedido.Criar(1, Itens((1, 3), (1, 3)), [Produto(1, estoque: 4)], Agora);

        Assert.Equal("estoque_insuficiente", Assert.Single(resultado.Errors).Code);
    }

    [Fact]
    public void Criar_Valido_CopiaPrecoDoProdutoEBaixaEstoque()
    {
        var teclado = Produto(1, preco: 459.90m, estoque: 10);
        var mouse = Produto(2, preco: 129.90m, estoque: 3);

        var resultado = Pedido.Criar(7, Itens((1, 2), (2, 3)), [teclado, mouse], Agora);

        Assert.True(resultado.IsSuccess);
        var pedido = resultado.Value;
        Assert.Equal(7, pedido.ClienteId);
        Assert.Equal(Agora, pedido.DataPedido);
        Assert.Equal(DateTimeKind.Utc, pedido.DataPedido.Kind);
        Assert.Equal(459.90m, pedido.Itens.Single(i => i.ProdutoId == 1).PrecoUnitario);
        Assert.Equal(129.90m, pedido.Itens.Single(i => i.ProdutoId == 2).PrecoUnitario);
        Assert.Equal(8, teclado.Estoque);
        Assert.Equal(0, mouse.Estoque);
    }

    [Fact]
    public void Criar_Valido_CalculaValorTotalPelaSomaDosSubtotais()
    {
        var resultado = Pedido.Criar(1, Itens((1, 2), (2, 3)), [Produto(1, preco: 10.50m), Produto(2, preco: 0.99m)], Agora);

        Assert.Equal(2 * 10.50m + 3 * 0.99m, resultado.Value.ValorTotal);
    }
}
