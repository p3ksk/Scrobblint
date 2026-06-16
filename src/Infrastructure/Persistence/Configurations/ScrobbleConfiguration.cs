using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class ScrobbleConfiguration : IEntityTypeConfiguration<Scrobble>
{
    public void Configure(EntityTypeBuilder<Scrobble> builder)
    {
        builder.ToTable("Scrobbles");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Artist)
            .IsRequired()
            .HasMaxLength(AppConstants.FieldMaxLength);

        builder.Property(s => s.Track)
            .IsRequired()
            .HasMaxLength(AppConstants.FieldMaxLength);

        builder.Property(s => s.Album)
            .HasMaxLength(AppConstants.FieldMaxLength);

        builder.Property(s => s.Timestamp).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        // Recent-listens and date-range queries: newest first per user.
        builder.HasIndex(s => new { s.UserId, s.Timestamp });
        // Grouping aggregates (top artists / tracks).
        builder.HasIndex(s => new { s.UserId, s.Artist });
    }
}
