using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class MagicSetKnowledgeConfiguration
    : IEntityTypeConfiguration<MagicSetKnowledge>
{
    public void Configure(
        EntityTypeBuilder<MagicSetKnowledge> builder)
    {
        builder.HasKey(x => x.SetCode);

        builder.Property(x => x.SetCode)
            .HasMaxLength(10);

        builder.Property(x => x.SetName)
            .HasMaxLength(200);

        builder.Property(x => x.SymbolDescription)
            .HasMaxLength(500);

        builder.Property(x => x.SymbolKeywords)
            .HasMaxLength(500);
    }
}