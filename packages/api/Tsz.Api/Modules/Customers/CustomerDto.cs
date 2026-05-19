using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Customers;

public sealed record CustomerDto(Guid Id, string Name)
    : IEntityDto<Customer, CustomerDto>
{
    public static Expression<Func<Customer, CustomerDto>> Project =>
        c => new CustomerDto(c.Id, c.Name);

    public static CustomerDto ToDto(Customer entity) =>
        new(entity.Id, entity.Name);
}
