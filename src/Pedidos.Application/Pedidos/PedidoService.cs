using Pedidos.Application.Abstractions;
using Pedidos.Application.Common;
using Pedidos.Domain.Common;
using Pedidos.Domain.Entities;

namespace Pedidos.Application.Pedidos;

public sealed class PedidoService(
    IClienteRepository clientes,
    IProdutoRepository produtos,
    IPedidoRepository pedidos,
    IPedidoQueries queries,
    TimeProvider clock)
{
    public const int TamanhoPaginaPadrao = 10;

    public async Task<Result<PedidoDto>> CriarAsync(CriarPedidoRequest request, CancellationToken ct)
    {
        var itens = (request.Itens ?? [])
            .Select(i => new ItemSolicitado(i.ProdutoId, i.Quantidade))
            .ToList();

        // 1) Entrada malformada: rejeita sem ir ao banco.
        var entrada = Pedido.ValidarEntrada(request.ClienteId, itens);
        if (entrada.IsFailure)
            return Result<PedidoDto>.Failure(entrada.Errors);

        // 2) RN02: cliente precisa existir.
        var cliente = await clientes.ObterPorIdAsync(request.ClienteId, ct);
        if (cliente is null)
            return Errors.Pedido.ClienteNaoEncontrado(request.ClienteId);

        // 3) Regras que dependem dos produtos ficam no domínio.
        var ids = itens.Select(i => i.ProdutoId).Distinct().ToList();
        var encontrados = await produtos.ObterPorIdsAsync(ids, ct);

        var criacao = Pedido.Criar(cliente.Id, itens, encontrados, clock.GetUtcNow().UtcDateTime);
        if (criacao.IsFailure)
            return Result<PedidoDto>.Failure(criacao.Errors);

        // 4) Pedido + baixa de estoque gravados juntos; RowVersion detecta corrida (RN10).
        pedidos.Adicionar(criacao.Value);
        try
        {
            await pedidos.SalvarAlteracoesAsync(ct);
        }
        catch (ConcorrenciaException)
        {
            return Errors.Pedido.ConflitoConcorrencia;
        }

        return PedidoDto.De(criacao.Value, cliente);
    }

    public async Task<Result<PedidoDto>> ObterPorIdAsync(int id, CancellationToken ct)
    {
        var pedido = await queries.ObterPorIdAsync(id, ct);
        return pedido is null ? Errors.Pedido.NaoEncontrado(id) : pedido;
    }

    public async Task<Result<PaginaResultado<PedidoDto>>> ListarAsync(FiltroPedidosRequest filtro, CancellationToken ct)
    {
        var pagina = filtro.Pagina ?? 1;
        var tamanho = filtro.TamanhoPagina ?? TamanhoPaginaPadrao;

        var erros = new List<Error>();
        if (pagina < 1)
            erros.Add(Errors.Filtro.PaginaInvalida);
        if (tamanho is < 1 or > Errors.Filtro.MaxTamanhoPagina)
            erros.Add(Errors.Filtro.TamanhoPaginaInvalido);
        if (filtro.DataInicio > filtro.DataFim)
            erros.Add(Errors.Filtro.PeriodoInvalido);

        if (erros.Count > 0)
            return Result<PaginaResultado<PedidoDto>>.Failure(erros);

        var consulta = new ConsultaPedidos(
            filtro.ClienteId,
            Periodo.InicioUtc(filtro.DataInicio),
            Periodo.FimExclusivoUtc(filtro.DataFim),
            pagina,
            tamanho);

        return await queries.ListarAsync(consulta, ct);
    }
}
