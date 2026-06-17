using ITMartin.Magic.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMartin.Magic.Infrastructure.Persistence;

public sealed class MagicCardConfiguration
    : IEntityTypeConfiguration<MagicCard>
{
    public void Configure(
        EntityTypeBuilder<MagicCard> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.SetCode)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.CollectorNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.ScryfallId);
    }
}
