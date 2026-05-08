---
name: validate
description: >
  Post-implementation validation step in the plan → implement → validate workflow. Use this skill
  whenever the user wants to verify their implementation is complete and correct — after coding,
  before committing, or when asking "did I miss anything?", "is this done?", "check my work",
  "review what I built", or "validate". Also trigger on /validate. Use proactively after the
  implement skill finishes if the user seems ready to commit or ship.
---

# Validate

This skill verifies that an implementation is correct, complete, and ready to commit. It runs
automated checks, cross-references against the original plan, and surfaces gaps before they become
bugs.

## Process

### 1. Load Context

Start by understanding what was supposed to be built and what actually changed.

- Read `PLAN.md` if it exists — this is the source of truth for intent
- Run `git diff HEAD` (or `git diff main`) to see what was actually changed
- If there's no PLAN.md, ask the user to briefly describe what they implemented before proceeding

Summarize in one sentence: "You added X to Y and Z." Confirm with the user if it looks right.

### 2. Run Automated Checks

Run all checks that apply to this project. For each one, report pass/fail with the actual output
if it fails — not just "it failed".

**Always run:**
```bash
bun run typecheck   # or tsc --noEmit if no typecheck script
bun run lint        # if a lint script exists
bun test            # or bun run test
```

**Discover available scripts first:**
```bash
cat package.json | grep -A 20 '"scripts"'
```

Run checks in parallel where they're independent. Don't stop at the first failure — collect all
failures before reporting.

If a script doesn't exist, skip it and note it's not configured. Don't treat a missing script as
a failure.

### 3. Cross-Reference Plan vs Implementation

If a PLAN.md exists, go through each **Step** item and assess whether it was addressed in the diff.

For each step:
- **Done** — the diff clearly addresses it
- **Partial** — something was done but it looks incomplete
- **Missing** — no evidence in the diff that this step was tackled
- **N/A** — the step turned out not to be needed (explain why)

Be pragmatic — don't flag "missing" just because the exact function name in the plan differs from
what was written. Assess intent, not literal text matching.

Also check the **Tests** section of the plan. If tests were planned and no test files were touched,
flag it.

### 4. Surface Quality Issues

Look at the actual changed code and flag anything that would reasonably cause problems:

- Unhandled promise rejections or missing error handling **at system boundaries** (API calls,
  user input, file I/O) — not internal helpers where the caller is responsible
- TypeScript `any` casts that look like they're hiding a real type problem
- Console.log / debug statements left in
- Obvious missing edge cases the plan called out that aren't handled
- Dead code introduced (imports, variables, functions that are never used)

Don't nitpick style or invent hypothetical edge cases. Focus on things that will actually matter.

### 5. Produce the Validation Report

Write a concise report directly in the conversation (no separate file unless the user asks).

Use this structure:

```
## Validation Report

### Automated Checks
- ✓ TypeScript: no errors
- ✗ Tests: 2 failing — [paste relevant failure output]
- ✓ Lint: clean
- — Build: no build script configured

### Plan Coverage  (omit if no PLAN.md)
- ✓ Step 1: Added validateTokenExpiry() to src/auth/middleware.ts
- ~ Step 2: Error handling added but only for 401, plan mentioned 403 too
- ✗ Step 3: Tests for the new middleware — no test files modified

### Issues Found
- `src/api/users.ts:47` — fetch() call has no error handling
- `src/auth/middleware.ts:12` — console.log left in

### Verdict: NEEDS WORK

**Before committing:**
1. Fix the 2 failing tests (see output above)
2. Add error handling to the fetch() at users.ts:47
3. Remove the console.log at middleware.ts:12
```

**Verdict rules:**
- **PASS** — all automated checks pass, no missing plan steps, no significant issues
- **NEEDS WORK** — automated checks pass but there are issues or gaps worth fixing first
- **FAILING** — one or more automated checks failed (type errors, test failures, lint errors)

### 6. Offer to Fix

After the report, ask the user how they'd like to proceed:
- If verdict is FAILING or NEEDS WORK, offer to fix the issues directly
- If verdict is PASS, offer to proceed to commit (using the commit skill if available)

Don't immediately start fixing without asking — the user might have context that changes the
priority or might prefer to fix things themselves.
