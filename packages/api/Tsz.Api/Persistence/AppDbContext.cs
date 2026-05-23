using Microsoft.EntityFrameworkCore;
using Tsz.Modules.Contracts;
using Tsz.Modules.Customers;
using Tsz.Modules.LeaveTypes;
using Tsz.Modules.Users;
using Tsz.Modules.Workdays;

namespace Tsz.Api.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersModule).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CustomersModule).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContractsModule).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(LeaveTypesModule).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkdaysModule).Assembly);
    }
}
