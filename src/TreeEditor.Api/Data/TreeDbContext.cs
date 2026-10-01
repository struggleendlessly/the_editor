using Microsoft.EntityFrameworkCore;
using TreeEditor.Contracts;

namespace TreeEditor.Api.Data;

public sealed class TreeDbContext(DbContextOptions<TreeDbContext> options) : DbContext(options)
{
    public DbSet<Node> Nodes => Set<Node>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Node>(node =>
        {
            node.Property(n => n.Id).ValueGeneratedNever();
            node.Property(n => n.Value).HasMaxLength(NodeDto.MaxValueLength);
            node.HasMany(n => n.Children)
                .WithOne()
                .HasForeignKey(n => n.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
