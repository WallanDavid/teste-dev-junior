namespace Pedidos.Domain.Entities;

public class Cliente
{
    // Construtor para o EF Core.
    private Cliente() { }

    public Cliente(string nome, string email)
    {
        Nome = nome;
        Email = email;
    }

    public int Id { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public DateTime DataCadastro { get; private set; }
}
