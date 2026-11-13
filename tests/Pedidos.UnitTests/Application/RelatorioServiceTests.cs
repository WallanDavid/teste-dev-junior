using NSubstitute;
using Pedidos.Application.Abstractions;
using Pedidos.Application.Relatorios;
using Pedidos.Domain.Common;

namespace Pedidos.UnitTests.Application;

public class RelatorioServiceTests
{
    private readonly IRelatorioQueries _queries = Substitute.For<IRelatorioQueries>();
    private readonly RelatorioService _service;

    public RelatorioServiceTests() => _service = new RelatorioService(_queries);

    [Fact]
    public async Task TopClientesAsync_SemDatas_RetornaAmbosOsErrosDeValidacao()
    {
        var resultado = await _service.TopClientesAsync(new FiltroPeriodoRequest(), default);

        Assert.Equal(2, resultado.Errors.Count);
        Assert.Contains(Errors.Filtro.DataInicioObrigatoria, resultado.Errors);
        Assert.Contains(Errors.Filtro.DataFimObrigatoria, resultado.Errors);
        await _queries.DidNotReceiveWithAnyArgs().TopClientesAsync(default, default, default, default);
    }

    [Fact]
    public async Task TopClientesAsync_PeriodoInvertido_RetornaValidacao()
    {
        var filtro = new FiltroPeriodoRequest { DataInicio = new(2026, 12, 31), DataFim = new(2026, 1, 1) };

        var resultado = await _service.TopClientesAsync(filtro, default);

        Assert.Equal(Errors.Filtro.PeriodoInvalido, Assert.Single(resultado.Errors));
    }

    [Fact]
    public async Task TopClientesAsync_Valido_ConsultaTop5ComIntervaloSemiaberto()
    {
        var esperado = new List<TopClienteDto> { new(1, "Ana", 2, 300m) };
        _queries.TopClientesAsync(default, default, default, default).ReturnsForAnyArgs(esperado);
        var filtro = new FiltroPeriodoRequest { DataInicio = new(2026, 1, 1), DataFim = new(2026, 12, 31) };

        var resultado = await _service.TopClientesAsync(filtro, default);

        Assert.Same(esperado, resultado.Value);
        await _queries.Received(1).TopClientesAsync(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            5,
            Arg.Any<CancellationToken>());
    }
}
