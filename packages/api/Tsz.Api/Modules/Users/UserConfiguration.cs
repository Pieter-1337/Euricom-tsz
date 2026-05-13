using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Api.Modules.Users;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.EntraOid)
            .HasMaxLength(64);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(u => u.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(u => u.HolidayDays).HasPrecision(5, 2);
        builder.Property(u => u.AdvDays).HasPrecision(5, 2);
        builder.Property(u => u.AncienniteitDays).HasPrecision(5, 2);
        builder.Property(u => u.SicknessDays).HasPrecision(5, 2);

        builder.HasIndex(u => u.EntraOid)
            .IsUnique()
            .HasFilter("\"EntraOid\" IS NOT NULL");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
