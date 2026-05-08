---
name: 'commit'
description: 'How to commit changes in this repo  triggers: commit, push'
---

# Describe how we commit changes in this repo follow the steps below sequentially.

## Status

! `git status`

## Diff

! `git diff`

## Commit message formatting

Use commit conventions

Primary Commit Types

feat: A new feature for the user. This type typically triggers a minor version bump in semantic versioning.

fix: A bug fix for the user. This type typically triggers a patch version bump.

Recommended Additional Types
docs: Documentation only changes (e.g., README, comments).

style: Changes that do not affect the meaning of the code, such as white-space, formatting, or missing semi-colons.

refactor: A code change that neither fixes a bug nor adds a feature (improves structure/readability).

perf: A code change that improves performance.

test: Adding missing tests or correcting existing tests.

build: Changes that affect the build system or external dependencies (e.g., npm, webpack).

ci: Changes to continuous integration configuration files and scripts (e.g., GitHub Actions).

chore: Other changes that don't modify src or test files (e.g., updating dependencies, configs).

revert: Reverts a previous commit.
