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

        // Covering indexes for the per-user grouping aggregates. The leading (UserId, Artist) prefix
        // also serves the top-artists / distinct-artists queries, so no separate (UserId, Artist)
        // index is needed. (UserId, Artist, Track) covers distinct-tracks and top-tracks;
        // (UserId, Artist, Album) covers distinct-albums and top-albums.
        builder.HasIndex(s => new { s.UserId, s.Artist, s.Track });
        builder.HasIndex(s => new { s.UserId, s.Artist, s.Album });
    }
}
