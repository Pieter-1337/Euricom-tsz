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

        builder.Property(u => u.FirstName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(u => u.LastName)
            .IsRequired()
            .HasMaxLength(128);

        builder.OwnsMany(u => u.RoleAssignments, ra =>
        {
            ra.ToTable("UserRoles");
            ra.WithOwner().HasForeignKey("UserId");
            ra.Property(r => r.Role).HasConversion<string>().HasMaxLength(32);
            ra.HasKey("UserId", nameof(UserRoleAssignment.Role));
        });

        builder.HasIndex(u => u.EntraOid)
            .IsUnique()
            .HasFilter("\"EntraOid\" IS NOT NULL");

        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
