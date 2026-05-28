# Design system — Timesheet Zone

> Anchor document for visual + interaction decisions. **Read this before
> styling new screens, components, or pages.** Keep it short and accurate
> — when you ship a new piece of chrome or a new pattern, add it here.

---

## What we use

**Brand: Euricom — Tech Tribes.** Dark canvas, neon green accent, geometric type.
Official brand guidelines: `_research/Euricom-Guidelines.pdf` _(if mirrored
in repo)_ — otherwise hosted internally by the Euricom design team.

**Tech**: Tailwind CSS v4 (`@import 'tailwindcss'` + `@theme inline`),
shadcn-style primitives under `src/components/ui/`, `lucide-react` icons,
TanStack Start + Router. Theme is dark-by-toggle via `.dark` class on
`<html>` (see `theme-toggle.tsx`).

**Type**: **Montserrat** is the only typeface. Loaded from Google Fonts.
Body uses regular (400), nav uses medium (500), labels/CTAs use semi (600),
headings use bold (700). Italic-light Montserrat in fluorescent green is
the brand's display accent — reserve it for hero moments, not chrome.

---

## Color tokens (from `styles.css`)

### Brand-fixed (same in both themes)

| Token                 | Value     | Use                                                                  |
| --------------------- | --------- | -------------------------------------------------------------------- |
| `--euri-green`        | `#00FF00` | Single accent — active states, focus, brandmark. **One per screen.** |
| `--euri-charcoal`     | `#1D252D` | Default dark canvas + header bg in both themes                       |
| `--euri-sidebar-dark` | `#171E25` | Sidebar surface on dark theme                                        |
| `--euri-steel-light`  | `#F1F5F6` | Sidebar / muted surface on light theme                               |
| `--euri-white`        | `#FFFFFF` | Primary text on dark; canvas on light                                |

### Color rules

- **One green per screen.** The neon is a spotlight. If two things want it, demote one.
- **Header stays charcoal in both themes.** It's the brand anchor.
- **No gradients on green.** Flat fluorescent only. The only "glow" is a focus halo.
- **Don't mix green with the steel-blue accents in the same component.**

---

## Spacing & radii

- **4px base grid.** All spacing is a multiple of 4.
- **Radii**: buttons `8px`, cards `12px`, pills `999px`.
- Active nav rail (the inset green line): `2px`.

Tailwind's default scale already matches — use `gap-3` (12px), `p-5` (20px),
`rounded-lg` (8px). Custom values via arbitrary brackets when needed
(e.g. `rounded-[10px]`, `py-[9px]`).

---

## Type rhythm

- **Body**: 13–16px regular (400), `--euri-white` on dark / `--euri-charcoal` on light.
- **Nav rows**: 13.5px medium (500).
- **Eyebrow labels** (section headers in chrome, "TECH TRIBES" labels): 10.5–13px medium, **uppercase**, tracking `0.32em`.
- **Headings**: bold (700), tight tracking (`-0.005em` to `-0.02em`).

Sentence case for everything except eyebrow labels and the brand wordmark
(which is lowercase: "euricom"). **No emoji. No exclamation marks.**

---

## Motion

| Token             | Value                         | Use                          |
| ----------------- | ----------------------------- | ---------------------------- |
| `--euri-dur-fast` | `120ms`                       | Color/background transitions |
| `--euri-dur-base` | `220ms`                       | Width / size transitions     |
| `--euri-ease-out` | `cubic-bezier(.22,.61,.36,1)` | Default easing               |

**No bounces. No spring overshoot. Always respect `prefers-reduced-motion`.**

---

## Icons

Use **`lucide-react`**. Stroke width **`1.75`**, default size **16px**
(20–24px in chrome accents). Stroke `currentColor`; green only when the
icon represents the active/primary state of the screen.

**No emoji. No unicode dingbats** (`→`, `★`). If you need an arrow, use
`<ArrowRight />` or the word.

---

## Components / surfaces shipped

| Where                         | File                                                                                | Notes                                                                                                                                                             |
| ----------------------------- | ----------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| App chrome (header + sidebar) | `packages/web/src/routes/_protected.tsx`                                            | Charcoal header, collapsible eyebrow-sectioned sidebar with `localStorage` persistence (`tsz.sidebar.collapsed`).                                                 |
| Impersonation banner          | `packages/web/src/routes/_protected.tsx`                                            | Full-width `bg-destructive` strip between header and content row; "You are impersonating **{name}**" + Stop button. Only visible while impersonating.             |
| Impersonate target picker     | `packages/web/src/features/users/components/impersonate-dialog.tsx`                 | shadcn `Dialog` + `Command` searchable picker listing non-Admin users from `/api/users/impersonation-targets`; selecting a user starts impersonation and reloads. |
| Theme toggle                  | `packages/web/src/components/theme-toggle.tsx`                                      | Toggles `.dark` on `<html>`, writes `theme` to `localStorage`. Reused inside the dark header.                                                                     |
| Brand assets                  | `packages/web/public/`                                                              | `brandmark.svg`, `grid-pattern.svg`. Brandmark at 22px in the header; grid as low-opacity background on the workspace.                                            |
| Admin read-only week header   | `packages/web/src/routes/_protected/_authenticated/time-entry/week/$year/$week.tsx` | Bold name + muted "Week N, YYYY — read only" subline rendered above the grid when `?userId` resolves to another user. Only shown in read-only mode.               |
| My Tasks (approval inbox)     | `packages/web/src/routes/_protected/_authenticated/my-tasks.tsx`                    | Admin-only page listing all `Submitted` weeks pending approval. shadcn `Table` with one row per week; `Inbox` lucide icon for the empty state; `ListChecks` NavLink icon in the sidebar. |

**Add a row to this table whenever you style a new piece of chrome or a
reusable component.**

---

## Patterns

### Active-state recipe (nav links, tabs, segmented controls)

- **Background**: `bg-[rgba(0,255,0,0.10)]` (light) / `bg-[rgba(0,255,0,0.08)]` (dark)
- **Foreground**: full-strength text colour
- **Marker**: 2px inset green rail — `shadow-[inset_2px_0_0_#00FF00]` for full-width items, or an absolutely-positioned `<span>` for icon-only rails
- **Icon stroke**: `#00FF00`

### Focus rings

- `outline: 2px solid #00FF00; outline-offset: 2px;`
- Use Tailwind: `focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#00FF00]`
- Skip `ring-*` utilities — they clash with the brand's flat aesthetic.

### Hover states

- **Ghost button**: bg fades to `rgba(0,255,0,0.08)` (dark) / `rgba(0,0,0,0.04)` (light)
- **Filled green button**: `opacity: 0.9` (no colour shift)
- **Link**: underline appears (was invisible), `text-underline-offset: 4px`
- **Press**: `translateY(1px)` (no colour change)

### Eyebrow labels

```tsx
<div className="px-3 text-[10.5px] font-medium uppercase tracking-[0.32em] text-[#6B7682] dark:text-white/40">
  Section name
</div>
```

### Plus-grid background atmosphere

On full-bleed surfaces (workspace, hero):

```tsx
<div
  className="absolute inset-0 pointer-events-none
                [background-image:url('/grid-pattern.svg')]
                [background-size:160px_160px]
                opacity-[0.18] [filter:invert(1)]
                dark:opacity-[0.55] dark:[filter:none]"
/>
```

---

## What NOT to do

- Don't introduce a third typeface. Don't use Inter, Roboto, system-ui,
  or a monospace.
- Don't add backdrop blur / frosted glass. The brand is flat and precise.
- Don't use shadows on cards by default — depth comes from a slightly
  raised surface colour (`--euri-charcoal-raised: #232C35`).
- Don't add icons just because there's space. Euricom resists icon-noise.
- Don't combine the green accent with the steel-blue accents in one
  component.
- Don't reach for `→`, `✓`, `★`, or emoji. Use a Lucide icon or a word.

---

## Where to explore further

- **Full brand book** (token names, type scale, motion specs):
  see Euricom design system files — the conversation that produced this
  layout used `/projects/019e30e6-5846-735c-923b-f2afaf21e2b4/` as the
  source of truth, mirroring `colors_and_type.css`, `assets/`, and
  `ui_kits/`. Ask Pieter for a current pointer if that's gone stale.
- **Original handoff bundle for this chrome**:
  `design_handoff_euricom_layout/` _(if not yet merged & deleted)_ — has
  pixel-level specs and 4 reference screenshots.

---

## Updating this doc

When you ship a new component or change a pattern:

1. Add or update the row in the **Components / surfaces shipped** table.
2. If you introduced a new pattern (active state, focus, hover, etc.)
   that future work should follow, add it under **Patterns**.
3. If you found a constraint worth codifying, add it to **What NOT to do**.

Keep entries terse. This is a map, not a textbook.
