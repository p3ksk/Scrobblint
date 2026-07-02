using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class FailedRelayConfiguration : IEntityTypeConfiguration<FailedRelay>
{
    public void Configure(EntityTypeBuilder<FailedRelay> builder)
    {
        builder.ToTable("FailedRelays");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Provider).HasConversion<int>().IsRequired();
        builder.Property(r => r.Status).HasConversion<int>().IsRequired();
        builder.Property(r => r.TracksJson).IsRequired().HasMaxLength(8192);
        builder.Property(r => r.RetryCount).IsRequired();
        builder.Property(r => r.NextRetryAt).IsRequired();
        builder.Property(r => r.LastError).HasMaxLength(1024);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt).IsRequired();

        builder.HasIndex(r => new { r.Status, r.NextRetryAt });

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
