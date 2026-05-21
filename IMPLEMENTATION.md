# Implementation: Contracts module — scaffold + create-contract (issue #4)

## Status

Complete

## Steps

| Step | Status | Notes |
| ---- | ------ | ----- |
| 1. Scaffold csproj files | ✓ Done | Both .csproj files exist; refs added to Api, Tests, Tests.Integration, and Users |
| 2. Public surface (Tsz.Modules.Contracts.Contracts) | ✓ Done | IContractsAccessModule + IsUserReferencedAsClientManagerOnContractQuery |
| 3. CustomerExistsQuery in Tsz.Modules.Customers.Contracts | ✓ Done | Query + handler present |
| 4. Contract aggregate + EF configuration | ✓ Done | Contract, ContractConfiguration, ContractDto, ContractErrors |
| 5. CrossModule handler + module wiring | ✓ Done | CrossModule handler, ContractsAccessModule, ContractsModule, GlobalUsings |
| 6. CreateContract feature + endpoint | ✓ Done | Validator (subject, dates, CustomerExistsQuery, UserExistsQuery + role), Handler (MAX+1 number), ContractEndpoints |
| 7. Register module in Program.cs + AppDbContext | ✓ Done | ContractsModule in modules list; ApplyConfigurationsFromAssembly added |
| 8. EF Core migration | ✓ Done | Migration 20260521091459_AddContracts creates Contracts table + filtered unique index |
| 9. Extend DeleteUserValidator | ✓ Done | IContractsAccessModule injected; NotAssignedAsClientManager checks both Customers and Contracts |
| 10. Unit tests | ✓ Done | CreateContractHandlerTests, CreateContractValidatorTests, IsUserReferencedAsClientManagerOnContractQueryHandlerTests, DeleteUserValidatorTests (4 new scenarios), ContractBuilder |
| 11. Integration tests | ✓ Done | ContractEndpointsTests: happy-path 201, non-admin 403, delete-user-blocked-by-contract 409 |
| 12. Frontend: Create contract page | ✓ Done | schemas.ts, server-fns.ts, contracts.server.ts, contracts.ts, ContractCreateForm, /admin/contracts/new, /admin/contracts/index placeholder |

## Files Changed

### New (backend)
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts.Contracts/Tsz.Modules.Contracts.Contracts.csproj`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts.Contracts/IContractsAccessModule.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts.Contracts/Queries/IsUserReferencedAsClientManagerOnContractQuery.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Tsz.Modules.Contracts.csproj`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/GlobalUsings.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/ContractsAccessModule.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/ContractsModule.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Domain/Contracts/Contract.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Domain/Contracts/ContractConfiguration.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Domain/Contracts/ContractDto.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Domain/Contracts/ContractErrors.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/CrossModule/IsUserReferencedAsClientManagerOnContractQueryHandler.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Features/CreateContract.cs`
- `packages/api/Modules/Contracts/Tsz.Modules.Contracts/Endpoints/ContractEndpoints.cs`
- `packages/api/Modules/Customers/Tsz.Modules.Customers.Contracts/Queries/CustomerExistsQuery.cs`
- `packages/api/Modules/Customers/Tsz.Modules.Customers/CrossModule/CustomerExistsQueryHandler.cs`
- `packages/api/Tsz.Api/Persistence/Migrations/20260521091459_AddContracts.cs`
- `packages/api/Tsz.Api/Persistence/Migrations/20260521091459_AddContracts.Designer.cs`
- `packages/api/Tsz.Api.Tests/Builders/ContractBuilder.cs`
- `packages/api/Tsz.Api.Tests/Modules/Contracts/Features/CreateContractHandlerTests.cs`
- `packages/api/Tsz.Api.Tests/Modules/Contracts/Features/CreateContractValidatorTests.cs`
- `packages/api/Tsz.Api.Tests/Modules/Contracts/CrossModule/IsUserReferencedAsClientManagerOnContractQueryHandlerTests.cs`
- `packages/api/Tsz.Api.Tests.Integration/ContractEndpointsTests.cs`

### Edited (backend)
- `packages/api/Tsz.Api/Tsz.Api.csproj` — added Contracts module refs
- `packages/api/Tsz.Api/Program.cs` — added ContractsModule registration
- `packages/api/Tsz.Api/Persistence/AppDbContext.cs` — added ApplyConfigurationsFromAssembly for ContractsModule
- `packages/api/Tsz.Api/Persistence/Migrations/AppDbContextModelSnapshot.cs` — updated
- `packages/api/Tsz.Api.Tests/Tsz.Api.Tests.csproj` — added Contracts module refs
- `packages/api/Tsz.Api.Tests.Integration/Tsz.Api.Tests.Integration.csproj` — added Contracts module refs
- `packages/api/Modules/Users/Tsz.Modules.Users/Tsz.Modules.Users.csproj` — added ref to Contracts.Contracts
- `packages/api/Modules/Users/Tsz.Modules.Users/Features/DeleteUser.cs` — injected IContractsAccessModule; NotAssignedAsClientManager checks both modules
- `packages/api/Tsz.Api.Tests/Modules/Users/Features/DeleteUserValidatorTests.cs` — 4 new CM scenarios

### New (frontend)
- `packages/web/src/api/contracts.ts`
- `packages/web/src/api/contracts.server.ts`
- `packages/web/src/features/contracts/schemas.ts`
- `packages/web/src/features/contracts/server-fns.ts`
- `packages/web/src/features/contracts/components/contract-create-form.tsx`
- `packages/web/src/routes/_protected/admin/contracts/new.tsx`
- `packages/web/src/routes/_protected/admin/contracts/index.tsx`

### Edited (frontend)
- `packages/web/src/api/schema.ts` — regenerated; includes ContractDto + CreateContractCommand
- `packages/web/src/features/customers/server-fns.ts` — fetchAllCustomers added (used by contract form)
- `packages/web/src/routeTree.gen.ts` — auto-regenerated with new contract routes

## Test Results

- Unit tests: 187/187 passed
- Integration tests: 64/64 passed
- TypeScript: no errors

## Deviations from Plan

- Endpoint uses `TypedResults.Created($"/api/contracts/{dto.Id}", dto)` (URL string) rather than `CreatedAtRoute` — no named route registered, functionally equivalent.
- `ContractErrors` includes an extra `NotFound` error code beyond the four listed in the plan — consistent with the CustomerErrors pattern.

## Open Items

None.
