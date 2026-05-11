# Always use bun (no npm/pnpm)
# Always use bash we are running claude in a WSL environment don't use powershell!
# When changing backend API contracts or endpoints (packages/api), regenerate the frontend TypeScript schema: `bun --filter web gen:api`