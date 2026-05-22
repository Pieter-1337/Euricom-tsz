using System.Linq.Expressions;

namespace Tsz.Infrastructure.Auth;

/// Describes how to scope reads of <typeparamref name="T"/> to the current caller.
/// Callers in <see cref="FullAccessRoles"/> bypass the filter (see everything);
/// everyone else is restricted to rows matched by <see cref="OwnerEquals"/> for their user id.
public sealed record OwnershipPolicy<T>(
    Func<Guid, Expression<Func<T, bool>>> OwnerEquals,
    IReadOnlyCollection<string> FullAccessRoles);
