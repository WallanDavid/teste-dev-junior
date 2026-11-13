using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Abstractions;
using Pedidos.Infrastructure.Persistence;

namespace Pedidos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // Sem EnableRetryOnFailure: ele exige envolver transações explícitas (usadas em PedidoRepository)
        // numa execution strategy, e no Docker a API só sobe depois do banco estar saudável.
        services.AddDbContext<PedidosDbContext>(options => options.UseSqlServer(connectionString));

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IPedidoQueries, PedidoQueries>();
        services.AddScoped<IRelatorioQueries, RelatorioQueries>();
        services.AddScoped<ICatalogoQueries, CatalogoQueries>();

        return services;
    }
}
