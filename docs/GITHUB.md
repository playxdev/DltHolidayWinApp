# Git Workflow & Rules

## Repository

| Remote | URL |
|--------|-----|
| origin | https://github.com/playxdev/DltHolidayWinApp |

Default branch: `main`

---

## Team Structure

| Role | Count | Responsibilities |
|------|-------|-----------------|
| Project Manager (PM) | 1 | Review & merge PRs, create releases, deploy production, maintain repo quality |
| Developer | 3 | Implement features/fixes, open PRs, resolve merge conflicts |

**Rule:** Only the PM may merge into `main`. No developer may merge their own PR.

---

## Branch Strategy

```
              ┌──────────┐
              │   main   │  Production (always stable, always deployable)
              └────┬─────┘
                   ▲
         Release PR│
                   │
              ┌────┴─────┐
              │  develop  │  Integration (latest working code)
              └────┬─────┘
                   ▲
      ┌────────────┼────────────┐
      │            │            │
┌─────┴──────┐ ┌───┴───────┐ ┌─┴──────────┐
│ feature/*  │ │ bugfix/*  │ │  hotfix/*   │
└────────────┘ └───────────┘ └──┬────┬─────┘
                                │    │
                          PR to main  │
                                      │
                              merge back to develop
```

| Branch | Purpose | Base from | PR target |
|--------|---------|-----------|-----------|
| `main` | Production | — | — |
| `develop` | Integration | `main` | — |
| `feature/*` | New feature | `develop` | `develop` |
| `bugfix/*` | Dev bug fix | `develop` | `develop` |
| `hotfix/*` | Critical fix | `main` | `main` → then back to `develop` |

### Branch Naming

Lowercase letters, numbers, hyphens only.

| Type | Pattern | Example |
|------|---------|---------|
| feature | `feature/<kebab-case>` | `feature/monthly-log-rotation` |
| bugfix | `bugfix/<kebab-case>` | `bugfix/curl-timeout-handling` |
| hotfix | `hotfix/<kebab-case>` | `hotfix/tls12-fallback` |

---

## Development Workflow

### Daily Start

```bash
git checkout develop
git pull origin develop
git checkout -b feature/my-feature
```

### Before Opening a PR

```bash
git fetch origin
git rebase origin/develop        # or origin/main for hotfix
git push origin feature/my-feature --force-with-lease
```

Resolve all conflicts locally. Never ask the PM to resolve them.

### Conflict Resolution

1. Rebase onto target branch: `git rebase origin/develop`
2. Resolve conflicts, then `git rebase --continue`
3. Push: `git push --force-with-lease`

---

## Commit Convention

```
<type>: <short description>
```

| Type | When |
|------|------|
| `feat` | New feature or enhancement |
| `fix` | Bug fix |
| `docs` | Documentation only |
| `chore` | Build, config, dependencies, tooling |
| `refactor` | Code restructuring (no behavior change) |

**Rules:** English only, ≤ 72 chars, no emoji, no trailing period.

**Examples:**
```
feat: monthly log rotation with prefix-based filenames
fix: handle NULL holiday_name in bulk copy
chore: bump Newtonsoft.Json to 13.0.4
```

---

## Pull Request Rules

### Checklist

- [ ] Rebased onto target branch
- [ ] Build passes (no compiler errors)
- [ ] No secrets, passwords, or connection strings
- [ ] No `.vs/`, `bin/`, `obj/`, or IDE config files

### Title

Use Conventional Commits: `feat: monthly log rotation`

### Description

- **What** changed
- **Why** it changed
- **How to test**

### Merge Strategy

| Branch | Strategy |
|--------|----------|
| `feature/*` → `develop` | Squash & Merge |
| `bugfix/*` → `develop` | Squash & Merge |
| `hotfix/*` → `main` | Rebase & Merge |
| `develop` → `main` (release) | Merge commit |

---

## Release Workflow

1. All planned features merged into `develop`
2. PM opens PR: `develop` → `main`
3. PM merges (only PM can do this)
4. Deploy from `main`

No feature branch may merge directly into `main`. Only `hotfix/*` and `develop` target `main`.

---

## Hotfix Workflow

```
  main ←── hotfix/fix ── PR ──▶ main
                                   │
                                   ▼
                              merge back to develop
```

1. Branch from `main`: `git checkout -b hotfix/fix main`
2. Fix → commit → push → PR targeting `main`
3. PM merges into `main`, deploys
4. PM opens PR: `main` → `develop` to sync back

---

## Protected Branches

| Branch | Rules |
|--------|-------|
| `main` | No direct push, no force push, no deletion. PR + 1 approval required. PM-only merge. |
| `develop` | No direct push by developers. PR + 1 approval required. |

---

## Deployment Policy

- Only `main` deploys to production.
- `develop` and `feature/*` are never deployed.
- No developer may deploy directly.

---

## Quick Reference

```bash
# Start feature
git checkout develop && git pull origin develop
git checkout -b feature/name

# Sync with develop
git fetch origin && git rebase origin/develop
git push --force-with-lease

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Verify before push
git diff origin/develop..HEAD --stat
```
