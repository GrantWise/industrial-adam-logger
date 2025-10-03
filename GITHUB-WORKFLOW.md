# GitHub Workflow - Industrial ADAM Logger

**Project:** Industrial ADAM Logger
**Date:** October 3, 2025
**Status:** OFFICIAL WORKFLOW

---

## Branch Strategy

### Protected Branches
- **`master`** - Production-ready code
  - Always stable and deployable
  - Requires pull request for changes
  - Direct commits prohibited

### Working Branches
- **`feature/*`** - New features or major fixes
- **`fix/*`** - Individual bug fixes
- **`refactor/*`** - Code refactoring
- **`docs/*`** - Documentation updates

### Branch Naming Convention
```
feature/descriptive-name-with-dashes
fix/issue-description
refactor/component-being-refactored
docs/what-is-being-documented
```

**Examples:**
- `feature/functional-fixes-2024-10`
- `fix/async-void-event-handler`
- `refactor/storage-layer`
- `docs/api-reference`

---

## Commit Message Format

### Standard Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types (Required)
- `feat:` - New feature
- `fix:` - Bug fix
- `refactor:` - Code refactoring
- `perf:` - Performance improvement
- `test:` - Test additions/updates
- `docs:` - Documentation only
- `chore:` - Maintenance (dependencies, configs)
- `style:` - Code formatting only

### Scope (Optional but Recommended)
- `core` - Core library
- `api` - Web API
- `storage` - Database/storage
- `devices` - Device communication
- `models` - Domain models

### Subject Rules
- Use imperative mood ("add" not "added")
- No period at end
- Max 72 characters
- Lowercase after type/scope

### Body (Optional)
- Explain what and why, not how
- Bullet points preferred
- Wrap at 72 characters

### Footer (Required for Issues)
- Reference issues: `Closes #123` or `Part of #456`
- Breaking changes: `BREAKING CHANGE: description`

### Examples

**Simple Fix:**
```bash
git commit -m "fix(models): add DataQuality.Unavailable state"
```

**With Body:**
```bash
git commit -m "fix(core): replace async void event handler with Channel-based processing

- Replace OnReadingReceived async void with synchronous method
- Add Channel<DeviceReading> for backpressure handling
- Implement ProcessReadingsAsync background task
- Add comprehensive error handling and logging

Closes #1"
```

**With Co-author:**
```bash
git commit -m "feat(api): add circuit breaker for database operations

- Add Polly circuit breaker policy
- Configure 10 failures before breaking
- Add health status for circuit state

Part of #6

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude <noreply@anthropic.com>"
```

---

## Development Workflow

### Daily Workflow

#### Morning Routine
```bash
# 1. Switch to feature branch
git checkout feature/your-branch-name

# 2. Pull latest changes
git pull

# 3. Check for updates from master
git fetch origin
git merge origin/master  # If master has updates

# 4. Verify clean state
git status
```

#### During Development
```bash
# 1. Make changes
# 2. Test locally: dotnet test
# 3. Verify build: dotnet build

# 4. Stage changes
git add <files>  # or git add . for all

# 5. Commit with message
git commit -m "type(scope): description"

# 6. Push to remote
git push
```

#### End of Day
```bash
# 1. Ensure all work is committed
git status  # Should be clean

# 2. Push all commits
git push

# 3. Update progress tracker
# Edit FIX-PROGRESS-TRACKER.md or project board
```

### Feature Development Process

#### 1. Start New Work
```bash
# Create and checkout feature branch from master
git checkout master
git pull
git checkout -b feature/descriptive-name

# Push to create remote branch
git push -u origin feature/descriptive-name
```

#### 2. Implement Changes
```bash
# Make changes, commit frequently
git add .
git commit -m "type(scope): what changed"
git push

# Repeat for each logical unit of work
```

#### 3. Keep Branch Updated
```bash
# Periodically sync with master
git fetch origin
git merge origin/master

# Resolve any conflicts
# Then: git add . && git commit
```

#### 4. Ready for Review
```bash
# 1. Ensure all tests pass
dotnet test

# 2. Ensure no build warnings
dotnet build

# 3. Self-review changes
git diff origin/master

# 4. Push final changes
git push
```

#### 5. Create Pull Request
```bash
# Via GitHub CLI (recommended)
gh pr create \
  --title "Descriptive PR title" \
  --body "Description of changes" \
  --base master \
  --label "priority-high"

# Or via GitHub UI
# Visit: https://github.com/GrantWise/industrial-adam-logger/pulls
```

#### 6. After PR Merged
```bash
# Switch back to master
git checkout master
git pull

# Delete local feature branch
git branch -d feature/your-branch-name

# Delete remote branch (optional, can keep for history)
git push origin --delete feature/your-branch-name
```

---

## Pull Request Requirements

### PR Title Format
```
<type>: Brief description of changes
```

**Examples:**
- `fix: Resolve critical functional issues for production readiness`
- `feat: Add circuit breaker for database resilience`
- `docs: Update API reference documentation`

### PR Description Must Include

1. **Summary** - What was changed and why
2. **Changes Made** - Bullet list of key changes
3. **Testing** - What testing was performed
4. **Documentation** - What docs were updated
5. **Breaking Changes** - If any (or explicitly state "None")

### PR Checklist (All Must Be Checked)
- [ ] All tests passing (100%)
- [ ] No build warnings
- [ ] Code self-reviewed
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
- [ ] No unnecessary files added
- [ ] Commit messages follow convention

### Review Process
1. **Self-review** - Review own PR before requesting review
2. **Automated checks** - Must pass (if CI/CD configured)
3. **Peer review** - If team member available (optional for solo)
4. **Approval** - Self-approve or wait for peer
5. **Merge** - Use "Squash and merge" or "Merge commit"

---

## Issue Tracking (When Used)

### Issue Labels
- `priority-critical` - Blocking issue
- `priority-high` - Important
- `priority-medium` - Should fix
- `priority-low` - Nice to have
- `type-bug` - Bug fix
- `type-enhancement` - Improvement
- `type-documentation` - Docs only

### Issue References in Commits
```bash
# Closes an issue
git commit -m "fix(core): resolve deadlock issue

Closes #42"

# Part of larger issue
git commit -m "feat(api): add endpoint validation

Part of #100"

# References without closing
git commit -m "docs: update troubleshooting guide

See #42 for context"
```

---

## Release Process

### Version Numbering
**Current:** v2.0.0
**Format:** MAJOR.MINOR.PATCH

- **MAJOR** - Breaking changes (2.0.0 → 3.0.0)
- **MINOR** - New features, backward compatible (2.0.0 → 2.1.0)
- **PATCH** - Bug fixes only (2.0.0 → 2.0.1)

### Creating a Release

```bash
# 1. Ensure master is up to date
git checkout master
git pull

# 2. Update version in Directory.Build.props
# <Version>2.1.0</Version>

# 3. Update CHANGELOG.md
# Move [Unreleased] items to [2.1.0] section

# 4. Commit version bump
git add .
git commit -m "chore: bump version to 2.1.0"
git push

# 5. Create and push tag
git tag -a v2.1.0 -m "Release v2.1.0 - Functional Fixes"
git push origin v2.1.0

# 6. Create GitHub release
gh release create v2.1.0 \
  --title "v2.1.0 - Functional Fixes for Production Readiness" \
  --notes "$(cat CHANGELOG.md | sed -n '/## \[2.1.0\]/,/## \[/p' | head -n -1)"
```

---

## Emergency Procedures

### Hotfix Process
```bash
# 1. Create hotfix branch from master
git checkout master
git pull
git checkout -b fix/critical-production-issue

# 2. Make minimal fix
# Edit files

# 3. Test thoroughly
dotnet test

# 4. Commit and push
git add .
git commit -m "fix: critical production issue description"
git push -u origin fix/critical-production-issue

# 5. Create PR immediately
gh pr create --title "HOTFIX: Critical issue" --base master --label "priority-critical"

# 6. After approval, merge and deploy
```

### Rollback Procedure
```bash
# Revert last commit on master (if not pushed yet)
git revert HEAD
git push

# Revert specific commit (if already pushed)
git revert <commit-hash>
git push

# Emergency: Force reset (DANGEROUS - use only if necessary)
git reset --hard HEAD~1
git push --force  # Requires force-push permissions
```

---

## File Organization

### What Goes Where

#### Root Directory
- `CHANGELOG.md` - All changes log
- `README.md` - Project overview
- `CLAUDE.md` - AI assistant guide
- Planning docs (CRITICAL-REVIEW.md, etc.)

#### .github Directory
```
.github/
  ├── workflows/           # CI/CD workflows
  ├── ISSUE_TEMPLATE/      # Issue templates
  ├── PULL_REQUEST_TEMPLATE.md
  └── CODEOWNERS          # Code ownership (optional)
```

#### Documentation
```
docs/
  ├── api-reference.md
  ├── architecture-guide.md
  ├── development-standards.md
  └── troubleshooting.md
```

### What NOT to Commit
```
# Already in .gitignore:
bin/
obj/
*.user
.vs/
.vscode/
*.suo
*.DotSettings.user
appsettings.*.json (except template)
.env (use .env.template)
```

---

## Quick Reference Commands

### Common Operations
```bash
# Check status
git status

# View commit history
git log --oneline -10

# See what changed
git diff

# Uncommitted changes
git diff HEAD

# Compare branches
git diff master..feature/branch-name

# Undo last commit (keep changes)
git reset --soft HEAD~1

# Undo changes to file
git restore <file>

# Unstage file
git restore --staged <file>

# Update from master
git fetch origin
git merge origin/master

# View remote branches
git branch -r

# Clean untracked files (careful!)
git clean -fd
```

### GitHub CLI Commands
```bash
# View PRs
gh pr list

# Check PR status
gh pr status

# View issues
gh issue list

# Create issue
gh issue create --title "Issue title" --body "Description"

# Close issue
gh issue close 123
```

---

## Enforcement

### Automated Checks (When CI/CD Configured)
- ✅ All tests must pass
- ✅ Build must succeed with no errors
- ✅ Code formatting validated
- ✅ No security vulnerabilities

### Manual Checks (Before Merge)
- ✅ Commit messages follow format
- ✅ PR description complete
- ✅ Documentation updated
- ✅ CHANGELOG.md updated
- ✅ No debug code or commented code
- ✅ Tests added for new functionality

### Branch Protection (Master)
- ✅ Require pull request before merging
- ✅ Require status checks to pass
- ✅ Require conversation resolution
- ✅ No direct pushes to master

---

## Summary: Standard Workflow

### For Bug Fixes
```bash
git checkout master && git pull
git checkout -b fix/issue-description
# make changes
git add . && git commit -m "fix(scope): description"
git push -u origin fix/issue-description
gh pr create --base master
# wait for review/merge
git checkout master && git pull
```

### For Features
```bash
git checkout master && git pull
git checkout -b feature/feature-name
# make changes over time
git add . && git commit -m "feat(scope): description"
git push
# repeat commits as needed
gh pr create --base master
# wait for review/merge
git checkout master && git pull
```

### For Documentation
```bash
git checkout master && git pull
git checkout -b docs/what-updating
# make changes
git add . && git commit -m "docs: description"
git push -u origin docs/what-updating
gh pr create --base master
# merge immediately (docs don't need extensive review)
```

---

**This is the definitive workflow for this project. Follow it consistently.**

**Last Updated:** October 3, 2025
