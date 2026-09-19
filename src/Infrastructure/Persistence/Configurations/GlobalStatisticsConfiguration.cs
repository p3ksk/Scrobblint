using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class GlobalStatisticsConfiguration : IEntityTypeConfiguration<GlobalStatistics>
{
    public void Configure(EntityTypeBuilder<GlobalStatistics> builder)
    {
        builder.ToTable("GlobalStatistics");

        // Fixed single row (id 1): the key is always supplied, never database-generated.
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        // No max length: the payload is an unbounded JSON document (TEXT on SQLite, longtext on MySQL).
        builder.Property(s => s.PayloadJson).IsRequired();
        builder.Property(s => s.ComputedAt).IsRequired();
    }
}
