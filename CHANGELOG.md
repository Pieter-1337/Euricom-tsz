## Changed

- Consolidated vitest config into vite.config.ts and added `@tests` path alias for cleaner test imports.

## Added

- Added product requirements, architecture overview, and agent convention docs (TypeScript, C#, domain, issue tracker, triage labels) under `docs/`.
- Added Ref and Exa MCP servers to project and VS Code MCP config for documentation lookup and web search support.
- Added changelog step to the commit skill so every commit includes a user-friendly summary in CHANGELOG.md.
- Extracted shared `client.ts` with `ApiRequestError` and error middleware; added `animals.spec.ts` test suite and `tests/fetch-util` helpers.

## Fixed

- Fixed TypeScript errors in the animal edit form: removed stale `age === ''` comparison and aligned the save schema with the required `age: number` DTO field.
