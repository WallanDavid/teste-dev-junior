using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pedidos.Application.Abstractions;
using Pedidos.Infrastructure.Persistence;

namespace Pedidos.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<PedidosDbContext>(options => options.UseSqlServer(
            connectionString,
            sql => sql.EnableRetryOnFailure(maxRetryCount: 5))); // tolera o SQL Server ainda subindo no Docker

        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IProdutoRepository, ProdutoRepository>();
        services.AddScoped<IPedidoRepository, PedidoRepository>();
        services.AddScoped<IPedidoQueries, PedidoQueries>();
        services.AddScoped<IRelatorioQueries, RelatorioQueries>();
        services.AddScoped<ICatalogoQueries, CatalogoQueries>();

        return services;
    }
}
