using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class ScrobbleImportConfiguration : IEntityTypeConfiguration<ScrobbleImport>
{
    public void Configure(EntityTypeBuilder<ScrobbleImport> builder)
    {
        builder.ToTable("ScrobbleImports");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Provider).HasConversion<int>().IsRequired();
        builder.Property(i => i.Status).HasConversion<int>().IsRequired();
        builder.Property(i => i.SourceAccount).IsRequired().HasMaxLength(256);
        builder.Property(i => i.Error).HasMaxLength(1024);
        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.UpdatedAt).IsRequired();

        // Find a user's latest / active import quickly.
        builder.HasIndex(i => new { i.UserId, i.Status });

        builder.HasOne(i => i.User)
            .WithMany()
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
