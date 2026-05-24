using Tsz.Infrastructure.Errors;

namespace Tsz.Modules.Timesheets.Domain.Timesheets;

public sealed class TimesheetErrors : ErrorCodeBase<TimesheetErrors>
{
    private TimesheetErrors(string code, string message, ErrorCategory category)
        : base(code, message, category) { }

    public static readonly TimesheetErrors NotDraft =
        new("ERR_TIMESHEET_NOT_DRAFT",
            "The timesheet week is not in Draft status and cannot be modified.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors DateOutsideWeek =
        new("ERR_TIMESHEET_DATE_OUTSIDE_WEEK",
            "One or more booking dates fall outside the requested week.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors DateNotBusinessDay =
        new("ERR_TIMESHEET_DATE_NOT_BUSINESS_DAY",
            "One or more booking dates are not business days.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors ContractTaskNotEligible =
        new("ERR_TIMESHEET_CONTRACT_TASK_NOT_ELIGIBLE",
            "One or more contract tasks are not active for this user in this week.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors InvalidDurationHours =
        new("ERR_TIMESHEET_INVALID_DURATION_HOURS",
            "Duration hours must be a multiple of 0.25 between 0.25 and 8.00.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors NotFound =
        new("ERR_TIMESHEET_NOT_FOUND",
            "The timesheet week was not found.",
            ErrorCategory.NotFound);

    public static readonly TimesheetErrors NotSubmittable =
        new("ERR_TIMESHEET_NOT_SUBMITTABLE",
            "The timesheet week cannot be submitted because it is not in Draft status.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors NotApprovable =
        new("ERR_TIMESHEET_NOT_APPROVABLE",
            "The timesheet week cannot be approved because it is not in Submitted status.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors NotReopenable =
        new("ERR_TIMESHEET_NOT_REOPENABLE",
            "The timesheet week cannot be reopened because it is not in Submitted or Approved status.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors LeaveTypeNotFound =
        new("ERR_TIMESHEET_LEAVE_TYPE_NOT_FOUND",
            "One or more leave types do not exist.",
            ErrorCategory.Validation);

    public static readonly TimesheetErrors LeaveAllowanceExceeded =
        new("ERR_TIMESHEET_LEAVE_ALLOWANCE_EXCEEDED",
            "The requested leave bookings exceed the yearly allowance for one or more leave types.",
            ErrorCategory.Validation);
}
