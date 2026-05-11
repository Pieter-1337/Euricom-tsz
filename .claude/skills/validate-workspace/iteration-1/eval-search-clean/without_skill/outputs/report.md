# Validation Report: Search Filter Feature

## Summary

The search filter implementation is **mostly correct and complete**, with one notable gap: the test file tests a standalone helper function rather than the actual component logic. Everything else aligns with the plan.

## Plan vs Implementation Checklist

| Plan requirement | Status | Notes |
|---|---|---|
| Import `useState` from React | PASS | Line 3 of `index.tsx` |
| Import shadcn `Input` from `#/components/ui/input` | PASS | Line 6 of `index.tsx` |
| `const [searchTerm, setSearchTerm] = useState('')` inside component | PASS | Line 20 of `index.tsx` |
| `Input` above `<Table>` with placeholder "Search by name or species..." | PASS | Lines 30-35 of `index.tsx` |
| Filter by `name` or `species`, case-insensitive | PASS | Lines 22-25 of `index.tsx` |
| Test: empty search returns all animals | PASS | `animals.spec.ts` line 15 |
| Test: name filter is case-insensitive | PASS | `animals.spec.ts` line 19 |
| Test: species filter works | PASS | `animals.spec.ts` line 23 |

## Code Review: `index.tsx`

- `useState('')` initialises to empty string — empty search shows all animals (edge case covered).
- Filter uses `.toLowerCase()` on both sides — case-insensitivity correct.
- Null-safety guards `(animal.name ?? '')` appropriate: `AnimalDTO` marks both as optional.
- Controlled `Input` with `value={searchTerm}` and `onChange`.
- Redundant `: AnimalDTO` type annotations on filter/map callbacks — not a bug, just noise.

## Code Review: `animals.spec.ts`

All three plan-required test cases are covered. However:

**Issue: logic duplication instead of shared extraction.** The spec defines its own `filterAnimals` function (lines 3-6) rather than importing from the component. Changes to `index.tsx` won't be caught by these tests because they test a copy.

Also: test fixture objects don't include optional-field `undefined` cases — the `?? ''` guard in production code is not exercised by the tests.

## Issues Found

| Severity | Location | Issue |
|---|---|---|
| Medium | `animals.spec.ts` | Tests a copy of filter logic, not production code |
| Low | `animals.spec.ts` | No test for undefined name/species (real schema possibility) |
| Low | `index.tsx` | Redundant explicit `: AnimalDTO` annotations |

## Verdict: PASS with recommendations

Implementation fulfils every plan requirement. The filter logic, edge cases, and nullable fields are handled correctly. The main weakness is tests exercising duplicate logic — not a blocker for merging but worth a follow-up.

Commands to run before merging:
```bash
bun run typecheck   # TypeScript
bun run check       # Format + lint
bun run test        # vitest
```
