namespace Pedidos.Domain.Entities;

public class ItemPedido
{
    // Construtor para o EF Core.
    private ItemPedido() { }

    internal ItemPedido(Produto produto, int quantidade)
    {
        Produto = produto;
        ProdutoId = produto.Id;
        Quantidade = quantidade;
        PrecoUnitario = produto.Preco; // RN07: preço congelado no momento do pedido
    }

    public int Id { get; private set; }
    public int PedidoId { get; private set; }
    public int ProdutoId { get; private set; }
    public Produto? Produto { get; private set; }
    public int Quantidade { get; private set; }
    public decimal PrecoUnitario { get; private set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;
}
