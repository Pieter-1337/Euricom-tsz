# Login

- Login with Azure Entra ID
- Login page with "Microsoft" login button
- Single-tenant setup (Euricom)
- One app registration shared by both frontend and API
- User roles: Admin, User, Client Manager
- User impersonation — Admins can "act as" another user (read-write, replace-not-union). Scoped in; see `requirements/login/impersonation/plan.md` and ADR-0004.
