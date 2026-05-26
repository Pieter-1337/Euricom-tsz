# Autonomous Workflow

Product Requirement Document implementation, end-to-end. Hand the orchestrator a PRD (or a single issue) and it spawns one agent per ready slice — auto-routed by work type, isolated in its own worktree + PR, respecting the `## Blocked by` dependency graph, in parallel where the DAG allows.

The single-issue chain (`/app-do-work <issue>`) is documented in [workflow-automatic.md](./workflow-automatic.md). **This doc covers the multi-issue / team layer that sits on top.**

## When to use this flow

- A PRD has already been broken into vertical slices on the tracker (via `/matt-to-issues`)
- You trust the implementer not to need pacing on each slice
- You're comfortable doing the human review _after_ each PR lands rather than mid-implementation
- You want to walk away while the chain unwinds

If any of those aren't true, use [workflow-manual.md](./workflow-manual.md) per slice.

## The chain

```
Prerequisite: PRD + child issues on the tracker, each with "## Blocked by"
              and a clear scope. (See workflow-manual.md → matt-to-issues.)

  /app-do-prd <PRD-ref>
        │
        ├─ fetch PRD + child issues
        ├─ build DAG from each child's "## Blocked by"
        ├─ schedule loop:
        │     for each issue whose blockers are all merged:
        │         decide agent type (auto-route by work type)
        │         decide isolation (worktree / current checkout)
        │         spawn agent → /app-do-work <issue> (linear chain)
        │     wait for PR-merged notifications
        │     re-evaluate ready set, repeat
        └─ stop when all child issues are merged, or a failure halts the chain

  human review (PR-by-PR, between batches)
```

The orchestrator never edits code. Per-issue work happens inside [the linear `/app-do-work` chain](./workflow-automatic.md), one invocation per issue.

## Surface

```
/app-do-prd <target> [--worktrees=true] [--parallel=true] [--reviewer=true]
                     [--agent=auto] [--on-failure=continue-siblings]
```

| Parameter | Type | Default | Meaning |
|---|---|---|---|
| `target` | PRD issue ref / URL | **required** | A PRD (the parent of several slice issues, each referencing it via `## Parent`) |
| `worktrees` | bool | `true` | Isolate each worker in its own git worktree + branch; open one PR per issue. Disable only when you want the orchestrator to run workers in the current checkout serially |
| `parallel` | bool | `true` | When the DAG allows it, launch ready issues concurrently |
| `reviewer` | bool | `true` | Each spawned worker (which runs `/app-do-work`) gets a `reviewer` sub-agent pass before commit. Cascades into the worker (see [Per-issue quality gate](#per-issue-quality-gate)) |
| `agent` | enum or `auto` | `auto` | Override per-issue agent selection (see [Agent auto-routing](#agent-auto-routing)). Cascades into each worker |
| `on-failure` | `continue-siblings` / `halt` | `continue-siblings` | What to do when a slice fails after autonomous recovery (see [Failure mode](#failure-mode)) |

For single-issue runs, use `/app-do-work <issue>` directly — that's documented in [workflow-automatic.md](./workflow-automatic.md). The orchestrator spawns one such `/app-do-work` invocation per ready slice.

## How it works internally

### 1. Resolve target → build the issue set

- If `target` is a PRD issue, fetch the PRD body + all child issues that reference it as `## Parent`.
- If `target` is a single issue, treat it as a one-node set.
- Read each child's `## Blocked by` section; parse issue refs (`#N` or full URLs) into a dependency graph.
- Sanity-check: the graph must be acyclic. If it isn't, abort and surface the cycle.

### 2. Schedule loop

A slice is not "done" when the PR opens — it's done when the PR merges. The orchestrator tracks each issue's state through that whole arc.

Per-issue states:

- **pending** — at least one blocker is still unmerged
- **in-flight** — agent is running, or PR is open but unmerged
- **failed** — agent halted with no recoverable path; needs human rescue
- **merged** — done

Loop:

```
while any issue is not yet merged:
    for each issue with all blockers merged AND no agent currently running:
        agent_type = pick_agent(issue)            # see Agent auto-routing
        isolation  = worktrees ? "worktree" : "current"
        spawn agent → /app-do-work <issue> [--reviewer=...]
        # agent commits, pushes branch, opens PR, returns PR URL

    wait for events:
        - PR's CI turns red on an open PR
              → re-spawn the agent for that issue in "iterate" mode
                (see Iterating on an open PR)
        - PR merged by human
              → recompute ready set (newly-unblocked issues become eligible)
        - agent halted with explicit failure
              → escalate to manual rescue (see Failure mode)
```

The orchestrator does not auto-merge. PRs go through human review like any other change — see [Stacking and merge cadence](#stacking-and-merge-cadence).

### 3. Each spawned agent runs the linear chain

For each ready issue, the orchestrator spawns one Agent with:
- `subagent_type` set to the chosen agent
- `isolation: "worktree"` when worktrees are enabled
- Prompt: roughly *"Run `/app-do-work <issue-ref>`. When the commit lands, push the branch and open a PR. Report the PR URL."*

That linear chain is unmodified — it explores, implements, validates, simplifies (then re-validates), optionally inserts a [reviewer pass](#per-issue-quality-gate), updates `CHANGELOG.md`, commits via the `commit` skill, and reports QA.

## Agent auto-routing

Per-issue agent selection scans the issue body's file paths and keywords. The rules pick a specialist — never a generic fallback. First match wins:

| Condition | Primary agent |
|---|---|
| Paths in `packages/api/**` AND none in `packages/web/**` | `backend-engineer` |
| Paths in `packages/web/**` AND none in `packages/api/**` | `frontend-engineer` |
| Paths only in `docs/**`, `CHANGELOG.md`, `CONTEXT.md`, or other `*.md` | `documenter` |
| Mixed paths (backend + frontend) | `backend-engineer` — typically the leading dependency (FE consumes BE); self-spawns `frontend-engineer` sub-agents when the work crosses into `packages/web/**` |

The routing applies **recursively at the sub-agent level**. When the primary agent hits work outside its specialty, it re-applies the same rules to pick a specialist sub-agent — never falls back to a generic one. The primary stays in charge of the issue; the sub-agent reports back as a tool result. See [Sub-agent self-recruitment](#sub-agent-self-recruitment) for examples.

You can force a specific primary via `--agent=...` to override the rules. If no rule matches (extremely rare — paths don't fit any of the patterns above), that's a signal the issue is under-specified; surface it for human re-scoping rather than guessing.

## Sub-agent self-recruitment

Every spawned agent has full access to the `Agent` tool. When a primary agent realises it needs cross-skill help mid-issue, it recruits a sub-agent in-band using the same [routing rules](#agent-auto-routing) — never a generic fallback:

- A `backend-engineer` slice that needs a quick FE schema regen + spec update → spawns a `frontend-engineer` sub-agent for that narrow step.
- A `frontend-engineer` slice that hits an unfamiliar backend constraint → spawns a `backend-engineer` sub-agent to investigate.
- Any agent at any time → spawns `Explore` for codebase queries to keep its own context light.

Sub-agents themselves can recruit further sub-sub-agents using the same rule, so the routing is genuinely recursive. The primary agent stays in charge of the issue end-to-end and integrates sub-agent reports.

When a primary genuinely cannot fit the issue's scope inside its single agent run, it should write a clear "needs follow-up" line in its return summary. The orchestrator then surfaces it for human decision — possibly filing a new issue, not retry-spawning blindly.

## Per-issue quality gate

When `--reviewer=true` (the default), each spawned agent inserts a step before the `commit` skill:

```
/app-do-work <issue>
  ├─ explore
  ├─ implement
  ├─ validate
  ├─ simplify
  ├─ validate                              ← original linear chain
  ├─ spawn `reviewer` sub-agent            ← reviewer pass
  │     reviewer reads the diff + tests, returns prioritised findings
  ├─ address findings (if any), re-validate
  └─ commit, push, open PR
```

Cost: roughly +30 % tokens for the reviewer to read the diff once. Catches issues before they hit a PR and saves a review round-trip with the human. Disable with `--reviewer=false` for trivially small changes.

This is orthogonal to the scope axis — single-issue runs benefit from it too.

## Iterating on an open PR

A slice's PR is the slice's body of work until it merges. The agent stays available to push more commits as long as the PR is open. Two paths back into the work:

### Automatic: CI flip on the PR

When the PR's CI turns red on its head SHA, the orchestrator re-spawns the same agent against the same issue in **iterate mode**:

```
agent (iterate mode):
  ├─ fetch PR diff, PR comments, CI logs for the failing run
  ├─ check out the PR's branch into the same (or a fresh) worktree
  ├─ diagnose: which check failed, what the logs say
  ├─ implement the fix
  ├─ run local validate (matching what CI runs)
  ├─ commit + push to the same branch
  └─ return: new HEAD + summary of what changed
```

CI then re-runs. If it goes green, the slice waits for human review/merge as usual. If it stays red after a small bounded number of iterations (default: 2), the orchestrator halts the slice and surfaces it via the [Manual rescue path](#manual-rescue-path) — endless thrash is worse than a clean human escalation.

### Manual: re-launch on review comments

Human review comments don't trigger an automatic re-spawn — they're conversational and the agent shouldn't speak for the reviewer's intent. When you want the agent to address comments, re-invoke `/app-do-work <issue>` explicitly. The skill notices the open PR and enters iterate mode (same as above), reads the comment threads and the PR diff, and pushes follow-up commits to the same branch.

Convention: leave the review comment thread open; the agent's follow-up commit addresses the points raised but does not resolve threads. Resolving stays the reviewer's call.

## Stacking and merge cadence

**Default: wait for human merge between dependent PRs.**

When the orchestrator launches issue #34 and #34's PR opens, the orchestrator pauses on any issue that has #34 in its `## Blocked by` set. After you review and merge #34, the orchestrator picks the freshly-unblocked issue (#35) and launches it from updated master.

Each PR is independently reviewable. Slower but safer; matches what the team's merge history already shows. This default applies whether `parallel=true` or not — issues with no remaining unmerged blockers run in parallel, issues blocked by an open PR wait.

**Alternative: stacked PRs.** Some teams branch each PR from the previous one rather than from master, so the chain can keep moving while reviews queue. Stacked PRs are powerful but the team's current review tooling doesn't make them friction-free. Stacking is documented here for completeness; it's not the default.

## Failure mode

Human intervention is the last resort, not the first. The orchestrator exhausts its own recovery options before halting.

A *failure* — distinct from CI red on an open PR, which [iterate mode](#iterating-on-an-open-pr) handles — is one of:

- The agent returned an explicit "I cannot complete this" summary (no PR opened, scope unclear, ran out of skill, etc.)
- The agent's local validate never went green after its in-slice retries (and never reached the push step)
- Iterate mode hit its retry bound (default 2) without bringing CI back to green
- The agent process died / context exhausted before reaching commit
- The PR was opened, CI is green, the agent reported done, but a human spots a *bad result* during review (the only case the orchestrator can't detect autonomously)

### Autonomous recovery sequence

For each detected failure (except the human-spotted bad-result case), the orchestrator runs this sequence:

1. **One diagnostic re-spawn.** Re-launch the same agent against the same issue with explicit context about the previous attempt's symptoms (last error / "stuck" summary / CI diagnosis). Often a second attempt with diagnostic context succeeds where the first didn't.
2. **If still failing, mark the slice failed and continue with independent siblings.** The DAG knows which open issues don't transitively depend on the failed one — those keep running. The failed slice's worktree + branch + last summary are preserved and reported at the end of the run, not surface-and-halt immediately.
3. **If every remaining open issue depends on a failed slice**, the orchestrator has nothing useful left to launch — that's when it halts and surfaces (see [Manual rescue path](#manual-rescue-path)).

This default prefers throughput. The chain keeps moving through localised failures rather than blocking the whole PRD on one stuck slice.

### `--on-failure` knob

| Value | Behaviour |
|---|---|
| `continue-siblings` (default) | The sequence above. Keep running independent siblings; halt only when fully blocked. |
| `halt` | Conservative. Stop on any failure, surface immediately. Use when you don't yet trust autopilot for the PRD's domain or when you want PR-by-PR review cadence. |

The bad-result case (agent reported done, PR is green, but the feature doesn't work) always escalates manually — the orchestrator has no signal that anything is wrong until a human looks. The [Per-issue quality gate](#per-issue-quality-gate) (reviewer pass) catches most of these pre-PR; the rest land at human review.

## Manual rescue path

When a slice fails — see [Failure mode](#failure-mode) for what counts as failure — the orchestrator halts and surfaces:

- the failing PR URL (if a PR was opened), and/or
- the worktree path + branch (the harness preserves it on failure), and/or
- the agent's last summary (containing whatever it managed to report before bailing)

A *bad result* (agent reported success, PR is green, but the feature doesn't work — what `/verify` would have caught) won't trigger an automatic halt; you'll find it during human PR review or QA. Treat it the same as a stall once spotted.

From there:

1. **Inspect the failing state** — `git status` / `git diff` inside the worktree, the PR's CI logs, and the agent's last summary together usually identify whether the agent ran out of skill, scope was wrong, or environment / CI drifted.
2. **Take over the slice manually** via the [workflow-manual.md](./workflow-manual.md) chain when the agent couldn't make it across the line but the issue's scope is fine.
3. **Regenerate the issue** via `/matt-to-issues` from the PRD when the issue file itself was the root cause (under-specified, wrong slicing), then re-launch the orchestrator.
4. **Re-launch the orchestrator on the remaining open child issues** when the failure is localised — come back to the failed slice separately rather than blocking the whole PRD on it.

## Relationship to the other workflows

There is one plan-side chain (always manual — humans hold the pen on architecture) and three implement-side modes that vary by autonomy:

```
PLAN SIDE                            IMPLEMENT SIDE  (pick one mode per slice)
──────────────────────────           ───────────────────────────────────────
matt-grill-with-docs                 ┌─ workflow-manual.md       step-by-step
  │                                  │                           human-paced
  ▼                                  │
matt-to-prd ──► PRD issue            ├─ workflow-automatic.md    one-shot
  │                                  │                           per issue
  ▼                                  │
matt-to-issues ──► slice issues ─►───┴─ workflow-autonomous.md   Product Requirement
                                                                 Document implementation
                       handover via the tracker issue
```

**Implement-side modes** in order of decreasing human pacing:

| Mode | Doc | Surface | When |
|---|---|---|---|
| Manual | [workflow-manual.md](./workflow-manual.md) | `/implement` → `/validate` → `/verify` → `/simplify` → `/commit` | Scope unclear, risk high, or you want to control pacing phase-by-phase |
| Automatic | [workflow-automatic.md](./workflow-automatic.md) | `/app-do-work <issue>` (one issue, linear chain) | Scope captured in an issue and you trust the implementer not to need pacing |
| Autonomous | [workflow-autonomous.md](./workflow-autonomous.md) | `/app-do-prd <PRD>` (orchestrator across slices, worktrees, parallel) | You want to walk away while a whole PRD unwinds |

Typical pattern: think with the plan-side chain, ship with whichever implement-side mode fits the slice's risk profile. Escalate _toward_ manual when a slice rejects autopilot or scope changes mid-flight; de-escalate _toward_ autonomous as the PRD's remaining slices become well-defined and low-risk.
