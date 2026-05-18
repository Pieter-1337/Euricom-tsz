This is a monorepo with TypeScript and C#

# Important notes

- The monorepo is powered by bun (no npm/pnpm)
- For C# conventions, see `docs/agents/conventions-c-sharp`
- For TypeScript conventions, see `docs/agents/conventions-typescript`
- When changing backend API contracts or endpoints (packages/api/Tsz.Api), regenerate the frontend TypeScript schema: `bun --filter web gen:api`
- This project follows the Euricom Tech Tribes design system. Read `packages/web/docs/DESIGN.md` before styling any new component or page.
- When reporting back be concise and sacrifice grammar for conciseness
- Spawn subagents if needed keep context as lean as possible (important!)
