using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts.Queries;

public sealed record GetCustomerClientManagerIdQuery(Guid CustomerId) : IQuery<Guid?>;
