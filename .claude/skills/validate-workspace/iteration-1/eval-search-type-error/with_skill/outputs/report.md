## Validation Report

**Summary:** You added a client-side search filter (Input + useState + filter logic) to the animals list page in `packages/web/src/routes/animals/index.tsx`, but the `useState` type is wrong (`number` instead of `string`) causing TypeScript errors, and the planned test file was never created.

### Automated Checks

- ✗ TypeScript: 2 errors
  ```
  src/routes/animals/index.tsx(23,29): error TS2339: Property 'toLowerCase' does not exist on type 'number'.
  src/routes/animals/index.tsx(34,40): error TS2345: Argument of type 'string' is not assignable to parameter of type 'SetStateAction<number>'.
  ```
- ✓ Tests: 2 passing (pre-existing `sample.spec.ts` — the required `animals.spec.ts` was never created)
- ✓ Lint/format: clean

### Plan Coverage

- ✓ Step 1: `useState` imported from react, shadcn `Input` imported from `#/components/ui/input`
- ✗ Step 2: `useState` added but typed as `number` — `useState<number>(0)` instead of `useState<string>('')`
- ✓ Step 3: `Input` rendered above `<Table>` with correct placeholder and `value`/`onChange` bindings
- ✓ Step 4: Filter logic present — case-insensitive, checks both `name` and `species`, null-guards with `?? ''`
- ✗ Step 5: `packages/web/src/routes/animals/animals.spec.ts` was never created

### Issues Found

- `packages/web/src/routes/animals/index.tsx:20` — `useState<number>(0)` should be `useState<string>('')`. Root cause of both TypeScript errors.
- `packages/web/src/routes/animals/animals.spec.ts` — file does not exist. Plan required tests for: empty search returns all animals, name filter works case-insensitively, species filter works.

### Verdict: FAILING

**Before committing:**
1. Fix `useState<number>(0)` → `useState<string>('')` on line 20 — resolves both TypeScript errors
2. Create `packages/web/src/routes/animals/animals.spec.ts` with the three planned tests
