using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Api.Modules.LeaveTypes;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveTypes");

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(lt => lt.PayrollCode)
            .HasMaxLength(50);

        builder.Property(lt => lt.ReportingCode)
            .HasMaxLength(50);

        builder.Property(lt => lt.Group)
            .HasMaxLength(50);

        builder.Property(lt => lt.DefaultDays)
            .HasPrecision(5, 2);

        builder.Property(lt => lt.DefaultAllowed)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(lt => lt.Name)
            .IsUnique();
    }
}
