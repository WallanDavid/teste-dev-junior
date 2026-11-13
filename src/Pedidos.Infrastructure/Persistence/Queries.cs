using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstractions;
using Pedidos.Application.Catalogo;
using Pedidos.Application.Pedidos;
using Pedidos.Application.Relatorios;
using Pedidos.Domain.Entities;

namespace Pedidos.Infrastructure.Persistence;

internal sealed class PedidoQueries(PedidosDbContext db) : IPedidoQueries
{
    public async Task<PaginaResultado<PedidoDto>> ListarAsync(ConsultaPedidos consulta, CancellationToken ct)
    {
        var query = db.Pedidos.AsNoTracking();

        // Predicados sargáveis: comparam a coluna "nua" com parâmetros (usam IX_Pedidos_*).
        if (consulta.ClienteId is { } clienteId)
            query = query.Where(p => p.ClienteId == clienteId);
        if (consulta.InicioUtc is { } inicio)
            query = query.Where(p => p.DataPedido >= inicio);
        if (consulta.FimExclusivoUtc is { } fim)
            query = query.Where(p => p.DataPedido < fim);

        var total = await query.CountAsync(ct);

        var itens = total == 0
            ? []
            : await Projetar(query
                    .OrderByDescending(p => p.DataPedido)
                    .ThenByDescending(p => p.Id) // desempate estável entre páginas
                    .Skip((consulta.Pagina - 1) * consulta.TamanhoPagina)
                    .Take(consulta.TamanhoPagina))
                .ToListAsync(ct);

        return new PaginaResultado<PedidoDto>(itens, consulta.Pagina, consulta.TamanhoPagina, total);
    }

    public Task<PedidoDto?> ObterPorIdAsync(int id, CancellationToken ct) =>
        Projetar(db.Pedidos.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct);

    // Projeção direta para DTO: o SQL traz só as colunas necessárias, sem materializar entidades.
    private static IQueryable<PedidoDto> Projetar(IQueryable<Pedido> query) =>
        query.Select(p => new PedidoDto(
            p.Id,
            p.ClienteId,
            p.Cliente!.Nome,
            p.DataPedido,
            p.Itens.Sum(i => i.Quantidade * i.PrecoUnitario),
            p.Itens
                .OrderBy(i => i.Id)
                .Select(i => new ItemPedidoDto(
                    i.ProdutoId,
                    i.Produto!.Nome,
                    i.Quantidade,
                    i.PrecoUnitario,
                    i.Quantidade * i.PrecoUnitario))
                .ToList()));
}

internal sealed class RelatorioQueries(PedidosDbContext db) : IRelatorioQueries
{
    // Mesma lógica da query corrigida da Parte 4 (database/debug/04-query-top-clientes.sql):
    // INNER JOIN, filtro de data sargável, agregação por ClienteId (não por nome), ordem decrescente.
    public async Task<IReadOnlyList<TopClienteDto>> TopClientesAsync(
        DateTime inicioUtc, DateTime fimExclusivoUtc, int limite, CancellationToken ct)
    {
        var linhas = await db.Database.SqlQuery<TopClienteLinha>($"""
            WITH TotaisPorCliente AS (
                SELECT p.ClienteId,
                       COUNT(DISTINCT p.Id)                AS QuantidadePedidos,
                       SUM(i.Quantidade * i.PrecoUnitario) AS ValorTotal
                FROM dbo.Pedidos p
                JOIN dbo.ItensPedido i ON i.PedidoId = p.Id
                WHERE p.DataPedido >= {inicioUtc} AND p.DataPedido < {fimExclusivoUtc}
                GROUP BY p.ClienteId
            )
            SELECT TOP ({limite})
                   c.Id   AS ClienteId,
                   c.Nome AS ClienteNome,
                   t.QuantidadePedidos,
                   t.ValorTotal
            FROM TotaisPorCliente t
            JOIN dbo.Clientes c ON c.Id = t.ClienteId
            ORDER BY t.ValorTotal DESC, c.Id
            """).ToListAsync(ct);

        return linhas.Select(l => new TopClienteDto(l.ClienteId, l.ClienteNome, l.QuantidadePedidos, l.ValorTotal)).ToList();
    }

    private sealed class TopClienteLinha
    {
        public int ClienteId { get; set; }
        public string ClienteNome { get; set; } = string.Empty;
        public int QuantidadePedidos { get; set; }
        public decimal ValorTotal { get; set; }
    }
}

internal sealed class CatalogoQueries(PedidosDbContext db) : ICatalogoQueries
{
    public async Task<IReadOnlyList<ClienteDto>> ListarClientesAsync(CancellationToken ct) =>
        await db.Clientes.AsNoTracking()
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Select(c => new ClienteDto(c.Id, c.Nome, c.Email))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProdutoDto>> ListarProdutosAsync(CancellationToken ct) =>
        await db.Produtos.AsNoTracking()
            .OrderBy(p => p.Nome)
            .Select(p => new ProdutoDto(p.Id, p.Nome, p.Preco, p.Estoque))
            .ToListAsync(ct);
}
