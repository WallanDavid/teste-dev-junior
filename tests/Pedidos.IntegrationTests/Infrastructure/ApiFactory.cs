using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Domain.Entities;
using Pedidos.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace Pedidos.IntegrationTests.Infrastructure;

/// <summary>
/// Sobe um SQL Server real (Testcontainers), cria o schema com os scripts de <c>database/</c>
/// e hospeda a API em memória apontando para ele. Compartilhado por todos os testes da coleção.
/// </summary>
public sealed partial class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();

        // 01 cria o banco (roda no master); 02 cria tabelas/índices. A carga (03) não é usada:
        // cada teste cria os próprios dados para não depender de ordem nem de valores fixos.
        await ExecutarScriptAsync(_sql.GetConnectionString(), "01-create-database.sql");
        _connectionString = new SqlConnectionStringBuilder(_sql.GetConnectionString()) { InitialCatalog = "Pedidos" }.ToString();
        await ExecutarScriptAsync(_connectionString, "02-create-tables.sql");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sql.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Pedidos", _connectionString);
    }

    // ---------------------------------------------------------------- dados de teste

    public async Task<Cliente> CriarClienteAsync(string nome = "Cliente Teste")
    {
        await using var db = NovoDbContext();
        var cliente = new Cliente(nome, $"{Guid.NewGuid():N}@teste.com");
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync();
        return cliente;
    }

    public async Task<Produto> CriarProdutoAsync(decimal preco = 10m, int estoque = 100, string? nome = null)
    {
        await using var db = NovoDbContext();
        var produto = new Produto(nome ?? $"Produto {Guid.NewGuid():N}"[..20], preco, estoque);
        db.Produtos.Add(produto);
        await db.SaveChangesAsync();
        return produto;
    }

    /// <summary>Grava um pedido direto no banco com data escolhida (a API sempre usa "agora").</summary>
    public async Task<int> CriarPedidoAsync(int clienteId, DateTime dataUtc, params (int ProdutoId, int Quantidade)[] itens)
    {
        await using var db = NovoDbContext();
        var ids = itens.Select(i => i.ProdutoId).ToList();
        var produtos = await db.Produtos.Where(p => ids.Contains(p.Id)).ToListAsync();
        var resultado = Pedido.Criar(
            clienteId,
            itens.Select(i => new ItemSolicitado(i.ProdutoId, i.Quantidade)).ToList(),
            produtos,
            dataUtc);
        if (resultado.IsFailure)
            throw new InvalidOperationException(string.Join("; ", resultado.Errors.Select(e => e.Message)));

        db.Pedidos.Add(resultado.Value);
        await db.SaveChangesAsync();
        return resultado.Value.Id;
    }

    public async Task<int> ObterEstoqueAsync(int produtoId)
    {
        await using var db = NovoDbContext();
        return await db.Produtos.Where(p => p.Id == produtoId).Select(p => p.Estoque).SingleAsync();
    }

    public async Task<int> ContarPedidosDoClienteAsync(int clienteId)
    {
        await using var db = NovoDbContext();
        return await db.Pedidos.CountAsync(p => p.ClienteId == clienteId);
    }

    private PedidosDbContext NovoDbContext() =>
        new(new DbContextOptionsBuilder<PedidosDbContext>().UseSqlServer(_connectionString).Options);

    // ---------------------------------------------------------------- scripts

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex SeparadorGo();

    private static async Task ExecutarScriptAsync(string connectionString, string arquivo)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "database", arquivo);
        var lotes = SeparadorGo().Split(await File.ReadAllTextAsync(caminho))
            .Where(l => !string.IsNullOrWhiteSpace(l));

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        foreach (var lote in lotes)
        {
            await using var cmd = new SqlCommand(lote, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Nome = "api";
}
