# Validation Report: Search Filter Feature

## Summary

The implementation has **2 TypeScript type errors** that cause `bun run typecheck` to fail. The test file required by the plan was **not created**. Formatting and linting pass cleanly.

## Checks Run

| Check | Command | Result |
|---|---|---|
| TypeScript type check | `bun run typecheck` | FAIL (2 errors) |
| Format + lint | `bun run check` | PASS |
| Tests | `bun run test` | PASS (2 tests, but not the required ones) |

## Issues Found

### 1. Type Error: `useState` initialized with `number` instead of `string` (BLOCKING)

File: `packages/web/src/routes/animals/index.tsx`, line 20

```ts
const [searchTerm, setSearchTerm] = useState<number>(0);
```

The state is typed as `number` but must be `string`. Two downstream TypeScript errors:

**Error 1** (line 23): `Property 'toLowerCase' does not exist on type 'number'`
**Error 2** (line 34): `Argument of type 'string' is not assignable to parameter of type 'SetStateAction<number>'`

**Fix:**
```ts
const [searchTerm, setSearchTerm] = useState<string>('');
```

### 2. Missing test file (BLOCKING per plan)

Expected: `packages/web/src/routes/animals/animals.spec.ts` — file does not exist.

The plan requires three test cases: empty search returns all, name filter case-insensitive, species filter works. The 2 passing tests come from `sample.spec.ts` — unrelated to the search feature.

## What Works

- Imports correct: `useState` from React, `Input` from `#/components/ui/input`
- Component structure: `Input` placed above `<Table>` with correct placeholder
- Filter logic structurally correct (case-insensitive `.includes()`)
- Nullish coalescing (`?? ''`) on optional fields
- Formatting and lint clean

## Verdict: FAILING

1. Fix `useState<number>(0)` → `useState<string>('')` in `index.tsx` line 20
2. Create `packages/web/src/routes/animals/animals.spec.ts` with the three tests
