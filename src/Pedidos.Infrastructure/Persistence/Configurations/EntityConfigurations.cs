using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pedidos.Domain.Entities;

namespace Pedidos.Infrastructure.Persistence.Configurations;

// Cada configuração espelha uma tabela de database/02-create-tables.sql.
// Índices são declarados aqui só como documentação/metadata: quem cria é o script.

internal sealed class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("Clientes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Nome).HasMaxLength(150).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(200).IsRequired();
        builder.Property(c => c.DataCadastro).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(c => c.Email).IsUnique().HasDatabaseName("UX_Clientes_Email");
    }
}

internal sealed class ProdutoConfiguration : IEntityTypeConfiguration<Produto>
{
    public void Configure(EntityTypeBuilder<Produto> builder)
    {
        builder.ToTable("Produtos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Nome).HasMaxLength(150).IsRequired();
        builder.Property(p => p.Preco).HasPrecision(18, 2);
        builder.Property(p => p.Estoque);
        builder.Property(p => p.RowVersion).IsRowVersion(); // UPDATE ... WHERE Id = @id AND RowVersion = @rv
    }
}

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("Pedidos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.DataPedido).HasColumnType("datetime2(3)");
        builder.Ignore(p => p.ValorTotal);

        builder.HasOne(p => p.Cliente)
            .WithMany()
            .HasForeignKey(p => p.ClienteId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasMany(p => p.Itens)
            .WithOne()
            .HasForeignKey(i => i.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field); // campo _itens

        builder.HasIndex(p => new { p.ClienteId, p.DataPedido }).HasDatabaseName("IX_Pedidos_ClienteId_DataPedido");
        builder.HasIndex(p => p.DataPedido).HasDatabaseName("IX_Pedidos_DataPedido");
    }
}

internal sealed class ItemPedidoConfiguration : IEntityTypeConfiguration<ItemPedido>
{
    public void Configure(EntityTypeBuilder<ItemPedido> builder)
    {
        builder.ToTable("ItensPedido");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        builder.Ignore(i => i.Subtotal);

        builder.HasOne(i => i.Produto)
            .WithMany()
            .HasForeignKey(i => i.ProdutoId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(i => new { i.PedidoId, i.ProdutoId }).IsUnique().HasDatabaseName("UX_ItensPedido_PedidoId_ProdutoId");
        builder.HasIndex(i => i.ProdutoId).HasDatabaseName("IX_ItensPedido_ProdutoId");
    }
}
