using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class UserStatisticsConfiguration : IEntityTypeConfiguration<UserStatistics>
{
    public void Configure(EntityTypeBuilder<UserStatistics> builder)
    {
        builder.ToTable("UserStatistics");
        builder.HasKey(s => s.UserId);

        // No max length: the payload is an unbounded JSON document (TEXT on SQLite, longtext on MySQL).
        builder.Property(s => s.PayloadJson).IsRequired();
        builder.Property(s => s.ComputedAt).IsRequired();

        // Deleting a user removes their snapshot with them.
        builder.HasOne<User>()
            .WithOne()
            .HasForeignKey<UserStatistics>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
