using Moq;
using Shouldly;
using Tsz.Modules.Contracts.Domain.Contracts;
using Tsz.Modules.Contracts.Features;
using Tsz.Modules.Customers.Contracts;
using Tsz.Modules.Customers.Contracts.Queries;

namespace Tsz.Api.Tests.Modules.Contracts.Features;

public class CreateContractValidatorTests
{
    private static CreateContractValidator BuildValidator(bool customerExists = true)
    {
        var customers = new Mock<ICustomersAccessModule>();
        customers
            .Setup(m => m.ExecuteQueryAsync(It.IsAny<CustomerExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerExists);

        return new CreateContractValidator(customers.Object);
    }

    private static CreateContractCommand ValidCmd(
        string? subject = "Engagement Alpha",
        DateOnly? start = null,
        DateOnly? end = null,
        Guid? customerId = null) =>
        new(
            subject ?? "Engagement Alpha",
            customerId ?? Guid.NewGuid(),
            start ?? new DateOnly(2026, 1, 1),
            end);

    [Fact]
    public async Task Valid_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task EmptySubject_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(subject: ""));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateContractCommand.Subject));
    }

    [Fact]
    public async Task SubjectTooLong_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(subject: new string('x', 257)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateContractCommand.Subject));
    }

    [Fact]
    public async Task EndBeforeStart_Fails()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(
            start: new DateOnly(2026, 6, 1),
            end: new DateOnly(2026, 5, 31)));
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.EndBeforeStart.Code);
    }

    [Fact]
    public async Task EndEqualToStart_Passes()
    {
        var date = new DateOnly(2026, 6, 1);
        var result = await BuildValidator().ValidateAsync(ValidCmd(start: date, end: date));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task NullEnd_Passes()
    {
        var result = await BuildValidator().ValidateAsync(ValidCmd(start: new DateOnly(2026, 1, 1), end: null));
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CustomerNotFound_Fails()
    {
        var result = await BuildValidator(customerExists: false).ValidateAsync(ValidCmd());
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == ContractErrors.CustomerNotFound.Code);
    }
}
