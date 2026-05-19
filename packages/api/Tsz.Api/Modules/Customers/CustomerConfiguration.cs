using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Tsz.Api.Modules.Customers;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Number)
            .IsRequired();

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.OwnsOne(c => c.Address, addr =>
        {
            addr.Property(a => a.Street).HasColumnName("Address_Street").HasMaxLength(256);
            addr.Property(a => a.Zip).HasColumnName("Address_Zip").HasMaxLength(32);
            addr.Property(a => a.City).HasColumnName("Address_City").HasMaxLength(128);
            addr.Property(a => a.Country).HasColumnName("Address_Country").HasMaxLength(2);
        });

        builder.OwnsOne(c => c.ContactPerson, cp =>
        {
            cp.Property(p => p.Name).HasColumnName("ContactPerson_Name").HasMaxLength(256);
            cp.Property(p => p.Email).HasColumnName("ContactPerson_Email").IsRequired().HasMaxLength(256);
        });

        // Cross-module reference to Users — Guid only, no nav, no FK constraint.
        builder.Property(c => c.ClientManagerId);

        builder.HasIndex(c => c.Number)
            .IsUnique()
            .HasFilter("\"DeletedAt\" IS NULL");

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
