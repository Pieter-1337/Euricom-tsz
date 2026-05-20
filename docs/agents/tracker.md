# Issue Tracker Configuration

This file tells the `matt-*` skills (`matt-to-prd`, `matt-to-issues`, `matt-triage`) which tracker to use and which labels to apply. The skills' own SKILL.md docs say "run `/setup-matt-pocock-skills` if not provided" — this file replaces that step.

## Tracker

- **Type:** GitHub Issues
- **Repo:** `Pieter-1337/Euricom-tsz`
- **CLI:** use `gh` (already authenticated for this user)

Common commands:

```
gh issue create  --repo Pieter-1337/Euricom-tsz --title "..." --body "..." --label "..."
gh issue list    --repo Pieter-1337/Euricom-tsz --label "needs-triage"
gh issue view    <number> --repo Pieter-1337/Euricom-tsz --comments
gh issue edit    <number> --repo Pieter-1337/Euricom-tsz --add-label "ready-for-agent" --remove-label "needs-triage"
gh issue comment <number> --repo Pieter-1337/Euricom-tsz --body "..."
```

## Label vocabulary

Canonical role name → GitHub label string. The canonical and actual names match 1:1 — no translation needed.

| Canonical role     | GitHub label      | Kind     |
| ------------------ | ----------------- | -------- |
| `bug`              | `bug`             | category |
| `enhancement`      | `enhancement`     | category |
| `needs-triage`     | `needs-triage`    | state    |
| `needs-info`       | `needs-info`      | state    |
| `ready-for-agent`  | `ready-for-agent` | state    |
| `ready-for-human`  | `ready-for-human` | state    |
| `wontfix`          | `wontfix`         | state    |

Every triaged issue should carry exactly one category label and one state label.

## Notes

- The PRD published by `/matt-to-prd` is a **draft** for review on the tracker; humans sharpen it via comments/edits on the issue before `/matt-to-issues` splits it. See `workflow-manual.md`.
- Comments and issues posted during triage must start with the AI disclaimer line specified in the `matt-triage` SKILL.
