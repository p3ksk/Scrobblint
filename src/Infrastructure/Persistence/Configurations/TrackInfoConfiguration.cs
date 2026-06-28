using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Application.Common;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class TrackInfoConfiguration : IEntityTypeConfiguration<TrackInfo>
{
    public void Configure(EntityTypeBuilder<TrackInfo> builder)
    {
        builder.ToTable("TrackInfos");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.ArtistKey)
            .IsRequired()
            .HasMaxLength(AppConstants.FieldMaxLength);

        builder.Property(t => t.TrackKey)
            .IsRequired()
            .HasMaxLength(AppConstants.FieldMaxLength);

        builder.Property(t => t.CanonicalArtist).HasMaxLength(AppConstants.FieldMaxLength);
        builder.Property(t => t.CanonicalTrack).HasMaxLength(AppConstants.FieldMaxLength);
        builder.Property(t => t.CanonicalAlbum).HasMaxLength(AppConstants.FieldMaxLength);
        builder.Property(t => t.FetchedAt).IsRequired();

        // One cache row per submitted (artist, track) pair; also the lookup index.
        builder.HasIndex(t => new { t.ArtistKey, t.TrackKey }).IsUnique();
    }
}
