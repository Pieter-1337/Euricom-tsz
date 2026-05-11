# Plan: Dark Mode Toggle

## Goal

Complete the dark mode feature so users can toggle between light and dark themes, with correct initial state, no flash of wrong theme, and solid test coverage.

## Context

Research revealed that the core infrastructure is already in place:

- **Tailwind v4** (no `tailwind.config.ts`). Config lives in `src/styles.css` via `@import 'tailwindcss'`. The dark variant is `@custom-variant dark (&:is(.dark *))` — toggling is done by adding/removing the `dark` class on `<html>`.
- `styles.css` already defines full `:root` (light) and `.dark` CSS custom property blocks for every shadcn/ui design token (background, foreground, primary, card, sidebar, chart, etc.).
- `packages/web/src/routes/__root.tsx` already injects an inline `<script>` (`themeInitScript`) before the body renders — it reads `localStorage.getItem('theme')` and `prefers-color-scheme` to apply `.dark` on `<html>` before first paint, preventing FOUC.
- `packages/web/src/components/theme-toggle.tsx` already exists: reads/writes `localStorage`, toggles the DOM class, uses `suppressHydrationWarning`, and defers icon rendering until after mount (via `useState<Theme | null>(null)` + `useEffect`).
- The toggle is already placed in `RootLayout` (right side of the nav bar).
- There is **no React context / `useTheme` hook** — theme state is DOM-only.
- Stack: TanStack Start, React 19, Tailwind v4, shadcn/ui (new-york), lucide-react, Vitest + Testing Library.

**What is missing / needs improvement:**

1. A `useTheme` hook so any component can reactively read the current theme in React state (currently nothing exposes this — `ThemeToggle` manages its own local state).
2. A `storage` event listener is absent — theme changes in another tab are not reflected.
3. No tests exist for `ThemeToggle` or the theme init script logic.
4. Minor UX: on the very first render cycle, `theme === null`, so the button renders `<Moon>` in light mode momentarily. This can be improved by reading the DOM class synchronously on mount.

## Files

- `packages/web/src/hooks/use-theme.ts` — **create**: a `useTheme()` hook that reads the current theme from the DOM and subscribes to changes (both same-tab toggles and cross-tab `storage` events).
- `packages/web/src/components/theme-toggle.tsx` — **modify**: replace local state with `useTheme()` hook; eliminate the flash by reading the class synchronously.
- `packages/web/src/hooks/use-theme.spec.ts` — **create**: Vitest unit tests for `useTheme()`.
- `packages/web/src/components/theme-toggle.spec.tsx` — **create**: Vitest + Testing Library tests for `ThemeToggle`.

## Steps

1. **Create `packages/web/src/hooks/use-theme.ts`.**
   - Export type `Theme = 'light' | 'dark'`.
   - Export `useTheme(): { theme: Theme; setTheme: (t: Theme) => void; toggle: () => void }`.
   - Initialize state with a lazy initializer: `() => (document.documentElement.classList.contains('dark') ? 'dark' : 'light')` — this reads the DOM synchronously on first render so there is no null/flash state.
   - In a `useEffect`, listen to `window` `storage` events filtered to `key === 'theme'` and update state accordingly; return a cleanup that removes the listener.
   - `setTheme(t)`: toggle the `dark` class on `document.documentElement`, write `localStorage.setItem('theme', t)`, call `setState(t)`.
   - `toggle()`: call `setTheme(theme === 'dark' ? 'light' : 'dark')`.

2. **Update `packages/web/src/components/theme-toggle.tsx`.**
   - Remove the local `useState<Theme | null>` and `useEffect` that was reading the DOM class after mount.
   - Import and call `useTheme()`.
   - Use `theme` from the hook directly (it is never `null`).
   - Remove the ternary guarding the icon — render `{theme === 'dark' ? <Sun /> : <Moon />}` directly.
   - Call `toggle()` from the hook in the `onClick` handler.
   - Keep `suppressHydrationWarning` on the `<Button>`.

3. **Create `packages/web/src/hooks/use-theme.spec.ts`.**
   - Mock `document.documentElement.classList` and `localStorage` using `jsdom` (already a dev dependency).
   - Test: default theme is `'light'` when no `dark` class is present.
   - Test: default theme is `'dark'` when `dark` class is present at init.
   - Test: `setTheme('dark')` adds `dark` class and writes to `localStorage`.
   - Test: `setTheme('light')` removes `dark` class and writes to `localStorage`.
   - Test: `toggle()` flips from light to dark and back.
   - Test: `storage` event with `key === 'theme'` and `newValue === 'dark'` causes the hook to update its state.

4. **Create `packages/web/src/components/theme-toggle.spec.tsx`.**
   - Render `<ThemeToggle />` with Testing Library.
   - Test: button renders with `aria-label="Toggle theme"`.
   - Test: in light mode the Moon icon is shown; in dark mode the Sun icon is shown.
   - Test: clicking the button calls `toggle()` (spy on `useTheme` or verify DOM class changes).

5. **Verify the `themeInitScript` in `__root.tsx` is covered.**
   - No changes needed to the script — it is already correct. Add a comment in a test file or inline that this is tested via E2E / manual verification (out of scope for unit tests since it runs before JS hydration).

## Tests

- `packages/web/src/hooks/use-theme.spec.ts` — verifies: initial state reads from DOM class; `setTheme` mutates DOM + localStorage; `toggle` flips state; cross-tab `storage` events sync state.
- `packages/web/src/components/theme-toggle.spec.tsx` — verifies: correct icon rendered per theme; button is accessible; click triggers toggle.

Run tests with:

```
bun --filter web-tanstack-start test
```

## Edge Cases

- **SSR / hydration mismatch**: The `themeInitScript` runs before React hydrates, so the DOM class is already correct. The hook reads the DOM class as its lazy initializer — this is safe in a browser context. With TanStack Start's SSR, the server renders without a `dark` class; the inline script corrects it client-side before paint. `suppressHydrationWarning` on the button suppresses the expected icon mismatch warning.
- **`localStorage` unavailable** (private browsing / blocked storage): The `themeInitScript` already wraps in `try/catch`. The hook's `setTheme` should also guard the `localStorage.setItem` call in a try/catch to avoid throwing.
- **`prefers-color-scheme` changes at runtime**: The current implementation does not listen to `matchMedia` changes. This is an acceptable limitation for v1; the user's explicit toggle always wins once they interact.
- **`storage` event only fires in other tabs**: Within the same tab, state is updated directly via `setTheme`; the `storage` listener is only for cross-tab sync.
- **React 19 strict mode double-effect**: The `useEffect` with event listeners will fire twice in development. Because the cleanup removes the listener each time, there is no double-registration issue.

## Assumptions

- Dark mode is controlled exclusively by the `dark` class on `<html>` (confirmed by `@custom-variant dark (&:is(.dark *))` in `styles.css`).
- Persistence is via `localStorage` key `'theme'` (confirmed by existing code).
- No `next-themes` or other third-party theme library will be introduced — the pattern stays DOM-class-based with a thin custom hook.
- The `packages/web/src/hooks/` directory does not yet exist and needs to be created.
- Tests run in jsdom (Vitest default for this project based on the existing `sample.spec.ts` and `jsdom` dev dependency).
- No changes to `styles.css`, `__root.tsx`, or the Tailwind configuration are required — they are already complete.
