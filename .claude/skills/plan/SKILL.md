---
name: 'plan'
description: >
  Collaborative planning for software engineering tasks. Use this skill whenever the user wants to think
  through a feature, bug fix, refactor, or any code change before touching the keyboard. Trigger on: /plan,
  "let's plan", "help me plan", "before we implement", "what's the approach", "think through",
  "how should we", "I want to build/add/fix X" when the user seems to want to think before doing.
  Also use proactively when a task is complex enough that jumping straight to implementation would be risky.
---

# Plan

This skill produces a thorough, ready-to-implement plan by working collaboratively with the user. The goal is a plan complete enough that the implement step can execute it without needing to ask any further questions.

## Process

### 1. Clarify the Task

Begin by making sure you understand what the user wants. You don't need to ask all of these — use judgment based on what's already clear:

- What is the desired end state? What should be true after this is done?
- What is the current state? What exists now?
- Are there constraints? (specific libraries, patterns to follow, performance requirements, things to avoid)
- What counts as "done"?

If the user's request is already well-described, skip straight to research.

### 2. Research the Codebase

Before drafting anything, explore the codebase to anchor the plan in reality. A plan based on wrong assumptions wastes implementation time.

Do all of the following that are relevant:

- Find the files most likely to be touched (Glob for paths, Grep for symbols)
- Read enough of those files to understand current patterns and conventions
- Identify the integration points — where new code hooks into existing code
- Note any existing abstractions or utilities that should be reused
- Spot anything that could complicate the work (legacy patterns, tight coupling, missing types)

Share a brief summary of what you found before drafting. This surfaces wrong assumptions early.

### 3. Draft the Plan

Write a structured plan covering:

- **Goal** — one sentence: what we're building and why
- **Files** — which files to create, modify, or delete (use full paths)
- **Steps** — numbered, in implementation order. Each step must be specific and actionable. Not "update the auth module" but "add `validateTokenExpiry()` to `src/auth/middleware.ts` after line 43"
- **Tests** — what tests to write or modify, and what they should verify
- **Edge cases** — things that could go wrong or need special handling
- **Assumptions** — anything you've assumed that the user should confirm

### 4. Review with the User

Share the draft and invite feedback explicitly. Flag:

- Any decision you made that the user might want to override
- Any step that's still vague or where you're not sure of the approach
- Any open question that needs their input

Iterate until the user confirms the plan is complete and they're ready to implement.

### 5. Save the Plan

Ask the user where to save the plan, defaulting to `PLAN.md` in the project root.

Write the final `PLAN.md` using this structure:

```markdown
# Plan: <short title>

## Goal

<One sentence: what we're building and why.>

## Context

<Brief summary of what research revealed — relevant files, patterns, integration points.>

## Files

- `path/to/file.ts` — create / modify / delete: <what changes>
- ...

## Steps

1. <Specific, actionable step>
2. <Next step>
3. ...

## Tests

- <What to test and what it verifies>

## Edge Cases

- <Thing to watch out for>

## Assumptions

- <Anything assumed — confirm if unsure>
```

The plan is ready when:

- Every step is specific enough to execute without further clarification
- All file paths are identified
- No open decisions remain unresolved
- The user has confirmed they're happy with it
