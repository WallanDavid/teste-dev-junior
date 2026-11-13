namespace Pedidos.Application.Catalogo;

public sealed record ClienteDto(int Id, string Nome, string Email);

public sealed record ProdutoDto(int Id, string Nome, decimal Preco, int Estoque);
