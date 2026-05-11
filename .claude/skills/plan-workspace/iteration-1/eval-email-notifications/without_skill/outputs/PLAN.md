# Plan: Email Notifications for Subscription Expiry (7 Days Before)

## Context and Codebase Observations

The existing API is a .NET 10 Minimal API using:

- **EF Core + SQLite** for persistence (via `AnimalDbContext`)
- **Vertical slice / module pattern**: each feature lives in `Modules/<Feature>/` with its own entity, DbContext, service, contracts, and endpoint registration
- **xUnit** for unit tests (in-memory EF Core) and `WebApplicationFactory` for integration tests
- **No existing background job infrastructure**, no email dependencies, no scheduler

The task is framed as "TypeScript Express API with a users table" but the actual codebase is .NET. The plan below matches the real stack. All new code follows the existing module conventions.

---

## Goal

Send an email notification to each user whose subscription expires exactly 7 days from now. The check should run automatically once per day (e.g. at 08:00).

---

## What Needs to Be Built

### 1. Users Module

Create `Modules/Users/` following the Animals pattern:

**`User.cs`** — entity

```
Id, Email, Name, SubscriptionExpiresAt (DateTimeOffset)
```

**`UserDbContext.cs`** (or extend a shared `AppDbContext`) — EF Core `DbSet<User>`

**`UserConfiguration.cs`** — fluent config (table name, index on email)

**`UserContracts.cs`** — request/response DTOs with `[Required]` / `[StringLength]` attributes

**`UserService.cs`** — CRUD + a targeted query:

```csharp
Task<List<User>> GetUsersExpiringInAsync(int days, CancellationToken ct)
// WHERE DATE(SubscriptionExpiresAt) = DATE(NOW + days)
```

**`UserEndpoints.cs`** — registers `/api/users` CRUD routes via `MapApiGroup`

**Migration** — `dotnet ef migrations add AddUsersTable`

---

### 2. Email Abstraction

Create `Common/Email/IEmailSender.cs`:

```csharp
public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body, CancellationToken ct = default);
}
```

Create `Common/Email/SmtpEmailSender.cs` — concrete implementation using `System.Net.Mail.SmtpClient` (or `MailKit` for a production-grade choice).

Configuration in `appsettings.json`:

```json
"Email": {
  "Host": "smtp.example.com",
  "Port": 587,
  "Username": "",
  "Password": "",
  "FromAddress": "noreply@example.com"
}
```

For local development, a **MailHog** or **Papercut** SMTP sink can be configured in `appsettings.Development.json`.

Register in `Program.cs`:

```csharp
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
```

---

### 3. Notification Service

Create `Modules/Users/SubscriptionExpiryNotifier.cs`:

```csharp
public class SubscriptionExpiryNotifier(UserService userService, IEmailSender emailSender, ILogger<SubscriptionExpiryNotifier> logger)
{
    public async Task NotifyExpiringUsersAsync(CancellationToken ct)
    {
        var users = await userService.GetUsersExpiringInAsync(days: 7, ct);
        foreach (var user in users)
        {
            await emailSender.SendAsync(
                to: user.Email,
                subject: "Your subscription expires in 7 days",
                body: $"Hi {user.Name}, your subscription expires on {user.SubscriptionExpiresAt:D}. Renew now to avoid interruption.",
                ct);
            logger.LogInformation("Expiry notification sent to {Email}", user.Email);
        }
    }
}
```

---

### 4. Background Job (Hosted Service)

Create `Common/BackgroundJobs/SubscriptionExpiryJob.cs` implementing `IHostedService` (via `BackgroundService`):

```csharp
public class SubscriptionExpiryJob(IServiceScopeFactory scopeFactory, ILogger<SubscriptionExpiryJob> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;
            var nextRun = now.Date.AddDays(1).AddHours(8); // next 08:00 UTC
            var delay = nextRun - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;

            await Task.Delay(delay, stoppingToken);

            using var scope = scopeFactory.CreateScope();
            var notifier = scope.ServiceProvider.GetRequiredService<SubscriptionExpiryNotifier>();
            try
            {
                await notifier.NotifyExpiringUsersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error running subscription expiry notifications");
            }
        }
    }
}
```

Register in `Program.cs`:

```csharp
builder.Services.AddScoped<SubscriptionExpiryNotifier>();
builder.Services.AddHostedService<SubscriptionExpiryJob>();
```

Note: `BackgroundService` runs on the host lifetime. It must create its own DI scope because scoped services (DbContext, notifier) cannot be injected directly into a singleton-lifetime hosted service.

---

### 5. Database Migration

```
dotnet ef migrations add AddUsersTable --project packages/api
dotnet ef database update --project packages/api
```

---

## Files to Create / Modify

| File                                                          | Action                          |
| ------------------------------------------------------------- | ------------------------------- |
| `packages/api/Modules/Users/User.cs`                          | Create                          |
| `packages/api/Modules/Users/UserConfiguration.cs`             | Create                          |
| `packages/api/Modules/Users/UserContracts.cs`                 | Create                          |
| `packages/api/Modules/Users/UserService.cs`                   | Create                          |
| `packages/api/Modules/Users/UserEndpoints.cs`                 | Create                          |
| `packages/api/Modules/Users/SubscriptionExpiryNotifier.cs`    | Create                          |
| `packages/api/Common/Email/IEmailSender.cs`                   | Create                          |
| `packages/api/Common/Email/SmtpEmailSender.cs`                | Create                          |
| `packages/api/Common/Email/EmailOptions.cs`                   | Create                          |
| `packages/api/Common/BackgroundJobs/SubscriptionExpiryJob.cs` | Create                          |
| `packages/api/Migrations/`                                    | New migration via EF CLI        |
| `packages/api/Program.cs`                                     | Modify — register new services  |
| `packages/api/appsettings.json`                               | Modify — add Email section      |
| `packages/api/appsettings.Development.json`                   | Modify — local SMTP sink config |
| `packages/api.csproj`                                         | Modify — add MailKit (optional) |

---

## Testing Plan

### Unit Tests (in `packages/api.tests/`)

**`SubscriptionExpiryNotifierTests.cs`**

- Mock `UserService` to return a list of users expiring in 7 days
- Mock `IEmailSender`
- Assert `SendAsync` is called once per user with correct `to` and `subject`
- Assert no emails sent when the list is empty

**`UserServiceTests.cs`**

- `GetUsersExpiringInAsync` returns only users whose expiry date is exactly 7 days out
- Uses in-memory EF Core (same pattern as `AnimalServiceTests`)

### Integration Tests (in `packages/api.tests.integration/`)

**`UserEndpointsTests.cs`**

- POST `/api/users` creates a user
- GET `/api/users/{id}` returns the user
- GET with non-existing id returns 404
- POST with invalid payload returns 400

**`SubscriptionExpiryJobTests.cs`** (optional, harder to test timing)

- Verify `SubscriptionExpiryNotifier` is resolved from DI correctly in an integration host

---

## Key Design Decisions

1. **No external scheduler (Hangfire/Quartz) needed** for a single daily job. `BackgroundService` is sufficient and avoids new dependencies.

2. **`IEmailSender` interface** keeps the notifier unit-testable and allows swapping SMTP for SendGrid/Mailgun later by registering a different implementation.

3. **Shared vs. separate DbContext**: given the small size of the project, adding `DbSet<User>` to a shared `AppDbContext` (renaming from `AnimalDbContext`) is cleaner long-term, but keeping separate contexts per module is also valid for strict isolation. Recommended: rename to `AppDbContext` and add both `DbSet`s.

4. **Idempotency**: the current design sends once per daily run. If re-run within the same day (e.g. after a crash), emails could be sent twice. For production, add a `NotificationSentAt` column to `User` and skip users already notified for this expiry.

5. **UTC dates**: store and compare all dates in UTC. Render in user-local time only in the email body if needed.

---

## Implementation Order

1. Create `Users` module (entity, config, contracts, service, endpoints) + migration
2. Add `IEmailSender` abstraction and `SmtpEmailSender` implementation
3. Add `SubscriptionExpiryNotifier`
4. Add `SubscriptionExpiryJob` (BackgroundService)
5. Wire everything in `Program.cs` and `appsettings.json`
6. Write unit tests for `UserService` and `SubscriptionExpiryNotifier`
7. Write integration tests for user endpoints
8. Manual smoke test with local SMTP sink (MailHog)
