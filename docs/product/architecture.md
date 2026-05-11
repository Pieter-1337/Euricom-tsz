# Architecture

This is the first draft of the architecture, for the current architecture and folder structure look in the codebase, not in this document.

<img src="./images/architecture-tanstack-start.excalidraw.png" alt="Architecture" width="500" height="500">

**Frontend: TanStack Start**

- TypeScript
- Full-stack Framework powered by TanStack Start for React
  - React 19, Vite+, TanStack Start + devtools
  - File-based routing via TanStack Router
  - Server Functions
- Styling with [Tailwindcss v4](https://tailwindcss.com/)
- Components library with [shadcn](https://ui.shadcn.com/)
- Form handling with [Tanstack Form](https://tanstack.com/form)
- Unit testing with Vite+ (Vitest) + [@testing-library/react](https://testing-library.com/docs/react-testing-library/intro/) + jsdom
- Linting & formatting via [vite-plus](https://www.npmjs.com/package/vite-plus) (`vp check`), which wraps OXLint + OXFmt
- OAuth2 authentication
  - Azure Entra ID
  - One app registration shared by both frontend and API
  - Token(s) are stored securely—either in encrypted cookies or server-side in the db
  - Optional: Use [BetterAuth](https://better-auth.com/) as the authentication framework
- API TS type generation with [openapi-ts.dev](https://openapi-ts.dev/)
- Use Bun in favor of npm

```bash
react-app/                       # packages/web-tanstack-start
├── public/
├── src/
|   ├── api/                     # API client (openapi-fetch + generated schema)
│   │   ├── animals.ts
│   │   └── schema.ts
│   ├── routes/                  # File-based routes (replaces app.tsx)
│   │   ├── __root.tsx           # Root layout/shell
│   │   ├── index.tsx            # /
│   │   ├── animals.tsx          # /animals
│   │   ├── auth/
│   │   │   ├── login.tsx        # /auth/login
│   │   │   └── layout.tsx
│   │   ├── dashboard/           # /dashboard/*
│   │   └── user/
│   │       └── $id.tsx          # /user/$id
│   ├── features/                # Domain logic (hooks/services per domain)
│   │   ├── auth/
│   │   ├── dashboard/
│   │   └── user/
│   ├── components/
│   │   └── ui/                  # Shadcn primitives
│   ├── hooks/                   # Global hooks
│   ├── context/
│   ├── lib/
│   │   └── utils.ts             # cn() helper
│   ├── router.tsx               # createRouter setup
│   ├── routeTree.gen.ts         # auto-generated route tree
│   └── styles.css               # @import "tailwindcss";
├── vite.config.ts
├── package.json
└── tsconfig.json
```

**API Service**

- C# & .NET 10+
- ASP.NET Core Web API: Minimal APIs for endpoints
- Entity Framework Core for DB access
- PostgreSQL (running on Docker) or SQLite as database 
- Dependency Injection
- OpenAPI enabled for documented API endpoints

**Global**
- Monorepo using Bun workspaces for the web packages; .NET projects live next to them as standalone projects
- Root tooling via [vite-plus](https://www.npmjs.com/package/vite-plus) (`vp check` wraps OXLint + OXFmt)

```bash
root/                              # repo root
├── packages/
│   ├── api/                       # .NET backend (ASP.NET Core Web API)
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── api.csproj
│   ├── api.tests/                 # xUnit unit tests
│   ├── api.tests.integration/     # xUnit + Microsoft.AspNetCore.Mvc.Testing
│   ├── web-react/                 # React 19 SPA (Vite 8)
│   ├── web-ng/                    # Angular 21 SPA
│   └── web-tanstack-start/        # TanStack Start app
├── docs/
├── vite.config.ts                 # vite-plus config (OXLint + OXFmt wrapper)
├── package.json                   # Bun workspaces (web packages)
└── bun.lock
```