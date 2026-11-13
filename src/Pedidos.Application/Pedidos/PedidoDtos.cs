using Pedidos.Domain.Entities;

namespace Pedidos.Application.Pedidos;

public sealed record CriarPedidoRequest(int ClienteId, IReadOnlyList<ItemPedidoRequest>? Itens);

public sealed record ItemPedidoRequest(int ProdutoId, int Quantidade);

/// <summary>Query string de GET /pedidos. Tudo opcional; padrões aplicados no service.</summary>
public sealed class FiltroPedidosRequest
{
    public int? ClienteId { get; set; }
    public DateOnly? DataInicio { get; set; }
    public DateOnly? DataFim { get; set; }
    public int? Pagina { get; set; }
    public int? TamanhoPagina { get; set; }
}

/// <summary>Filtro já validado e convertido para intervalo UTC semiaberto [InicioUtc, FimExclusivoUtc).</summary>
public sealed record ConsultaPedidos(
    int? ClienteId,
    DateTime? InicioUtc,
    DateTime? FimExclusivoUtc,
    int Pagina,
    int TamanhoPagina);

public sealed record ItemPedidoDto(int ProdutoId, string ProdutoNome, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public sealed record PedidoDto(
    int Id,
    int ClienteId,
    string ClienteNome,
    DateTime DataPedido,
    decimal ValorTotal,
    IReadOnlyList<ItemPedidoDto> Itens)
{
    public static PedidoDto De(Pedido pedido, Cliente cliente) => new(
        pedido.Id,
        cliente.Id,
        cliente.Nome,
        pedido.DataPedido,
        pedido.ValorTotal,
        pedido.Itens
            .Select(i => new ItemPedidoDto(i.ProdutoId, i.Produto?.Nome ?? string.Empty, i.Quantidade, i.PrecoUnitario, i.Subtotal))
            .ToList());
}

public sealed record PaginaResultado<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int TotalItens)
{
    public int TotalPaginas => TotalItens == 0 ? 0 : (int)Math.Ceiling(TotalItens / (double)TamanhoPagina);
}
