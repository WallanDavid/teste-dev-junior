using Pedidos.Domain.Entities;

namespace Pedidos.UnitTests;

internal static class Builders
{
    /// <summary>Cria um produto com Id definido (o Id normalmente é gerado pelo banco).</summary>
    public static Produto Produto(int id, decimal preco = 10m, int estoque = 100, string? nome = null)
    {
        var produto = new Produto(nome ?? $"Produto {id}", preco, estoque);
        typeof(Produto).GetProperty("Id")!.SetValue(produto, id);
        return produto;
    }

    public static Cliente Cliente(int id, string nome = "Cliente Teste")
    {
        var cliente = new Cliente(nome, $"cliente{id}@teste.com");
        typeof(Cliente).GetProperty("Id")!.SetValue(cliente, id);
        return cliente;
    }

    public static List<ItemSolicitado> Itens(params (int ProdutoId, int Quantidade)[] itens) =>
        itens.Select(i => new ItemSolicitado(i.ProdutoId, i.Quantidade)).ToList();
}
