---
name: app-do-prd
description: Orchestrate a PRD across its slice issues end-to-end. Parses the dependency DAG from each child issue's `## Blocked by` field, schedules ready slices, spawns one worktree-isolated worker per slice (each running `/app-do-work`), handles iterate-on-CI-red, and continues independent siblings through localised failures. Use when the user wants to autonomously execute a whole PRD rather than one slice at a time.
argument-hint: '<PRD-ref> [--worktrees=true] [--parallel=true] [--reviewer=true] [--agent=auto] [--on-failure=continue-siblings] — `<PRD-ref>` is a tracker issue ref like `#33` or a full GitHub issue URL.'
disable-model-invocation: true
---

# Orchestrate PRD

Run a whole PRD autonomously: parse its child slice issues, build the dependency DAG, spawn one worker per ready slice (each running `/app-do-work`) in an isolated git worktree, react to CI / merge / failure events, and keep the chain flowing until every slice is merged or autonomously unrecoverable.

This skill is the orchestrator layer. Per-slice work happens inside `/app-do-work` (see `.claude/skills/app-do-work/SKILL.md`). Conceptual picture lives in `docs/agents/workflow-autonomous.md`.

## Workflow

### 1. Resolve the PRD

Fetch the parent issue via `gh issue view <PRD-ref> --repo <repo> --json title,body,labels,state` (repo lookup per `docs/agents/tracker.md`).

Abort if:
- The issue doesn't exist or isn't open.
- No other open issues reference it via `## Parent: #<N>` (without children, there's nothing to orchestrate).
- The labels don't include `enhancement` or whatever the project uses to mark implementable scope (warn, don't abort).

### 2. Fetch child issues and build the DAG

Use `gh issue list --repo <repo> --search 'in:body "#<PRD-number>"' --state open --json number,title,body,labels,state` to find candidates, then filter to those whose body contains `## Parent` referencing the PRD.

For each child, extract from the body:
- The `## Blocked by` section — parse issue refs (`#N` or full URLs) into a list.
- The `## What to build` section (passed verbatim to the worker; the orchestrator does not interpret it).
- Acceptance criteria headings (used for sanity-check only).

Build the dependency graph. Sanity-check for cycles; if any, halt and surface the cycle for human resolution.

### 3. Per-issue state and agent routing

Track each child issue's state through the run:

- **pending** — at least one blocker not yet merged
- **in-flight** — a worker is running, or its PR is open and unmerged
- **failed** — autonomous recovery exhausted, awaiting human rescue
- **merged** — done

For each issue, pre-compute the primary agent type using the rules below. First match wins. **Never falls back to `general-purpose`** — sub-spawning of specialists is in-band per the [workflow-autonomous doc](../../../docs/agents/workflow-autonomous.md#agent-auto-routing).

| Condition | Primary agent |
|---|---|
| Paths in `packages/api/**` AND none in `packages/web/**` | `backend-engineer` |
| Paths in `packages/web/**` AND none in `packages/api/**` | `frontend-engineer` |
| Only `docs/**`, `CHANGELOG.md`, `CONTEXT.md`, or other `*.md` paths | `documenter` |
| Mixed backend + frontend paths | `backend-engineer` (FE consumes BE; the worker self-spawns `frontend-engineer` sub-agents in-band when it crosses domains) |

`--agent=<name>` overrides the per-issue routing for the whole run.

If a child issue's body has no path signal that matches any rule, halt and report — that issue needs human re-scoping before it can be autonomously executed.

### 4. Scheduling loop

```
while any issue in {pending, in-flight}:

    # 4a. Launch newly-ready slices
    for issue in pending where all blockers are merged
                         AND no worker currently running for it:
        spawn Agent with:
            subagent_type      = agent_type for issue
            isolation          = "worktree" if --worktrees else current
            run_in_background  = true       if --parallel  else false
            prompt = "Run /app-do-work {issue-ref}
                      [--reviewer={inherited}]
                      [--agent={inherited if --agent was passed}].
                      When the commit lands, push the branch and open a PR.
                      Report the PR URL and worker summary."
        mark issue = in-flight
        record { issue → worker handle, branch (when known), PR URL (when known) }

    # 4b. React to events
    wait for the next of:

      • Worker reports PR opened
            → record PR URL; issue stays in-flight (PR is the slice's body of work until merged)

      • PR CI turns red on an open PR
            → re-spawn the same worker on the same branch in iterate mode (see §5)

      • PR merged by human
            → mark issue = merged
            → recompute the ready set; loop continues

      • Worker halts with explicit failure (no PR opened, or stuck mid-work, or CI red after iterate-mode retries)
            → invoke autonomous recovery (see §6)

      • All workers idle AND no pending issue is ready
            → loop ends (some issues may be in failed state — see final report)
```

### 5. Iterate mode on CI red

When a PR's head SHA shows failed checks, the orchestrator re-spawns the **same agent type** with the **same worktree / branch** and a prompt like:

> "PR {url} for issue #{n} has CI red on {head-sha}. Re-launch /app-do-work #{n} — the skill will detect the open PR and enter iterate mode automatically. Diagnose from the failing checks and PR diff, push a fix. Report the new HEAD."

Bound iterate-mode retries to **2 per slice per orchestrator run**. After that, mark the slice as failed and route to §6.

(The per-slice iterate-mode loop is inside `/app-do-work` itself — see that skill's step 1, which detects an open PR and switches to iterate mode.)

### 6. Autonomous failure recovery

When a worker halts with an unrecoverable failure (NOT CI red — that's §5):

1. **One diagnostic re-spawn.** Re-launch the same worker against the same issue, passing the previous attempt's last error / "stuck" summary explicitly in the prompt. Often a second attempt with diagnostic context succeeds where the first didn't.
2. **If still failing**, branch on `--on-failure`:
   - `continue-siblings` (default): mark the slice failed, preserve the worktree + branch + worker summary, log the failure, **continue launching independent siblings**. The DAG knows which open issues do NOT transitively depend on this slice — those keep flowing.
   - `halt`: stop all launches, surface immediately.
3. **Halt unconditionally** when every remaining `pending` issue transitively depends on a failed slice — there's nothing useful left to launch.

### 7. Final report

When the loop ends, emit a structured report:

- **Merged slices** — one bullet per slice with the PR URL.
- **Failed slices** — one bullet per slice with the worktree path, branch name, last worker summary, and PR URL (if one was opened). Each is ready for `workflow-manual.md` rescue.
- **Skipped slices** — slices that stayed `pending` because their blockers failed. Listed transitively so the human can see what unblocks if they manually resolve a failure.

## Parameters

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `<PRD-ref>` | issue ref or URL | **required** | The parent PRD issue |
| `--worktrees` | bool | `true` | Per-worker worktree isolation (one branch + one PR per slice) |
| `--parallel` | bool | `true` | Launch ready workers concurrently when the DAG allows |
| `--reviewer` | bool | `true` | Cascades to each worker; each `/app-do-work` invocation runs its reviewer sub-agent pass before commit |
| `--agent` | enum or `auto` | `auto` | Override the per-issue agent routing. Cascades to every worker |
| `--on-failure` | `continue-siblings` / `halt` | `continue-siblings` | Behaviour after autonomous recovery (§6) gives up |

## What this skill does not do

- It does not auto-merge PRs. Human review stays in the loop on every slice — see [Stacking and merge cadence](../../../docs/agents/workflow-autonomous.md#stacking-and-merge-cadence).
- It does not file new issues. It only runs against pre-existing tracker issues created by `/matt-to-prd` + `/matt-to-issues`.
- It does not edit `CONTEXT.md` / ADRs / `CHANGELOG.md` directly — those changes come only via individual workers acting on their assigned slices.
- It does not decompose a single issue across multiple parallel workers. If an issue is too big for one worker, that's a signal to re-slice via `/matt-to-issues`, not to bolt on intra-issue swarms.

## See also

- `docs/agents/workflow-autonomous.md` — conceptual picture, failure modes, escalation paths
- `.claude/skills/app-do-work/SKILL.md` — per-worker linear chain (what each spawned worker runs)
- `docs/agents/workflow-automatic.md` — when to use single-issue `/app-do-work` instead
- `docs/agents/workflow-manual.md` — fully-manual rescue path when autopilot can't recover
- `docs/agents/tracker.md` — issue tracker conventions and label vocabulary
