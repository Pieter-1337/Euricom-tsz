using System.Linq.Expressions;
using Moq;
using Shouldly;
using Tsz.Api.Tests.Builders;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Workdays;
using Tsz.Modules.Workdays.Contracts;
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

    private static (Mock<IUnitOfWork> uow, Mock<IRepository<Holiday>> repo) BuildMocksWithHolidays(
        IEnumerable<Holiday> holidays)
    {
        var repo = new Mock<IRepository<Holiday>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<Expression<Func<Holiday, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(holidays.ToList());

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

    [Fact]
    public async Task GetDayKindsAsync_WeekendDay_IsBusinessDayFalseHolidayNameNull()
    {
        var saturday = new DateOnly(2026, 5, 23);
        var (uow, _) = BuildMocksWithHolidays([]);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.GetDayKindsAsync([saturday]);

        result.Count.ShouldBe(1);
        result[0].Date.ShouldBe(saturday);
        result[0].IsBusinessDay.ShouldBeFalse();
        result[0].HolidayName.ShouldBeNull();
    }

    [Fact]
    public async Task GetDayKindsAsync_HolidayOnWeekday_IsBusinessDayFalseHolidayNameSet()
    {
        var ascensionDay = new DateOnly(2026, 5, 14); // Thursday — Ascension Day 2026
        var holiday = Holiday.Create(ascensionDay, "Ascension Day", "BE", HolidayType.Public);
        var (uow, _) = BuildMocksWithHolidays([holiday]);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.GetDayKindsAsync([ascensionDay]);

        result.Count.ShouldBe(1);
        result[0].IsBusinessDay.ShouldBeFalse();
        result[0].HolidayName.ShouldBe("Ascension Day");
    }

    [Fact]
    public async Task GetDayKindsAsync_PlainWeekday_IsBusinessDayTrueHolidayNameNull()
    {
        var monday = new DateOnly(2026, 5, 18);
        var (uow, _) = BuildMocksWithHolidays([]);
        var module = new WorkdaysAccessModule(uow.Object);

        var result = await module.GetDayKindsAsync([monday]);

        result.Count.ShouldBe(1);
        result[0].IsBusinessDay.ShouldBeTrue();
        result[0].HolidayName.ShouldBeNull();
    }
}
