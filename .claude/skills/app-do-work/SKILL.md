---
name: app-do-work
description: Execute a unit of work end-to-end: plan, implement, validate with typecheck and tests, then commit. Use when user wants to do work, build a feature, fix a bug, or implement a phase from a plan.
argument-hint: '[issue-file] — path to an issue markdown file.'
disable-model-invocation: true
---

# Do Work

Execute a complete unit of work: plan it, build it, validate it, commit it.

## Workflow

### 1. Understand the task

If an issue file was passed as an argument, read it first — it is the source of truth for scope, acceptance criteria, and any references. Otherwise, abort the skill and ask the user to provide an issue file.

Then explore the codebase to understand the relevant files, patterns, and conventions. Delegate codebase exploration beyond ~3 greps to the built-in `Explore` agent to keep context light.

If the task is ambiguous, ask the user to clarify scope before proceeding.

### 2. Implement

Work through the plan step by step.

### 3. Validate

Run the feedback loops and fix any issues. Repeat until all pass cleanly.

```bash
bun run check --fix    # static analysis of Typescript code with linting, typechecking, and formatting
bun run test:web  # runs frontend unit tests
bun run test:api  # runs backend unit tests
bun run test:integration  # runs end-to-end tests
```

### 4. Simplify

Run `Skill('simplify')` to simplify the code.

Run the validation loops again and fix any issues. Repeat until all pass cleanly.

```bash
bun run check --fix    # static analysis of Typescript code with linting, typechecking, and formatting
bun run test:web  # runs frontend unit tests
bun run test:api  # runs backend unit tests
bun run test:integration  # runs end-to-end tests
```

### 5. Commit

Once static analysis and tests pass

- Update `CHANGELOG.md` under today's date with functional, user-facing bullet points. Each bullet answers "what can a user now do?" or "what behavior changed?" — not "what was built". No class/method names, no test counts, no migration names. Example:
  - ✓ "Admins can view all users and create new ones via an Add dialog"
  - ✓ "Creating a user automatically assigns a leave balance for each active leave type"
  - ✗ "Added UserService.CreateAsync with single SaveChangesAsync and 10 unit tests"
- commit the work. Run `Skill('commit')` to commit the work.

### 5. Report QA

Write a list of items the user should test to verify the work.
