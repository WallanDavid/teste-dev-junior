using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstractions;
using Pedidos.Domain.Entities;

namespace Pedidos.Infrastructure.Persistence;

internal sealed class ClienteRepository(PedidosDbContext db) : IClienteRepository
{
    public Task<Cliente?> ObterPorIdAsync(int id, CancellationToken ct) =>
        db.Clientes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
}

internal sealed class ProdutoRepository(PedidosDbContext db) : IProdutoRepository
{
    // Rastreados: o estoque será alterado e o RowVersion carregado aqui é o usado na checagem de concorrência.
    public async Task<IReadOnlyList<Produto>> ObterPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Produtos.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
}

internal sealed class PedidoRepository(PedidosDbContext db) : IPedidoRepository
{
    public void Adicionar(Pedido pedido) => db.Pedidos.Add(pedido);

    public async Task SalvarAlteracoesAsync(CancellationToken ct)
    {
        try
        {
            // SaveChanges já envolve INSERT do pedido/itens e UPDATE do estoque numa única transação.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcorrenciaException("O estoque foi alterado por outra transação.", ex);
        }
    }
}
