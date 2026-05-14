# TypeScript Guidelines

## API Error Handling

- **`ApiRequestError`** (`packages/web/src/api/client.ts`): Carries `status` and `problem: ProblemDetails | null`. Use getters:
  - `isExpected` — true for 4xx, false for 5xx
  - `userMessage` — detail → title → generic fallback
  - `fieldErrors` — per-property map of `{ code, message }[]` or null
- **Parsing**: The fetch wrapper in `packages/web/src/lib/api.server.ts` auto-parses `application/problem+json` bodies into `ProblemDetails`.

## Form Error Handling

- **TanStack Form with server errors**: Use `errorMap.onServer` to set field-level errors from the API (not `errors`, which is read-only in v1.x).
- **Submit button logic**: Use `hasClientSideError(fieldMeta)` from `packages/web/src/lib/form-utils.ts` — returns true only if non-`onServer` errors exist, so the button stays enabled when only server errors are present.

## General

- Use strict TypeScript
- Avoid using `any`
- Prefer `unknown` for untrusted input, then narrow with guards or schemas
- After TypeScript changes, run `bun check` and fix all errors