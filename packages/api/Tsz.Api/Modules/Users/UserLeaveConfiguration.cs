using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Api.Modules.LeaveTypes;

namespace Tsz.Api.Modules.Users;

public class UserLeaveConfiguration : IEntityTypeConfiguration<UserLeave>
{
    public void Configure(EntityTypeBuilder<UserLeave> builder)
    {
        builder.ToTable("UserLeaves");

        builder.HasKey(ul => ul.Id);

        builder.Property(ul => ul.TotalDays)
            .HasPrecision(5, 2);

        builder.HasOne<LeaveType>()
            .WithMany()
            .HasForeignKey(ul => ul.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(ul => ul.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ul => new { ul.UserId, ul.LeaveTypeId })
            .IsUnique();
    }
}
