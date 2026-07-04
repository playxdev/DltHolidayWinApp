# Git Workflow & Rules

## Branching Model (Git Flow)

```
main        ── Production (stable, deployable)
develop     ── Integration (latest working code)
feature/*   ── New features
bugfix/*    ── Development bug fixes
hotfix/*    ── Production hotfixes
```

### Branch Naming

| Type      | Pattern                | Example                              |
|-----------|------------------------|--------------------------------------|
| feature   | `feature/<kebab-case>` | `feature/monthly-log-rotation`       |
| bugfix    | `bugfix/<kebab-case>`  | `bugfix/curl-timeout-handling`       |
| hotfix    | `hotfix/<kebab-case>`  | `hotfix/tls12-fallback`              |

### Lifecycle

```
feature/* ──PR──▶ develop ──PR──▶ main
bugfix/*  ──PR──▶ develop
hotfix/*  ──PR──▶ main ──merge──▶ develop
```

---

## Commit Convention

```
<type>: <short description>
```

| Type     | When                                     |
|----------|------------------------------------------|
| `feat`   | New feature or enhancement               |
| `fix`    | Bug fix                                  |
| `docs`   | Documentation only                       |
| `chore`  | Build, config, dependencies, tooling     |
| `refactor` | Code restructuring (no behavior change) |

### Examples

```
feat: monthly log rotation with prefix-based filenames
fix: handle NULL holiday_name in bulk copy
chore: bump Newtonsoft.Json to 13.0.4
```

### Rules

- First line ≤ 72 characters
- Use English language only
- No emojis in commit messages
- No period at the end of the subject line

---

## Pull Request Rules

### Before Opening a PR

1. Branch is up-to-date with its **target branch** (`rebase` or `merge`):
   ```bash
   git checkout feature/my-feature
   git fetch origin
   git rebase origin/develop   # or origin/main for hotfix
   ```

2. Code builds without errors (Visual Studio / MSBuild)
3. No secrets, passwords, or connection strings committed
4. No `.vs/`, `bin/`, `obj/`, or IDE config files included

### PR Title

Match the commit convention:
```
feat: monthly log rotation with prefix-based filenames
```

### PR Description

- **What** — summary of changes
- **Why** — reason for the change
- **How to test** — steps to verify

### Merge Strategy

- **Squash & Merge** for `feature/*` and `bugfix/*` branches
- **Rebase & Merge** for `hotfix/*` branches (preserves atomicity)
- Only merge when CI is green (if configured)

---

## Common Commands

### Start a new feature
```bash
git checkout develop
git pull origin develop
git checkout -b feature/my-feature
```

### Sync feature branch with develop
```bash
git fetch origin
git rebase origin/develop
git push --force-with-lease
```

### Undo last commit (keep changes)
```bash
git reset --soft HEAD~1
```

### Before pushing, verify
```bash
git status
git diff origin/develop..HEAD --stat
```

---

## Repository

| Remote | URL                                    |
|--------|----------------------------------------|
| origin | https://github.com/playxdev/DltHolidayWinApp |

---

## Branch Protection (Recommended)

| Branch  | Rules                                                    |
|---------|----------------------------------------------------------|
| `main`  | Require PR, 1+ approval, up-to-date with base, no bypass |
| `develop` | Require PR, 1+ approval, up-to-date with base           |
