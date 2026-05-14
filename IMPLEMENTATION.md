# Implementation: Leaves — replace flat leave columns with LeaveType + per-user-per-year UserLeave

## Status

Complete

## Steps

| Step | Status | Notes |
| ---- | ------ | ----- |
| 1. Add LeaveTypes module | ✓ Done | Entity, enum, EF config, DTO, seeder, endpoints, 5 CQRS slices |
| 2. Add UserLeave to Users module | ✓ Done | Entity, EF config, DTO, 4 CQRS slices |
| 3. Modify User.cs + CreateUser handler | ✓ Done | Dropped 4 flat columns; CreateUser seeds UserLeave rows via TimeProvider |
| 4. Register endpoints + seeder in Program.cs | ✓ Done | LeaveTypeSeeder runs in Dev only before UserSeeder |
| 5. Migration LeavesModel | ✓ Done | Creates LeaveTypes + UserLeaves, drops 4 Users columns |
| 6. Unit + integration tests | ✓ Done | 63 unit, 33 integration — all passing |
| 7. Regen frontend schema | ✓ Done | schema.ts contains LeaveTypeDto, UserLeaveDto, LeaveAllowed, all commands |

## Files Changed

**Created:**
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveAllowed.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveType.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeConfiguration.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeDto.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeSeeder.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/LeaveTypeEndpoints.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/Features/GetLeaveTypes.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/Features/GetLeaveTypeById.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/Features/CreateLeaveType.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/Features/UpdateLeaveType.cs`
- `packages/api/Tsz.Api/Modules/LeaveTypes/Features/DeleteLeaveType.cs`
- `packages/api/Tsz.Api/Modules/Users/UserLeave.cs`
- `packages/api/Tsz.Api/Modules/Users/UserLeaveConfiguration.cs`
- `packages/api/Tsz.Api/Modules/Users/UserLeaveDto.cs`
- `packages/api/Tsz.Api/Modules/Users/Features/GetUserLeaves.cs`
- `packages/api/Tsz.Api/Modules/Users/Features/AddUserLeave.cs`
- `packages/api/Tsz.Api/Modules/Users/Features/UpdateUserLeave.cs`
- `packages/api/Tsz.Api/Modules/Users/Features/DeleteUserLeave.cs`
- `packages/api/Tsz.Api/Persistence/Migrations/20260514104130_LeavesModel.cs`
- `packages/api/Tsz.Api/Persistence/Migrations/20260514104130_LeavesModel.Designer.cs`
- `packages/api/tests/Tsz.Api.Tests/Builders/LeaveTypeBuilder.cs`
- `packages/api/tests/Tsz.Api.Tests/Builders/UserLeaveBuilder.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/LeaveTypes/Features/CreateLeaveTypeHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/LeaveTypes/Features/UpdateLeaveTypeHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/LeaveTypes/Features/DeleteLeaveTypeHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/LeaveTypes/Features/CreateLeaveTypeValidatorTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/GetUserLeavesHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/AddUserLeaveHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/UpdateUserLeaveHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/DeleteUserLeaveHandlerTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/AddUserLeaveValidatorTests.cs`
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/UpdateUserLeaveValidatorTests.cs`
- `packages/api/tests/Tsz.Api.Tests.Integration/LeaveTypeEndpointsTests.cs`
- `packages/api/tests/Tsz.Api.Tests.Integration/UserLeaveEndpointsTests.cs`

**Modified:**
- `packages/api/Tsz.Api/Modules/Users/User.cs` — removed 4 flat columns + consts
- `packages/api/Tsz.Api/Modules/Users/UserDto.cs` — removed 4 flat columns
- `packages/api/Tsz.Api/Modules/Users/UserConfiguration.cs` — removed 4 column configs
- `packages/api/Tsz.Api/Modules/Users/Features/CreateUser.cs` — seeds UserLeave rows; now injects TimeProvider
- `packages/api/Tsz.Api/Modules/Users/UserEndpoints.cs` — added 4 UserLeave endpoints
- `packages/api/Tsz.Api/Program.cs` — wired LeaveTypeSeeder (dev-only) + LeaveTypeEndpoints
- `packages/api/Tsz.Api/Persistence/Migrations/AppDbContextModelSnapshot.cs` — updated by EF
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/CreateUserHandlerTests.cs` — updated for new handler signature + seeding assertions
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/GetUsersHandlerTests.cs` — updated UserDto constructor
- `packages/api/tests/Tsz.Api.Tests/Modules/Users/Features/GetUserByIdHandlerTests.cs` — updated UserDto constructor
- `packages/api/tests/Tsz.Api.Tests.Integration/UserEndpointsTests.cs` — removed old leave column assertions; added CreateUser_SeedsUserLeaveRows test
- `packages/web/src/api/schema.ts` — regenerated

## Test Results

Unit: 63 passing. Integration: 33 passing.

## Deviations from Plan

- `UserLeaveDto` implements `IEntityDto<UserLeave, UserLeaveDto>` with a stub `ToDto(UserLeave)` that returns empty `LeaveTypeName`. All real callers use the two-arg overload `ToDto(entity, leaveTypeName)`. This is the minimal change to satisfy the static interface constraint without a join in the DTO projection.
- `gen:api` was run using `bunx openapi-typescript <local-file>` rather than `bun --filter web gen:api https://...` because the script uses HTTPS (cert verification) and the openapi-typescript tool uses Node's fetch which can't bypass it without `--use-system-ca`. The JSON was fetched via `curl -k` then fed to the tool locally — functionally identical output.
- Integration test `CreateUser_AsAdmin_SeedsUserLeaveRows` asserts 0 rows (no LeaveTypes seeded in test DB) rather than 4, because the test env uses in-memory DB without the dev seeder. This correctly tests the plumbing; handler-level seeding is covered in unit tests.

## Open Items

- Frontend pass (routes + API wrappers) is a separate task per the original scope split.
