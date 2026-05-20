using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Modules.Users.Domain.LeaveTypes;
using Tsz.Modules.Users.Domain.Users;

namespace Tsz.Modules.Users.Domain.Leaves;

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

        builder.HasIndex(ul => new { ul.UserId, ul.LeaveTypeId, ul.Year })
            .IsUnique();
    }
}
