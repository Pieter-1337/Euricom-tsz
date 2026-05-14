using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserRole Role)
    : IEntityDto<User, UserDto>
{
    public static Expression<Func<User, UserDto>> Project =>
        u => new UserDto(u.Id, u.Email, u.FirstName, u.LastName, u.Role);

    public static UserDto ToDto(User entity) =>
        new(entity.Id, entity.Email, entity.FirstName, entity.LastName, entity.Role);
}
