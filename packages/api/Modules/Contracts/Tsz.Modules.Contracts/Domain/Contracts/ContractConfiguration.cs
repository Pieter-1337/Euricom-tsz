using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Modules.Contracts.Domain.Contracts;

public class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        builder.ToTable("Contracts");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number)
            .IsRequired();

        builder.Property(c => c.Subject)
            .IsRequired()
            .HasMaxLength(256);

        // Cross-module references — Guid only, no nav, no FK constraint.
        builder.Property(c => c.CustomerId).IsRequired();
        builder.Property(c => c.ClientManagerId);

        builder.Property(c => c.Start).IsRequired();
        builder.Property(c => c.End);

        builder.OwnsMany(c => c.Consultants, cc =>
        {
            cc.ToTable("ContractConsultants");
            cc.WithOwner().HasForeignKey("ContractId");
            cc.Property(x => x.UserId).IsRequired();
            cc.HasKey("ContractId", nameof(ContractConsultant.UserId));
        });

        builder.OwnsMany(c => c.Tasks, tb =>
        {
            tb.ToTable("ContractTasks");
            tb.WithOwner().HasForeignKey("ContractId");
            tb.HasKey(t => t.Id);
            tb.Property(t => t.Id).ValueGeneratedNever();
            tb.Property(t => t.Name).IsRequired().HasMaxLength(256);
            tb.Property(t => t.Rate).IsRequired().HasColumnType("decimal(18,4)");
            tb.Property(t => t.DeletedAt);
        });

        builder.HasIndex(c => c.Number)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasQueryFilter("SoftDelete", c => c.DeletedAt == null);
    }
}
