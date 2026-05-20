# Automatic Workflow

One-shot autopilot from an issue file to a committed change. Use when planning is done, scope is captured in an issue, and you want the implementation phase to run without per-step approval.

## When to use this flow

- The plan side is finished (PRD + issues exist on the tracker, ADRs / CONTEXT.md committed)
- Scope is small enough for one session
- You trust the implementer not to need pacing
- You're comfortable doing the human review _after_ the commit lands rather than mid-implementation

## The chain

```
Prerequisite: plan-side chain has produced an issue file or tracker issue
              (see workflow-manual.md)

  /app-do-work <issue-file>
        │
        ├─ explore codebase
        ├─ implement
        ├─ validate (typecheck + tests, retry on error)
        ├─ simplify (refactor, then re-validate)
        ├─ commit (changelog + git commit)
        └─ produce QA list for human

  human review (post-commit)
```

## How `/app-do-work` works internally

Defined in `.claude/skills/app-do-work/SKILL.md`. Takes an issue file as its argument; aborts if not given one.

1. **Understand the task** — read the issue file, explore the codebase (delegates broad exploration to the built-in `Explore` agent to keep context light).
2. **Implement** — works through the plan step by step.
3. **Validate** — runs the feedback loops, repeats until clean:
   ```
   bun run check --fix
   bun run test:web
   bun run test:api
   bun run test:integration
   ```
4. **Simplify** — invokes `Skill('simplify')`, then re-runs the feedback loops.
5. **Commit** — updates `CHANGELOG.md` (user-facing bullets only, no class/method names), then commits via the `commit` skill.
6. **Report QA** — writes a list of items the user should manually verify.

## Per-edit feedback loop (Agent Hook)

A `PostToolUse` hook wired in `.claude/settings.json` runs single-file checks on every `Edit` or `Write`, before the slower full-suite validate at the end.

**Routing:**

| File pattern         | Tool                        | What runs                                                |
| -------------------- | --------------------------- | -------------------------------------------------------- |
| `**/packages/web/**` | `bun vp check --fix <file>` | TypeScript typecheck + lint + format on the changed file |

On failure the hook prints the tool's output and exits with code 2, which tells the model "your last edit broke something — fix it." Successful runs are silent.

## Manual rescue path

If autopilot stalls or produces a bad result:

1. Don't fight the autopilot — stop it and inspect what was changed (`git status`, `git diff`).
2. Switch to the manual chain (see `workflow-manual.md`) from whichever phase failed.
3. If the issue file was the root cause (underspecified, wrong slicing), regenerate via `/matt-to-issues` from the PRD.

## Combining with the manual chain

The two flows aren't mutually exclusive — they meet at the issue file:

```
Plan side (manual)  →  issue file on tracker  →  Implement side (auto)
                          ↑                          ↓
                          └──── feedback / re-slice ─┘
```

Typical pattern: think with the manual chain, ship with autopilot, return to the manual chain when scope changes.
