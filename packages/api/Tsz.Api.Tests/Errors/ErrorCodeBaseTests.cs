using Shouldly;
using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Tests.Errors;

public class ErrorCodeBaseTests
{
    private sealed class TestErrors : ErrorCodeBase<TestErrors>
    {
        private TestErrors(string code, string message, ErrorCategory category)
            : base(code, message, category) { }

        public static readonly TestErrors AlphaError =
            new("ERR_ALPHA", "Alpha failed.", ErrorCategory.Validation);

        public static readonly TestErrors BetaError =
            new("ERR_BETA", "Beta conflict.", ErrorCategory.Conflict);
    }

    [Fact]
    public void Code_StartsWithErrPrefix()
    {
        TestErrors.AlphaError.Code.ShouldStartWith("ERR_");
        TestErrors.BetaError.Code.ShouldStartWith("ERR_");
    }

    [Fact]
    public void Category_IsExposed()
    {
        TestErrors.AlphaError.Category.ShouldBe(ErrorCategory.Validation);
        TestErrors.BetaError.Category.ShouldBe(ErrorCategory.Conflict);
    }

    [Fact]
    public void Instances_AreDistinct()
    {
        TestErrors.AlphaError.ShouldNotBeSameAs(TestErrors.BetaError);
        TestErrors.AlphaError.Code.ShouldNotBe(TestErrors.BetaError.Code);
    }

    [Fact]
    public void FromCode_RoundTrips_KnownCode()
    {
        var found = TestErrors.FromCode("ERR_ALPHA");
        found.ShouldNotBeNull();
        found.ShouldBeSameAs(TestErrors.AlphaError);
    }

    [Fact]
    public void FromCode_ReturnsNull_ForUnknownCode()
    {
        var found = TestErrors.FromCode("ERR_DOES_NOT_EXIST");
        found.ShouldBeNull();
    }

    [Fact]
    public void Constructor_Throws_WhenCodeMissingPrefix()
    {
        Should.Throw<ArgumentException>(() =>
        {
            _ = new BadPrefixErrors();
        });
    }

    private sealed class BadPrefixErrors : ErrorCodeBase<BadPrefixErrors>
    {
        public BadPrefixErrors() : base("NO_PREFIX", "bad", ErrorCategory.Validation) { }
    }

    [Fact]
    public void CommonErrors_AllHaveErrPrefix()
    {
        CommonErrors.Required.Code.ShouldStartWith("ERR_");
        CommonErrors.Invalid.Code.ShouldStartWith("ERR_");
        CommonErrors.InvalidEmail.Code.ShouldStartWith("ERR_");
    }

    [Fact]
    public void CommonErrors_AllAreValidationCategory()
    {
        CommonErrors.Required.Category.ShouldBe(ErrorCategory.Validation);
        CommonErrors.Invalid.Category.ShouldBe(ErrorCategory.Validation);
        CommonErrors.InvalidEmail.Category.ShouldBe(ErrorCategory.Validation);
    }
}
