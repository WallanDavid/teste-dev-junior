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
    void Adicionar(Pedido pedido);

    /// <summary>Persiste pedido + baixa de estoque numa transação.</summary>
    /// <exception cref="ConcorrenciaException">Outro processo alterou o estoque no meio do caminho.</exception>
    Task SalvarAlteracoesAsync(CancellationToken ct);
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

/// <summary>Conflito de concorrência otimista ao salvar (ex.: estoque alterado por outro pedido).</summary>
public sealed class ConcorrenciaException(string message, Exception? inner = null) : Exception(message, inner);
