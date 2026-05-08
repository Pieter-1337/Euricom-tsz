# Dark Mode Implementation Plan

## Context

This plan is for adding a user-toggleable dark/light theme to the TanStack Start React app in `packages/web`. The app already uses Tailwind CSS v4, shadcn/ui (new-york style, zinc base colour, CSS variables), and lucide-react icons.

### What already exists

A significant amount of groundwork is already in place:

- **`src/styles.css`** — already defines a full `.dark` class block with complete CSS variable overrides (background, foreground, card, popover, primary, secondary, muted, accent, destructive, border, input, ring, chart colours, sidebar colours). The Tailwind custom variant `@custom-variant dark (&:is(.dark *))` is also already configured.
- **`src/components/theme-toggle.tsx`** — a `ThemeToggle` component that reads/writes `localStorage` and toggles the `dark` class on `document.documentElement`.
- **`src/routes/__root.tsx`** — inlines a `themeInitScript` that runs before first paint to apply the stored theme (or system preference), preventing flash of wrong theme. The `ThemeToggle` is already rendered in the nav bar.

In other words, **the core infrastructure is fully implemented**. What remains is polish, robustness, and potential UX improvements.

---

## Gap Analysis

| Area | Status | Notes |
|------|--------|-------|
| CSS variables for dark theme | Done | All tokens in `styles.css` |
| Tailwind dark variant | Done | `@custom-variant dark` in CSS |
| No-FOUC script | Done | Inline script in `RootDocument` |
| Toggle button (UI) | Done | `ThemeToggle` in nav |
| localStorage persistence | Done | In `ThemeToggle.toggle()` |
| System preference detection | Done | In `themeInitScript` |
| Shared theme state / hook | Missing | State lives only in `ThemeToggle` component |
| `system` (auto) mode support | Missing | Only `light`/`dark` supported |
| Accessible icon label | Present | `aria-label="Toggle theme"` exists |
| Hydration safety | Partial | `suppressHydrationWarning` on button, but icon flickers on hydration because initial `theme` state is `null` |

---

## Recommended Changes

### 1. Extract a `useTheme` hook — `src/hooks/use-theme.ts`

Currently theme state (`useState`, `localStorage` read/write, class manipulation) lives entirely inside `ThemeToggle`. Extracting it to a hook lets any other component (e.g. a settings page, a sidebar) read or set the current theme without prop-drilling.

```ts
// src/hooks/use-theme.ts
import { useEffect, useState } from 'react';

export type Theme = 'light' | 'dark';

export function useTheme() {
  const [theme, setThemeState] = useState<Theme>(() => {
    // Runs only on client; SSR fallback handled by inline script
    if (typeof document !== 'undefined') {
      return document.documentElement.classList.contains('dark') ? 'dark' : 'light';
    }
    return 'light';
  });

  function setTheme(next: Theme) {
    document.documentElement.classList.toggle('dark', next === 'dark');
    localStorage.setItem('theme', next);
    setThemeState(next);
  }

  return { theme, setTheme };
}
```

- Initialise from the DOM class (which the inline script has already set) rather than deferring to a `useEffect`, so the icon renders correctly on first paint without a flicker.
- Remove `suppressHydrationWarning` from the button once the hook initialises synchronously from the DOM.

### 2. Simplify `ThemeToggle` to use the hook

```tsx
// src/components/theme-toggle.tsx
import { Moon, Sun } from 'lucide-react';
import { Button } from '#/components/ui/button';
import { useTheme } from '#/hooks/use-theme';

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  return (
    <Button
      variant="ghost"
      size="icon-sm"
      onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')}
      aria-label="Toggle theme"
    >
      {theme === 'dark' ? <Sun /> : <Moon />}
    </Button>
  );
}
```

### 3. Fix the hardcoded `text-gray-600` in `src/routes/index.tsx`

The home page uses `text-gray-600` which does not respond to dark mode. Replace with the semantic token:

```tsx
// Before
<p className="mt-2 text-gray-600">A simple TanStack Start app.</p>

// After
<p className="mt-2 text-muted-foreground">A simple TanStack Start app.</p>
```

Audit the rest of the codebase for similar raw colour classes (`text-gray-*`, `bg-white`, `text-black`, etc.) that bypass CSS variable theming.

### 4. (Optional) Add `system` as a third theme option

If a three-way toggle (light / dark / system) is desired later, the `useTheme` hook can be extended to store `'system'` in `localStorage` while deriving the applied class from `window.matchMedia`. This is not required for the initial implementation.

---

## File Changelist

| File | Action | Reason |
|------|--------|--------|
| `src/hooks/use-theme.ts` | **Create** | Shared, reusable theme hook |
| `src/components/theme-toggle.tsx` | **Edit** | Use `useTheme`, remove `useState`/`useEffect` duplication |
| `src/routes/index.tsx` | **Edit** | Replace `text-gray-600` with `text-muted-foreground` |

No changes are needed to `styles.css`, `__root.tsx`, or any shadcn UI components — they are already correctly set up.

---

## Implementation Order

1. Create `src/hooks/use-theme.ts`.
2. Update `src/components/theme-toggle.tsx` to consume the hook.
3. Audit routes for raw colour classes; fix `src/routes/index.tsx` at minimum.
4. Manually verify toggle works, persists across page reloads, and respects OS preference on first visit.

---

## Testing Checklist

- Toggle switches icon and applies/removes `.dark` on `<html>`.
- Preference is saved to `localStorage` and restored on reload.
- First-paint uses the correct theme with no flash (test in a fresh private window with OS in dark mode, and with OS in light mode).
- All pages look correct in both themes (no invisible text, no harsh white boxes in dark mode).
- `ThemeToggle` renders the correct icon immediately — no flicker from `null` → resolved state.
