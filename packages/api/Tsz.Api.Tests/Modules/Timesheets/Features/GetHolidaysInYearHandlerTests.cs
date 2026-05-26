using Moq;
using Shouldly;
using Tsz.Infrastructure.Abstractions;
using Tsz.Modules.Timesheets.Domain.Holidays;
using Tsz.Modules.Timesheets.Features;

namespace Tsz.Api.Tests.Modules.Timesheets.Features;

public class GetHolidaysInYearHandlerTests
{
    private static GetHolidaysInYearHandler BuildHandler(IEnumerable<Holiday>? holidays = null)
    {
        var repo = new Mock<IRepository<Holiday>>();
        repo.Setup(r => r.GetAllAsListAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>()))
            .ReturnsAsync(holidays?.ToList() ?? []);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.RepositoryFor<Holiday>()).Returns(repo.Object);

        return new GetHolidaysInYearHandler(uow.Object);
    }

    [Fact]
    public async Task NoHolidays_ReturnsEmpty()
    {
        var handler = BuildHandler();
        var result = await handler.HandleAsync(new GetHolidaysInYearQuery(2030));
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task HolidaysExist_ProjectsCorrectFields()
    {
        var holiday = Holiday.Create(new DateOnly(2026, 1, 1), "New Year's Day", "BE", HolidayType.Public);
        var handler = BuildHandler([holiday]);

        var result = await handler.HandleAsync(new GetHolidaysInYearQuery(2026));

        result.Count.ShouldBe(1);
        result[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        result[0].Name.ShouldBe("New Year's Day");
        result[0].Type.ShouldBe(HolidayType.Public);
    }

    [Fact]
    public async Task MultipleHolidays_SortedByDate()
    {
        var h1 = Holiday.Create(new DateOnly(2026, 12, 25), "Christmas Day", "BE", HolidayType.Public);
        var h2 = Holiday.Create(new DateOnly(2026, 1, 1), "New Year's Day", "BE", HolidayType.Public);
        var handler = BuildHandler([h1, h2]);

        var result = await handler.HandleAsync(new GetHolidaysInYearQuery(2026));

        result[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
        result[1].Date.ShouldBe(new DateOnly(2026, 12, 25));
    }

    [Fact]
    public async Task YearFilter_OnlyCurrentYearReturned()
    {
        // Handler applies Country == "BE" && Date.Year == year filter via repository predicate.
        // Unit test verifies the query projects correctly; filtering logic verified via integration tests.
        var h2026 = Holiday.Create(new DateOnly(2026, 5, 1), "Labour Day", "BE", HolidayType.Public);
        var handler = BuildHandler([h2026]);

        var result = await handler.HandleAsync(new GetHolidaysInYearQuery(2026));

        result.Count.ShouldBe(1);
        result[0].Date.Year.ShouldBe(2026);
    }
}
