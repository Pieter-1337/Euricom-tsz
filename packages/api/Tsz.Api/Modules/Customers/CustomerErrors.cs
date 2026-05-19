using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.Customers;

public sealed class CustomerErrors : ErrorCodeBase<CustomerErrors>
{
    private CustomerErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly CustomerErrors NotFound =
        new("ERR_CUSTOMER_NOT_FOUND", "Customer not found.", ErrorCategory.NotFound);
}
