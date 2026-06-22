using ITMartinAdhd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ITMartinAdhd.Infrastructure.Persistence.Configurations;

public sealed class StoredItemConfiguration : IEntityTypeConfiguration<StoredItem>
{
    public void Configure(EntityTypeBuilder<StoredItem> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Location).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.PhotoPath).HasMaxLength(500);
        builder.HasIndex(x => x.UpdatedAt);
        builder.HasIndex(x => x.Name);
    }
}
