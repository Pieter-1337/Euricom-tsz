using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts.Queries;

public sealed record CustomerExistsQuery(Guid CustomerId) : IQuery<bool>;
