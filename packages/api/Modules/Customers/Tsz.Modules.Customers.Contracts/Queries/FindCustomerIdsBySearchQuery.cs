using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts.Queries;

public sealed record FindCustomerIdsBySearchQuery(string Term) : IQuery<IReadOnlyList<Guid>>;
