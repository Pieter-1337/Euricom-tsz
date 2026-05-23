using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Workdays;
using Tsz.Modules.Workdays.Domain.Holidays;

namespace Tsz.Api.Tests.Modules.Workdays;

public class WorkdaysAccessModuleTests
{
    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Holiday>> repo) BuildMocks(bool holidayExists = false)
    {
        var repo = new Mock<IRepository<Holiday>>();
        repo.Setup(r => r.ExistsAsync(
                It.IsAny<Expression<Func<Holiday, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(holidayExists);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Holiday>()).Returns(repo.Object);
        return (uow, repo);
    }

    [Theory]
    [InlineData(2026, 1, 2)]  // Friday
    [InlineData(2026, 3, 16)] // Monday
    [InlineData(2026, 7, 22)] // Wednesday
    public async Task IsBusinessDay_WeekdayWithNoHoliday_ReturnsTrue(int year, int month, int day)
    {
        var (uow, _) = BuildMocks(holidayExists: false);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.IsBusinessDay(new DateOnly(year, month, day));

        result.ShouldBeTrue();
    }

    [Theory]
    [InlineData(2026, 1, 3)]  // Saturday
    [InlineData(2026, 1, 4)]  // Sunday
    [InlineData(2026, 3, 21)] // Saturday
    [InlineData(2026, 3, 22)] // Sunday
    public async Task IsBusinessDay_Weekend_ReturnsFalse(int year, int month, int day)
    {
        var (uow, repo) = BuildMocks();
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.IsBusinessDay(new DateOnly(year, month, day));

        result.ShouldBeFalse();
        repo.Verify(r => r.ExistsAsync(
            It.IsAny<Expression<Func<Holiday, bool>>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<bool>()), Times.Never, "No DB call expected for weekends");
    }

    [Fact]
    public async Task IsBusinessDay_BelgianHolidayDate_ReturnsFalse()
    {
        var (uow, _) = BuildMocks(holidayExists: true);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.IsBusinessDay(new DateOnly(2026, 1, 1)); // New Year

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsBusinessDay_WeekdayWithNoSeedEntry_ReturnsTrue()
    {
        var (uow, _) = BuildMocks(holidayExists: false);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.IsBusinessDay(new DateOnly(2030, 6, 17)); // Far future weekday (Monday)

        result.ShouldBeTrue();
    }
}
