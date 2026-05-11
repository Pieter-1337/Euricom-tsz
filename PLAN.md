# Plan: Add search filter to animals list

## Goal

Add a client-side search input above the animals table so users can filter by name or species.

## Context

The animals list is at `packages/web/src/routes/animals/index.tsx`. It uses a TanStack Start server loader that fetches all animals and renders them with shadcn Table components. Client-side filtering is appropriate since the dataset is small.

## Files

- `packages/web/src/routes/animals/index.tsx` — modify: add `useState` for search term, shadcn `Input` component above the table, and filter logic
- `packages/web/src/routes/animals/animals.spec.ts` — create: tests for the filter logic

## Steps

1. Import `useState` from react and the shadcn `Input` component (`#/components/ui/input`)
2. Add `const [searchTerm, setSearchTerm] = useState('')` inside the `Animals` component
3. Add a shadcn `Input` above the `<Table>` with placeholder "Search by name or species..." bound to `searchTerm`
4. Filter the animals array before rendering: keep entries where `name` or `species` includes the search term (case-insensitive)
5. Write tests in `packages/web/src/routes/animals/animals.spec.ts` verifying: empty search returns all animals, name filter works case-insensitively, species filter works

## Tests

- `packages/web/src/routes/animals/animals.spec.ts`: empty search returns all animals, name filter is case-insensitive, species filter works

## Edge Cases

- Empty search string must show all animals (not zero results)
- Search must be case-insensitive

## Assumptions

- Client-side filtering is sufficient for current data volume
- No debouncing needed yet
