# Plan: Subscription Expiry Email Notifications

## Goal
Add a background job that queries the users table daily for subscriptions expiring in exactly 7 days and sends each affected user a notification email.

## Context
The codebase is a .NET 10 ASP.NET Core Minimal API (not TypeScript/Express — the task describes a hypothetical TypeScript Express project, so this plan is written for an equivalent TypeScript Express API that mirrors the patterns found here). Key patterns observed:

- **Module-per-feature layout**: each feature lives under `src/modules/<Feature>/` with co-located model, service, repository/db context, contracts, and route registration files.
- **Service layer**: thin route handlers delegate all business logic to a `<Feature>Service` class injected via DI.
- **Entity/model class**: plain class with typed properties; no decorators except validation ones on request contracts.
- **DI registration in the entry point**: all services, contexts, and new infrastructure (background jobs, mailer) are registered in `src/app.ts` (equivalent to `Program.cs`).
- **Validation via a reusable filter/middleware**: request validation is centralised in `src/common/middleware/validationMiddleware.ts`.
- **SQLite via an ORM** (EF Core here; the TypeScript equivalent would be Drizzle or Prisma against SQLite or Postgres).
- **Configuration from environment / appsettings**: connection strings and secrets come from environment variables or a config file.

## Files

- `src/modules/users/User.ts` — **modify**: add `subscriptionExpiresAt: Date | null` column to the User entity/model and its migration.
- `src/modules/users/userRepository.ts` — **modify**: add `findUsersExpiringOn(date: Date): Promise<User[]>` query that returns users whose `subscriptionExpiresAt` falls on the given date.
- `src/modules/users/userService.ts` — **modify**: add `getUsersExpiringIn(days: number): Promise<User[]>` method that builds the target date and delegates to the repository.
- `src/modules/notifications/emailService.ts` — **create**: thin wrapper around an email transport (Nodemailer or Resend SDK); exposes `sendSubscriptionExpiryWarning(to: string, expiresAt: Date): Promise<void>`.
- `src/modules/notifications/emailTemplates.ts` — **create**: plain-text and HTML templates for the subscription expiry warning email.
- `src/jobs/subscriptionExpiryJob.ts` — **create**: the scheduled job; on each run it calls `userService.getUsersExpiringIn(7)`, iterates results, calls `emailService.sendSubscriptionExpiryWarning` for each, and logs outcomes. Errors per user are caught individually so one failure does not abort the rest.
- `src/jobs/scheduler.ts` — **create**: initialises and starts all registered cron jobs using `node-cron` (or equivalent); registers `subscriptionExpiryJob` on a `0 8 * * *` schedule (08:00 UTC daily).
- `src/app.ts` — **modify**: import and start the scheduler after the Express app is created; inject `UserService` and `EmailService` into the scheduler via DI or direct instantiation.
- `src/config.ts` — **modify**: add `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS`, `SMTP_FROM` (or `RESEND_API_KEY`) to the validated config object.
- `migrations/<timestamp>_add_subscription_expires_at.ts` — **create**: migration that adds a nullable `subscription_expires_at` column (datetime) to the `users` table and creates an index on it for efficient date-range queries.
- `.env.example` — **modify**: document the new SMTP / email provider environment variables.

## Steps

1. Add `subscriptionExpiresAt: Date | null` to `src/modules/users/User.ts` (entity class and ORM schema definition).
2. Write and run the migration `migrations/<timestamp>_add_subscription_expires_at.ts`: `ALTER TABLE users ADD COLUMN subscription_expires_at DATETIME NULL`; add an index `CREATE INDEX idx_users_subscription_expires_at ON users(subscription_expires_at)`.
3. Add `findUsersExpiringOn(date: Date): Promise<User[]>` to `src/modules/users/userRepository.ts`. The query should match rows where `DATE(subscription_expires_at) = DATE(?)` to avoid time-of-day sensitivity.
4. Add `getUsersExpiringIn(days: number): Promise<User[]>` to `src/modules/users/userService.ts`. Compute target date as `new Date(today + days * 86400000)` (normalised to midnight UTC) and call `userRepository.findUsersExpiringOn(targetDate)`.
5. Install email dependency: `bun add nodemailer` (and `bun add -d @types/nodemailer`) or `bun add resend` depending on provider choice.
6. Create `src/modules/notifications/emailTemplates.ts` with `subscriptionExpiryWarningTemplate(user: { name: string; email: string }, expiresAt: Date): { subject: string; text: string; html: string }`.
7. Create `src/modules/notifications/emailService.ts`:
   - Constructor receives SMTP config from `src/config.ts` and creates the transport once.
   - `sendSubscriptionExpiryWarning(user: User, expiresAt: Date): Promise<void>` builds the message from the template and calls `transporter.sendMail(...)`.
8. Create `src/jobs/subscriptionExpiryJob.ts`:
   - Export `async function runSubscriptionExpiryJob(userService: UserService, emailService: EmailService): Promise<void>`.
   - Fetch users expiring in 7 days, loop, send email per user with individual try/catch, log success/failure with user ID (never log PII like email addresses in plain logs).
9. Create `src/jobs/scheduler.ts`:
   - Import `node-cron` (`bun add node-cron`, `bun add -d @types/node-cron`).
   - Export `startScheduler(userService: UserService, emailService: EmailService): void` which calls `cron.schedule('0 8 * * *', () => runSubscriptionExpiryJob(...))`.
10. Modify `src/app.ts` to instantiate `EmailService` (with config), then call `startScheduler(userService, emailService)` after the Express app starts listening.
11. Update `src/config.ts` to read and validate `SMTP_HOST`, `SMTP_PORT`, `SMTP_USER`, `SMTP_PASS`, `SMTP_FROM` (throw at startup if any required value is missing).
12. Update `.env.example` to document all new environment variables with placeholder values and comments.

## Tests

- **`userRepository.test.ts`** — seeds a users table with three users: one expiring in 7 days, one in 6 days, one in 8 days. Asserts `findUsersExpiringOn(targetDate)` returns exactly the 7-day user.
- **`userService.test.ts`** — unit test for `getUsersExpiringIn(7)`: mock repository, assert correct date is computed (use a fixed `Date.now()` via `vi.setSystemTime`) and passed to `findUsersExpiringOn`.
- **`emailService.test.ts`** — mock the Nodemailer transport; call `sendSubscriptionExpiryWarning`; assert `sendMail` was called once with the correct `to`, `subject`, and a non-empty `html` body.
- **`subscriptionExpiryJob.test.ts`** — mock `userService.getUsersExpiringIn` to return two users; mock `emailService.sendSubscriptionExpiryWarning` to reject for the second user; assert the job still attempts to send to both and logs one error without throwing.
- **`scheduler.test.ts`** — assert `cron.schedule` is called with `'0 8 * * *'` and a function when `startScheduler` is invoked.
- **Integration test** (optional, using a real SQLite in-memory DB) — insert a user with `subscriptionExpiresAt = today + 7`, run the job against a mock SMTP server (e.g. `smtp-server` package), assert one email was received.

## Edge Cases

- **User has no email address**: guard in `runSubscriptionExpiryJob` — skip users with null/empty `email` and log a warning (do not throw).
- **Subscription already cancelled but date still in DB**: consider adding a `subscriptionStatus` field check so cancelled subscriptions are excluded from the query.
- **Duplicate sends**: if the job runs more than once in a day (restart, re-deploy), the same user may receive multiple emails. Mitigate with a `last_expiry_notification_sent_at` column on the user row, or by checking it was not already sent today before sending.
- **Time zone sensitivity**: `DATE(subscription_expires_at)` comparisons in SQLite are UTC. If users are in various time zones, the 7-day window may be off by up to a day. Document this limitation and consider whether the product requires TZ-aware scheduling.
- **Large user volumes**: if thousands of users expire the same day, sending sequentially may be slow. Batch with `Promise.allSettled` over chunks of 50, or use a queue (BullMQ / pg-boss) instead of direct in-job sends.
- **SMTP transport failures**: if the entire transport is unavailable, all sends fail. The job should catch at the top level, log the outage, and (optionally) alert ops — but still exit cleanly so the process does not crash.
- **Missing `subscriptionExpiresAt` data**: existing users will have `null`; the query's `DATE(subscription_expires_at) = ?` predicate naturally excludes NULLs, so no action needed there.

## Assumptions

- The existing `users` table has at minimum `id`, `email`, and `name` columns; adding `subscription_expires_at` is a non-breaking migration.
- The project uses a migration runner (e.g. Drizzle `migrate`, Prisma, or Knex) that supports raw SQL or schema-diff migrations.
- `node-cron` is acceptable for scheduling; if the app runs on multiple instances behind a load balancer, a distributed lock or an external scheduler (AWS EventBridge, cron Kubernetes Job) should be preferred — but this is out of scope here.
- Nodemailer (SMTP) is the email transport default; switching to a transactional API (Resend, SendGrid) requires changing only `emailService.ts`.
- Tests use Vitest (`bun test` runs the Vitest suite), consistent with the project's bun-first tooling.
- No authentication or authorisation is required for the job trigger itself (it runs server-side on a timer, not via an HTTP endpoint).
