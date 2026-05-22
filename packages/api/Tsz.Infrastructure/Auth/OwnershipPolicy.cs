using System.Linq.Expressions;

namespace Tsz.Infrastructure.Auth;

/// Describes how to scope reads of <typeparamref name="T"/> to the current caller.
/// Callers in <see cref="FullAccessRoles"/> bypass the filter (see everything);
/// everyone else is restricted to rows where <see cref="OwnerIdSelector"/> equals their user id.
public sealed record OwnershipPolicy<T>(
    Expression<Func<T, Guid?>> OwnerIdSelector,
    IReadOnlyCollection<string> FullAccessRoles);
