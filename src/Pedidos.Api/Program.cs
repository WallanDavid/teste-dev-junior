using System.Text.Json.Serialization;
using Pedidos.Api.Endpoints;
using Pedidos.Api.Http;
using Pedidos.Application.Pedidos;
using Pedidos.Application.Relatorios;
using Pedidos.Infrastructure;
using Pedidos.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Pedidos")
    ?? throw new InvalidOperationException("ConnectionStrings:Pedidos não configurada.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<PedidoService>();
builder.Services.AddScoped<RelatorioService>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    o.SerializerOptions.Converters.Add(new DinheiroJsonConverter());
});

// Exceções não tratadas e erros de binding (ex.: data em formato inválido) também viram ProblemDetails.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();

builder.Services.AddHealthChecks().AddDbContextCheck<PedidosDbContext>("sqlserver");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SupportNonNullableReferenceTypes();
    o.DescribeAllParametersInCamelCase();
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health");

app.MapPedidoEndpoints();
app.MapRelatorioEndpoints();
app.MapCatalogoEndpoints();

app.Run();

// Exposto para o WebApplicationFactory dos testes de integração.
public partial class Program;
