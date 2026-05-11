This is a monorepo with TypeScript and C#

# Important notes

- The monorepo is powered by bun (no npm/pnpm)
- Always use bash — WSL environment, do not use PowerShell
- For C# conventions, see `docs/agents/conventions-c-sharp`
- For TypeScript conventions, see `docs/agents/conventions-typescript`
- When changing backend API contracts or endpoints (packages/api), regenerate the frontend TypeScript schema: `bun --filter web gen:api`
- When reporting back be concise and sacrifice grammar for conciseness
- Spawn subagents if needed keep context as lean as possible
