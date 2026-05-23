using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Modules.LeaveTypes.Domain.LeaveTypes;

namespace Tsz.Modules.LeaveTypes.Domain.Leaves;

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

        builder.HasIndex(ul => new { ul.UserId, ul.LeaveTypeId, ul.Year })
            .IsUnique();

        builder.Ignore(ul => ul.DeletedAt);
    }
}
