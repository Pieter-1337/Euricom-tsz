using Tsz.Infrastructure.Errors;

namespace Tsz.Api.Modules.Customers;

public sealed class CustomerErrors : ErrorCodeBase<CustomerErrors>
{
    private CustomerErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly CustomerErrors NotFound =
        new("ERR_CUSTOMER_NOT_FOUND", "Customer not found.", ErrorCategory.NotFound);

    public static readonly CustomerErrors ClientManagerNotFound =
        new("ERR_CUSTOMER_CLIENT_MANAGER_NOT_FOUND",
            "The assigned client manager does not exist.",
            ErrorCategory.NotFound);

    public static readonly CustomerErrors ClientManagerMissingRole =
        new("ERR_CUSTOMER_CLIENT_MANAGER_MISSING_ROLE",
            "The assigned user does not have the ClientManager role.",
            ErrorCategory.Validation);
}
