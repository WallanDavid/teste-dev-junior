namespace Pedidos.Domain.Entities;

public class Produto
{
    // Construtor para o EF Core.
    private Produto() { }

    public Produto(string nome, decimal preco, int estoque)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(preco);
        ArgumentOutOfRangeException.ThrowIfNegative(estoque);
        Nome = nome;
        Preco = preco;
        Estoque = estoque;
    }

    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public decimal Preco { get; private set; }
    public int Estoque { get; private set; }

    /// <summary>Token de concorrência otimista (coluna ROWVERSION).</summary>
    public byte[] RowVersion { get; private set; } = [];

    public bool PossuiEstoque(int quantidade) => Estoque >= quantidade;

    /// <summary>
    /// Baixa o estoque. Chamado apenas por <see cref="Pedido.Criar"/>, que já validou a quantidade;
    /// por isso uma violação aqui é erro de programação (exceção), não regra de negócio.
    /// </summary>
    internal void BaixarEstoque(int quantidade)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantidade);
        if (!PossuiEstoque(quantidade))
            throw new InvalidOperationException($"Estoque insuficiente para o produto {Id}.");

        Estoque -= quantidade;
    }
}
