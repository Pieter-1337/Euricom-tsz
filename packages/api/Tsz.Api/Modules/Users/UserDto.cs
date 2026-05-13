using System.Linq.Expressions;
using Tsz.Infrastructure.Abstractions;

namespace Tsz.Api.Modules.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string Name,
    UserRole Role,
    decimal HolidayDays,
    decimal AdvDays,
    decimal AncienniteitDays,
    decimal SicknessDays)
    : IEntityDto<User, UserDto>
{
    public static Expression<Func<User, UserDto>> Project =>
        u => new UserDto(u.Id, u.Email, u.Name, u.Role, u.HolidayDays, u.AdvDays, u.AncienniteitDays, u.SicknessDays);

    public static UserDto ToDto(User entity) =>
        new(entity.Id, entity.Email, entity.Name, entity.Role,
            entity.HolidayDays, entity.AdvDays, entity.AncienniteitDays, entity.SicknessDays);
}
