using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public class TimesheetWeekConfiguration : IEntityTypeConfiguration<TimesheetWeek>
{
    public void Configure(EntityTypeBuilder<TimesheetWeek> builder)
    {
        builder.ToTable("TimesheetWeeks");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        // Cross-module reference — Guid only, no nav, no FK constraint.
        builder.Property(w => w.UserId).IsRequired();

        builder.Property(w => w.IsoYear).IsRequired();
        builder.Property(w => w.IsoWeek).IsRequired();

        builder.Property(w => w.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Ignore(w => w.DeletedAt);
        builder.Ignore(w => w.Entries);

        builder.OwnsMany(w => w.TimeEntries, te =>
        {
            te.ToTable("TimeEntries");
            te.WithOwner().HasForeignKey("TimesheetWeekId");
            te.HasKey(e => e.Id);
            te.Property(e => e.Id).ValueGeneratedNever();
            te.Property(e => e.ContractTaskId).IsRequired();
            te.Property(e => e.Date).IsRequired();
            te.Property(e => e.DurationHours)
                .IsRequired()
                .HasColumnType("decimal(3,2)");
        });

        builder.HasIndex(w => new { w.UserId, w.IsoYear, w.IsoWeek })
            .IsUnique();
    }
}
