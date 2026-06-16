using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class ExternalConnectionConfiguration : IEntityTypeConfiguration<ExternalConnection>
{
    public void Configure(EntityTypeBuilder<ExternalConnection> builder)
    {
        builder.ToTable("ExternalConnections");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Provider).HasConversion<int>().IsRequired();
        builder.Property(c => c.IsEnabled).IsRequired();
        builder.Property(c => c.Token).IsRequired().HasMaxLength(512);
        builder.Property(c => c.ApiRoot).HasMaxLength(512);
        builder.Property(c => c.ExternalUsername).HasMaxLength(256);
        builder.Property(c => c.CreatedAt).IsRequired();

        // One connection per provider per user.
        builder.HasIndex(c => new { c.UserId, c.Provider }).IsUnique();

        builder.HasOne(c => c.User)
            .WithMany(u => u.ExternalConnections)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
