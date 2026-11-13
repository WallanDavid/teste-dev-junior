using Pedidos.Domain.Common;

namespace Pedidos.Domain.Entities;

/// <summary>Linha da requisição de criação de pedido (antes de qualquer validação).</summary>
public sealed record ItemSolicitado(int ProdutoId, int Quantidade);

public class Pedido
{
    private readonly List<ItemPedido> _itens = [];

    // Construtor para o EF Core.
    private Pedido() { }

    private Pedido(int clienteId, DateTime dataPedidoUtc)
    {
        ClienteId = clienteId;
        DataPedido = dataPedidoUtc;
    }

    public int Id { get; private set; }
    public int ClienteId { get; private set; }
    public Cliente? Cliente { get; private set; }
    public DateTime DataPedido { get; private set; }
    public IReadOnlyCollection<ItemPedido> Itens => _itens.AsReadOnly();

    public decimal ValorTotal => _itens.Sum(i => i.Subtotal);

    /// <summary>
    /// Validações que dependem só da requisição (RN01, RN04). Ficam separadas de <see cref="Criar"/>
    /// para que o caso de uso possa rejeitar entrada malformada antes de ir ao banco.
    /// </summary>
    public static Result ValidarEntrada(int clienteId, IReadOnlyList<ItemSolicitado>? itens)
    {
        var erros = new List<Error>();

        if (clienteId <= 0)
            erros.Add(Errors.Pedido.ClienteIdInvalido);

        if (itens is null || itens.Count == 0)
        {
            erros.Add(Errors.Pedido.SemItens);
            return Result.Failure(erros);
        }

        if (itens.Count > Errors.Pedido.MaxItens)
            erros.Add(Errors.Pedido.ItensDemais);

        for (var i = 0; i < itens.Count; i++)
        {
            if (itens[i].ProdutoId <= 0)
                erros.Add(Errors.Pedido.ProdutoIdInvalido(i));

            if (itens[i].Quantidade is <= 0 or > Errors.Pedido.MaxQuantidadePorItem)
                erros.Add(Errors.Pedido.QuantidadeInvalida(i));
        }

        return erros.Count == 0 ? Result.Success() : Result.Failure(erros);
    }

    /// <summary>
    /// Cria o pedido aplicando RN01, RN03–RN08. A existência do cliente (RN02) é verificada
    /// pelo caso de uso, que precisa consultar o repositório.
    /// </summary>
    /// <param name="produtos">Produtos encontrados no banco para os ids solicitados.</param>
    /// <remarks>
    /// Atômico: se qualquer item falhar, nenhum estoque é alterado (as validações rodam todas antes da baixa).
    /// </remarks>
    public static Result<Pedido> Criar(
        int clienteId,
        IReadOnlyList<ItemSolicitado> itens,
        IReadOnlyCollection<Produto> produtos,
        DateTime dataPedidoUtc)
    {
        var entrada = ValidarEntrada(clienteId, itens);
        if (entrada.IsFailure)
            return Result<Pedido>.Failure(entrada.Errors);

        var porId = produtos.ToDictionary(p => p.Id);

        // RN08: mesmo produto em várias linhas vira um único item com a soma das quantidades.
        // Max 100 linhas × 10.000 = 1.000.000, sem risco de overflow.
        var agrupados = itens
            .GroupBy(i => i.ProdutoId)
            .Select(g => (ProdutoId: g.Key, Quantidade: g.Sum(i => i.Quantidade)))
            .ToList();

        var erros = new List<Error>();
        foreach (var (produtoId, quantidade) in agrupados)
        {
            if (!porId.TryGetValue(produtoId, out var produto))
                erros.Add(Errors.Pedido.ProdutoNaoEncontrado(produtoId));              // RN03
            else if (!produto.PossuiEstoque(quantidade))
                erros.Add(Errors.Pedido.EstoqueInsuficiente(produto.Nome, produto.Estoque, quantidade)); // RN05
        }

        if (erros.Count > 0)
            return Result<Pedido>.Failure(erros);

        var pedido = new Pedido(clienteId, DateTime.SpecifyKind(dataPedidoUtc, DateTimeKind.Utc));
        foreach (var (produtoId, quantidade) in agrupados)
        {
            var produto = porId[produtoId];
            produto.BaixarEstoque(quantidade);                    // RN06
            pedido._itens.Add(new ItemPedido(produto, quantidade)); // RN07
        }

        return pedido;
    }
}
