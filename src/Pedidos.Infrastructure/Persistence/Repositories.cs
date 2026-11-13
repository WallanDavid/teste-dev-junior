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
    // Rastreados: os itens do pedido referenciam estas instâncias (navegação ItemPedido.Produto).
    public async Task<IReadOnlyList<Produto>> ObterPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        await db.Produtos.Where(p => ids.Contains(p.Id)).ToListAsync(ct);
}

internal sealed class PedidoRepository(PedidosDbContext db) : IPedidoRepository
{
    public async Task SalvarAsync(Pedido pedido, CancellationToken ct)
    {
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        // Baixa atômica: o próprio UPDATE confere o estoque. Dois pedidos simultâneos do mesmo produto
        // serializam no lock da linha; o segundo só "perde" se realmente não houver mais estoque.
        foreach (var item in pedido.Itens.OrderBy(i => i.ProdutoId)) // ordem fixa evita deadlock entre pedidos
        {
            var afetadas = await db.Produtos
                .Where(p => p.Id == item.ProdutoId && p.Estoque >= item.Quantidade)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Estoque, p => p.Estoque - item.Quantidade), ct);

            if (afetadas == 0)
                throw new EstoqueEsgotadoException(item.ProdutoId); // dispose da transação = rollback
        }

        // O domínio já subtraiu o estoque nas instâncias em memória; o UPDATE acima é a fonte da verdade,
        // então o EF não deve gerar um segundo UPDATE com os valores (possivelmente desatualizados) dela.
        foreach (var produto in db.ChangeTracker.Entries<Produto>())
            produto.State = EntityState.Unchanged;

        db.Pedidos.Add(pedido);
        await db.SaveChangesAsync(ct);
        await transacao.CommitAsync(ct);
    }
}
