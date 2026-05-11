# Validation Report: Search Filter Feature

## Summary

The search filter implementation in `packages/web/src/routes/animals/index.tsx` is functionally correct and passes lint/type checks, but is **incomplete** relative to the plan: the required test file was never created.

## Plan vs Implementation Checklist

| Plan Step                                              | Status      | Notes                 |
| ------------------------------------------------------ | ----------- | --------------------- |
| Import `useState` from react                           | DONE        | Line 3 of `index.tsx` |
| Import shadcn `Input` component                        | DONE        | Line 6 of `index.tsx` |
| Add `searchTerm` state with `useState('')`             | DONE        | Line 20               |
| Add `<Input>` above `<Table>` with correct placeholder | DONE        | Lines 30-35           |
| Filter animals by name or species (case-insensitive)   | DONE        | Lines 22-25           |
| Create `animals.spec.ts` with filter tests             | **MISSING** | File does not exist   |

## Code Review

The implementation is clean and correct:

- `useState('')` initialises search state properly — empty string means all animals shown on first render.
- The filter uses `.toLowerCase()` on both sides — case-insensitive matching is correct.
- Null-safety applied with `?? ''` fallbacks for `name` and `species`.
- Controlled `Input` component, correct for React.

## Automated Checks

**`bun run check` (TypeScript + lint): PASSED**

- 24 files formatted correctly, no lint warnings, exit code 0.

**`bun run test` (vitest): PASSED — but only the pre-existing `sample.spec.ts` ran.**

```
Test Files  1 passed (1)
      Tests  2 passed (2)
```

The file specified by the plan — `packages/web/src/routes/animals/animals.spec.ts` — **does not exist**.

## Issues Found

### CRITICAL: Missing test file

**File:** `packages/web/src/routes/animals/animals.spec.ts` — Not created.

The plan explicitly required three test cases:

1. Empty search returns all animals
2. Name filter works case-insensitively
3. Species filter works

None were written. The test suite "passing" is misleading — it passes because the file was never created.

## Verdict: INCOMPLETE

The feature itself works correctly, but the test coverage required by the plan is entirely absent. Cannot be considered done until `packages/web/src/routes/animals/animals.spec.ts` is created.
