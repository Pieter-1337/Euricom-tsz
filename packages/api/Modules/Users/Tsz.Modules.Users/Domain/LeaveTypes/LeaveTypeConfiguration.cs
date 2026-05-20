using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tsz.Modules.Users.Domain.Leaves;

namespace Tsz.Modules.Users.Domain.LeaveTypes;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
    private static readonly Guid VerlofId      = new("11111111-1111-1111-1111-000000000001");
    private static readonly Guid AdvId         = new("11111111-1111-1111-1111-000000000002");
    private static readonly Guid AncienniteitId = new("11111111-1111-1111-1111-000000000003");
    private static readonly Guid ZiekteId      = new("11111111-1111-1111-1111-000000000004");

    public void Configure(EntityTypeBuilder<LeaveType> builder)
    {
        builder.ToTable("LeaveTypes");

        builder.HasKey(lt => lt.Id);

        builder.Property(lt => lt.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(lt => lt.DefaultDays)
            .HasPrecision(5, 2);

        builder.Property(lt => lt.DefaultAllowed)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.HasIndex(lt => lt.Name)
            .IsUnique();

        builder.Ignore(lt => lt.DeletedAt);

        builder.HasData(
            new { Id = VerlofId,       Name = "Verlof",        DefaultAllowed = LeaveAllowed.Limited,   DefaultDays = (decimal?)20m  },
            new { Id = AdvId,          Name = "ADV dagen",     DefaultAllowed = LeaveAllowed.Limited,   DefaultDays = (decimal?)5m   },
            new { Id = AncienniteitId, Name = "Anciënniteit",  DefaultAllowed = LeaveAllowed.Limited,   DefaultDays = (decimal?)0m   },
            new { Id = ZiekteId,       Name = "Ziekte",        DefaultAllowed = LeaveAllowed.Unlimited, DefaultDays = (decimal?)null }
        );
    }
}
