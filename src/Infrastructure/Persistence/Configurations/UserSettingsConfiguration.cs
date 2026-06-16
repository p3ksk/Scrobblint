using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scrobblint.Domain.Entities;

namespace Scrobblint.Infrastructure.Persistence.Configurations;

public sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(s => s.Id);

        // Enums persist as integers (provider-neutral, compact).
        builder.Property(s => s.ProfileVisibility).HasConversion<int>().IsRequired();
        builder.Property(s => s.Theme).HasConversion<int>().IsRequired();

        builder.HasIndex(s => s.UserId).IsUnique();
    }
}
