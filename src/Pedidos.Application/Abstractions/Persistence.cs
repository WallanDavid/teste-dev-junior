using Pedidos.Application.Catalogo;
using Pedidos.Application.Pedidos;
using Pedidos.Application.Relatorios;
using Pedidos.Domain.Entities;

namespace Pedidos.Application.Abstractions;

// Lado de escrita: trabalha com entidades rastreadas.

public interface IClienteRepository
{
    Task<Cliente?> ObterPorIdAsync(int id, CancellationToken ct);
}

public interface IProdutoRepository
{
    /// <summary>Retorna apenas os produtos encontrados (ids inexistentes são ignorados).</summary>
    Task<IReadOnlyList<Produto>> ObterPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct);
}

public interface IPedidoRepository
{
    /// <summary>
    /// Grava o pedido e baixa o estoque de cada item numa única transação. A baixa é um UPDATE
    /// condicional (<c>WHERE Estoque &gt;= @quantidade</c>), atômico mesmo com pedidos simultâneos.
    /// </summary>
    /// <exception cref="EstoqueEsgotadoException">O estoque acabou entre a validação e a gravação; nada foi gravado.</exception>
    Task SalvarAsync(Pedido pedido, CancellationToken ct);
}

// Lado de leitura: projeções direto para DTO, sem rastreamento.

public interface IPedidoQueries
{
    Task<PaginaResultado<PedidoDto>> ListarAsync(ConsultaPedidos consulta, CancellationToken ct);
    Task<PedidoDto?> ObterPorIdAsync(int id, CancellationToken ct);
}

public interface IRelatorioQueries
{
    /// <param name="inicioUtc">Inclusivo.</param>
    /// <param name="fimExclusivoUtc">Exclusivo (semiaberto, para manter o predicado sargável).</param>
    Task<IReadOnlyList<TopClienteDto>> TopClientesAsync(DateTime inicioUtc, DateTime fimExclusivoUtc, int limite, CancellationToken ct);
}

public interface ICatalogoQueries
{
    Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct);
    Task<IReadOnlyList<ProdutoDto>> ListarProdutosAsync(CancellationToken ct);
}

/// <summary>Outro pedido consumiu o estoque entre a validação e a gravação (corrida pelo último item).</summary>
public sealed class EstoqueEsgotadoException(int produtoId)
    : Exception($"Estoque do produto {produtoId} esgotou durante a gravação do pedido.")
{
    public int ProdutoId { get; } = produtoId;
}
