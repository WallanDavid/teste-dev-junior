namespace Pedidos.Domain.Common;

/// <summary>
/// Catálogo de erros do sistema. Os códigos são contrato com o frontend: não renomeie.
/// </summary>
public static class Errors
{
    public static Error Validacao(string campo, string mensagem) =>
        new("validacao", mensagem, ErrorType.Validacao, campo);

    public static class Pedido
    {
        public const int MaxItens = 100;
        public const int MaxQuantidadePorItem = 10_000;

        public static readonly Error SemItens =
            Validacao("itens", "O pedido deve ter pelo menos um item.");

        public static readonly Error ItensDemais =
            Validacao("itens", $"O pedido pode ter no máximo {MaxItens} itens.");

        public static Error ClienteIdInvalido =>
            Validacao("clienteId", "Informe um cliente válido.");

        public static Error ProdutoIdInvalido(int indice) =>
            Validacao($"itens[{indice}].produtoId", "Informe um produto válido.");

        public static Error QuantidadeInvalida(int indice) =>
            Validacao($"itens[{indice}].quantidade", $"A quantidade deve estar entre 1 e {MaxQuantidadePorItem}.");

        public static Error ClienteNaoEncontrado(int clienteId) =>
            new("cliente_nao_encontrado", $"Cliente {clienteId} não encontrado.", ErrorType.Regra, "clienteId");

        public static Error ProdutoNaoEncontrado(int produtoId) =>
            new("produto_nao_encontrado", $"Produto {produtoId} não encontrado.", ErrorType.Regra, "itens");

        public static Error EstoqueInsuficiente(string produto, int disponivel, int solicitado) =>
            new("estoque_insuficiente",
                $"Estoque insuficiente para \"{produto}\": disponível {disponivel}, solicitado {solicitado}.",
                ErrorType.Regra, "itens");

        public static Error EstoqueEsgotadoNaGravacao(string produto) =>
            new("estoque_insuficiente",
                $"O estoque de \"{produto}\" acabou enquanto o pedido era gravado (outro pedido foi concluído antes).",
                ErrorType.Regra, "itens");

        public static Error NaoEncontrado(int pedidoId) =>
            new("pedido_nao_encontrado", $"Pedido {pedidoId} não encontrado.", ErrorType.NaoEncontrado);
    }

    public static class Filtro
    {
        public const int MaxTamanhoPagina = 100;

        public static readonly Error PaginaInvalida =
            Validacao("pagina", "A página deve ser maior ou igual a 1.");

        public static readonly Error TamanhoPaginaInvalido =
            Validacao("tamanhoPagina", $"O tamanho da página deve estar entre 1 e {MaxTamanhoPagina}.");

        public static readonly Error PeriodoInvalido =
            Validacao("dataInicio", "A data inicial não pode ser maior que a data final.");

        public static readonly Error DataInicioObrigatoria =
            Validacao("dataInicio", "Informe a data inicial.");

        public static readonly Error DataFimObrigatoria =
            Validacao("dataFim", "Informe a data final.");
    }
}
