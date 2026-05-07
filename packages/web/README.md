# web-tanstack-start

TanStack Start frontend for the dotnet-spa template. Uses TanStack Router (file-based), TanStack Query, Tailwind CSS v4, and connects to the .NET API.

## Getting Started

```bash
# from the workspace root
bun run dev:tanstack

# or from this directory
bun run dev
```

The dev server runs on http://localhost:3000. Make sure the API is running (`bun run dev:api` from the root).

## Scripts

| Script    | Description              |
| --------- | ------------------------ |
| `dev`     | Start dev server         |
| `build`   | Production build         |
| `preview` | Preview production build |
| `test`    | Run tests with Vitest    |

## Project Structure

```
src/
  api/              API client (openapi-fetch, generated types)
  components/       Shared components
  integrations/     TanStack Query provider setup
  lib/              Utilities (cn helper)
  routes/           File-based routes
    __root.tsx      Root layout with nav
    index.tsx       Home page
    animals.tsx     Animals table (fetches from API)
  router.tsx        Router factory
  styles.css        Tailwind CSS entry
```

## Adding Routes

Add a new file in `src/routes/`. TanStack Router auto-generates the route tree.

```tsx
import { createFileRoute } from '@tanstack/react-router';

export const Route = createFileRoute('/my-page')({
  component: MyPage,
});

function MyPage() {
  return <h1>My Page</h1>;
}
```

## Adding shadcn/ui Components

```bash
npx shadcn@latest add button
```
