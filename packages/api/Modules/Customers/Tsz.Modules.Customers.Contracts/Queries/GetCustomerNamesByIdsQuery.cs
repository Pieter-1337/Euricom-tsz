using Tsz.Infrastructure.Abstractions;

namespace Tsz.Modules.Customers.Contracts.Queries;

public sealed record GetCustomerNamesByIdsQuery(IReadOnlyList<Guid> CustomerIds)
    : IQuery<IReadOnlyDictionary<Guid, string>>;
