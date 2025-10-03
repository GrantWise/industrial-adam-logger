# GitHub Workflow & Best Practices

**Project:** Industrial ADAM Logger
**Date:** October 3, 2025

---

## GitHub Best Practices for This Project

### 1. Branch Strategy

#### Main Branches
- **`main`** (or `master`) - Production-ready code
  - Always stable and deployable
  - Protected with branch protection rules
  - Requires pull request reviews before merge

#### Development Workflow
```
main (protected)
  └── feature/functional-fixes-2024-10  (our working branch)
       ├── fix/data-quality-unavailable
       ├── fix/async-void-event-handler
       ├── fix/device-restart-race-condition
       └── ... (one branch per major fix OR all in one branch)
```

#### Branch Naming Convention
- `feature/` - New features
- `fix/` - Bug fixes
- `refactor/` - Code refactoring
- `docs/` - Documentation updates
- `test/` - Test additions/updates
- `chore/` - Maintenance tasks

**For our fixes, we have two options:**

**Option A: Single Feature Branch (RECOMMENDED)**
```bash
feature/functional-fixes-2024-10
```
- ✅ Easier to manage
- ✅ All fixes tested together
- ✅ Single PR for review
- ✅ Cleaner git history
- ❌ Larger code review

**Option B: Multiple Fix Branches**
```bash
fix/data-quality-unavailable
fix/async-void-event-handler
fix/device-restart-race-condition
# ... etc
```
- ✅ Smaller code reviews
- ✅ Can merge incrementally
- ❌ More complex merge management
- ❌ Potential merge conflicts between fixes

**RECOMMENDATION: Use Option A (single branch)** since fixes are related and should be tested together.

---

### 2. GitHub Issues Strategy

#### Issue Types

**Epic Issue (Parent):**
```markdown
Title: [EPIC] Fix Critical Functional Issues for Production Readiness
Labels: epic, priority-high, type-bug

Description:
Parent issue tracking 11 critical/high/medium functional fixes identified in code review.

**Goal:** Resolve all functional issues to enable proper testing before production deployment.

**Related Documents:**
- CRITICAL-REVIEW.md
- FUNCTIONAL-FIX-PLAN.md
- FIX-PROGRESS-TRACKER.md

**Child Issues:**
- #1 Fix async void event handler
- #2 Add DataQuality.Unavailable state
- #3 Fix device restart race condition
- #4 Implement IAsyncDisposable pattern
- #5 Fix blocking async in GetHealthStatus
- #6 Add circuit breaker for database
- #7 Add retry to DLQ file I/O
- #8 Validate table name (SQL injection)
- #9 Fix timer disposal race condition
- #10 Add database init timeout
- #11 Integration testing validation

**Phases:**
- [ ] Phase 1: Critical fixes (#1-3)
- [ ] Phase 2: High priority (#4-6)
- [ ] Phase 3: Medium priority (#7-10)
- [ ] Phase 4: Validation (#11)

**Success Criteria:**
- All 11 issues resolved
- 100% test pass rate
- Zero data loss under load
- All documentation updated
```

**Individual Issues (Children):**
```markdown
Title: Fix async void event handler in AdamLoggerService
Labels: priority-critical, type-bug, phase-1

Description:
**Problem:**
`OnReadingReceived` is async void which can cause unhandled exceptions and silent data loss.

**Impact:**
- Data loss when storage fails
- Application crashes
- No visibility into failures

**Solution:**
Replace with Channel<T>-based processing for proper async flow.

**Files to Change:**
- `src/Industrial.Adam.Logger.Core/Services/AdamLoggerService.cs`

**Testing Required:**
- [ ] Unit tests for Channel processing
- [ ] Integration test: 1000 readings without loss
- [ ] Load test: 10,000 readings/sec

**Documentation:**
- [ ] Update CHANGELOG.md
- [ ] Update CLAUDE.md with pattern

**Related:**
- Part of #[EPIC_NUMBER]
- Blocks #11 (integration testing)

**Estimated Effort:** 2 hours
```

#### Issue Labels

**Priority:**
- `priority-critical` - Must fix before any testing
- `priority-high` - Must fix before production
- `priority-medium` - Should fix before production
- `priority-low` - Nice to have

**Type:**
- `type-bug` - Bug fix
- `type-enhancement` - Improvement
- `type-security` - Security issue
- `type-performance` - Performance issue
- `type-documentation` - Documentation

**Phase:**
- `phase-1` - Critical fixes (Day 1)
- `phase-2` - High priority (Day 2)
- `phase-3` - Medium priority (Day 3)
- `phase-4` - Testing & validation

**Status:**
- `status-blocked` - Cannot proceed
- `status-in-progress` - Currently working
- `status-review` - Needs code review
- `status-testing` - In testing phase

**Component:**
- `component-core` - Core library
- `component-api` - Web API
- `component-storage` - Database/storage
- `component-devices` - Device communication

---

### 3. Pull Request Strategy

#### PR Template

Create `.github/pull_request_template.md`:

```markdown
## Description
<!-- Brief description of changes -->

## Related Issues
Closes #
Part of #

## Type of Change
- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update

## Changes Made
<!-- List main changes -->
-
-

## Testing Performed
- [ ] Unit tests added/updated
- [ ] Integration tests added/updated
- [ ] Manual testing completed
- [ ] Load testing performed

### Test Results
- Unit tests: ✅ Passing (X/X)
- Integration tests: ✅ Passing (X/X)
- Load test: X readings/sec, X ms latency

## Documentation Updates
- [ ] CHANGELOG.md updated
- [ ] README.md updated (if needed)
- [ ] CLAUDE.md updated (if patterns changed)
- [ ] API docs updated (if API changed)
- [ ] Code comments added/updated

## Checklist
- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my code
- [ ] I have commented my code, particularly in hard-to-understand areas
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] Any dependent changes have been merged and published

## Screenshots (if applicable)
<!-- Add screenshots for UI changes -->

## Additional Notes
<!-- Any additional information -->
```

#### PR Best Practices

1. **One PR for Feature Branch:**
   ```
   Title: Fix critical functional issues for production readiness
   Branch: feature/functional-fixes-2024-10 → main
   ```

2. **PR Description Should Include:**
   - Summary of all 11 fixes
   - Link to FUNCTIONAL-FIX-PLAN.md
   - Link to FIX-PROGRESS-TRACKER.md
   - Test results summary
   - Breaking changes highlighted

3. **PR Checklist:**
   - [ ] All tasks from tracker completed
   - [ ] All tests passing (100%)
   - [ ] No data loss in stress tests
   - [ ] Performance validated
   - [ ] All documentation updated
   - [ ] CHANGELOG.md comprehensive
   - [ ] Self-reviewed code
   - [ ] Ready for peer review

---

### 4. Commit Message Convention

#### Format
```
<type>(<scope>): <subject>

<body>

<footer>
```

#### Types
- `feat:` - New feature
- `fix:` - Bug fix
- `refactor:` - Code refactoring
- `perf:` - Performance improvement
- `test:` - Test additions/updates
- `docs:` - Documentation changes
- `chore:` - Maintenance tasks
- `style:` - Code style changes (formatting)

#### Examples

**Good Commits:**
```bash
# Critical fix with body
git commit -m "fix(core): replace async void event handler with Channel-based processing

- Replace OnReadingReceived async void with synchronous method
- Add Channel<DeviceReading> for backpressure handling
- Implement ProcessReadingsAsync background task
- Add comprehensive error handling and logging

Fixes #1
Part of #[EPIC]"

# Simple fix
git commit -m "fix(models): add DataQuality.Unavailable state for 21 CFR Part 11

Closes #2"

# Multiple files
git commit -m "fix(devices): prevent race condition in device restart

- Add RestartLock to DeviceContext
- Track polling task for synchronization
- Wait for old task completion before starting new
- Add timeout protection

Fixes #3"
```

**Commit Frequency:**
- Commit after each fix is complete and tested
- Don't mix multiple fixes in one commit
- Commit working code only (all tests passing)

---

### 5. Code Review Process

#### Self-Review Checklist (Before Requesting Review)
- [ ] All tests passing locally
- [ ] No console warnings or errors
- [ ] Code formatted consistently
- [ ] No commented-out code
- [ ] No debug statements or console.logs
- [ ] Documentation updated
- [ ] CHANGELOG.md updated
- [ ] No unnecessary files added
- [ ] .gitignore covers all generated files

#### Peer Review Guidelines (For Reviewer)
- [ ] Code solves stated problem
- [ ] Tests adequately cover changes
- [ ] No obvious bugs or edge cases missed
- [ ] Performance implications considered
- [ ] Security implications considered
- [ ] Error handling is comprehensive
- [ ] Logging is appropriate
- [ ] Documentation is clear and accurate
- [ ] Breaking changes are justified and documented

#### Review Timeline
- **Self-review:** Before pushing
- **Automated checks:** Immediately on push (if CI/CD setup)
- **Peer review:** Within 24 hours
- **Approval:** Within 48 hours
- **Merge:** After approval + all checks pass

---

### 6. GitHub Actions (CI/CD) - Optional but Recommended

Create `.github/workflows/build-and-test.yml`:

```yaml
name: Build and Test

on:
  push:
    branches: [ main, master, feature/** ]
  pull_request:
    branches: [ main, master ]

jobs:
  build:
    runs-on: ubuntu-latest

    services:
      postgres:
        image: timescale/timescaledb:latest-pg15
        env:
          POSTGRES_PASSWORD: postgres
          POSTGRES_DB: adam_logger_test
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5
        ports:
          - 5432:5432

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: 9.0.x

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --no-restore

    - name: Test
      run: dotnet test --no-build --verbosity normal

    - name: Upload test results
      if: always()
      uses: actions/upload-artifact@v4
      with:
        name: test-results
        path: '**/TestResults/*.trx'
```

---

### 7. Branch Protection Rules (GitHub Settings)

Recommended settings for `main` branch:

1. **Require pull request before merging**
   - ✅ Require approvals: 1
   - ✅ Dismiss stale reviews
   - ✅ Require review from code owners (if CODEOWNERS file exists)

2. **Require status checks to pass**
   - ✅ Require branches to be up to date
   - ✅ Build and test workflow

3. **Require conversation resolution**
   - ✅ All conversations must be resolved

4. **Do not allow bypassing**
   - ✅ Include administrators

5. **Restrictions** (optional)
   - Limit who can push to matching branches

---

### 8. Milestones

Create milestone in GitHub:

**Milestone: Production Readiness - Functional Fixes**
- **Due Date:** October 6, 2025
- **Description:** Resolve all critical functional issues identified in code review
- **Issues:** 11 (all fix issues)
- **Progress:** Automatically tracked by GitHub

---

### 9. Project Board (Optional but Helpful)

Create GitHub Project with columns:
- 📋 **Backlog** - Issues not yet started
- 🏗️ **In Progress** - Currently working
- 👀 **In Review** - PR submitted, awaiting review
- ✅ **Testing** - Code merged, in testing
- ✨ **Done** - Complete and validated

Automate card movement:
- Issue created → Backlog
- Issue assigned → In Progress
- PR opened → In Review
- PR merged → Testing
- Issue closed → Done

---

### 10. Release Strategy

#### Version Numbering (Semantic Versioning)
Current: `v2.0.0`
Next: `v2.1.0` (minor - new functionality, backward compatible fixes)

Format: `MAJOR.MINOR.PATCH`
- **MAJOR:** Breaking changes
- **MINOR:** New features, backward compatible
- **PATCH:** Bug fixes only

#### Release Process
1. **Create release branch:** `release/v2.1.0`
2. **Update version numbers:**
   - `Directory.Build.props`
   - All `*.csproj` files if needed
3. **Update CHANGELOG.md:**
   - Move items from Unreleased to version section
4. **Create GitHub Release:**
   - Tag: `v2.1.0`
   - Title: `v2.1.0 - Functional Fixes for Production Readiness`
   - Description: Copy from CHANGELOG.md
   - Attach build artifacts (optional)

---

## Recommended Workflow for Our Fixes

### Step 1: Commit Planning Docs (Now)
```bash
git add CLAUDE.md CRITICAL-REVIEW.md FUNCTIONAL-FIX-PLAN.md FIX-PROGRESS-TRACKER.md GITHUB-WORKFLOW.md
git commit -m "docs: add comprehensive code review and fix planning documentation

- Add CRITICAL-REVIEW.md: Detailed code review findings
- Add FUNCTIONAL-FIX-PLAN.md: 11-fix implementation plan
- Add FIX-PROGRESS-TRACKER.md: Detailed progress tracking
- Add GITHUB-WORKFLOW.md: GitHub best practices
- Update CLAUDE.md: Enhanced guidance for future work

Part of production readiness effort"

git push origin master
```

### Step 2: Create GitHub Issues (Optional but Recommended)
```bash
# Create epic issue and 11 child issues via GitHub UI or CLI
gh issue create --title "[EPIC] Fix Critical Functional Issues for Production Readiness" \
  --body-file .github/ISSUE_TEMPLATE/epic.md \
  --label "epic,priority-high,type-bug"

# Create individual issues for each fix (can be scripted)
```

### Step 3: Create Feature Branch
```bash
git checkout -b feature/functional-fixes-2024-10
git push -u origin feature/functional-fixes-2024-10
```

### Step 4: Implement Fixes (Iteratively)
```bash
# Fix 1
# ... make changes ...
git add .
git commit -m "fix(models): add DataQuality.Unavailable state for 21 CFR Part 11

- Add Unavailable enum value to DataQuality
- Update ModbusDevicePool to use Unavailable on device failure
- Update DataProcessor to handle Unavailable quality
- Add unit tests for new quality state

Closes #2
Part of #[EPIC]"

# Fix 2
# ... make changes ...
git commit -m "fix(core): replace async void event handler with Channel-based processing
..."

# Continue for all fixes...

# Push after each commit or batch
git push
```

### Step 5: Create Pull Request
```bash
# Via GitHub CLI
gh pr create \
  --title "Fix critical functional issues for production readiness" \
  --body-file .github/pull_request_body.md \
  --base master \
  --head feature/functional-fixes-2024-10 \
  --label "priority-high,type-bug" \
  --milestone "Production Readiness"

# Or via GitHub UI
```

### Step 6: Code Review & Merge
1. Request review (or self-review if solo)
2. Address review comments
3. Ensure all checks pass
4. Merge PR (squash or merge commit)
5. Delete feature branch

### Step 7: Create Release (After Merge)
```bash
git checkout master
git pull
git tag -a v2.1.0 -m "Release v2.1.0 - Functional Fixes"
git push origin v2.1.0

# Create GitHub release
gh release create v2.1.0 \
  --title "v2.1.0 - Functional Fixes for Production Readiness" \
  --notes-file CHANGELOG.md
```

---

## Quick Reference Commands

### Daily Workflow
```bash
# Start of day
git checkout feature/functional-fixes-2024-10
git pull

# After each fix
git add .
git commit -m "fix(scope): description"
git push

# End of day
git push  # Ensure all work backed up
```

### Sync with Main
```bash
# Periodically sync feature branch with main
git checkout feature/functional-fixes-2024-10
git fetch origin
git merge origin/master
# Resolve conflicts if any
git push
```

### Emergency Rollback
```bash
# Revert last commit
git revert HEAD
git push

# Reset to previous state (destructive)
git reset --hard HEAD~1
git push --force  # Use with caution!
```

---

## Summary: What We Should Do

### Minimal Approach (Quick Start)
1. ✅ Commit planning docs to master
2. ✅ Create feature branch
3. ✅ Implement all fixes
4. ✅ Create single PR with comprehensive description
5. ✅ Review, test, merge

### Recommended Approach (Best Practice)
1. ✅ Commit planning docs to master
2. ✅ Create Epic issue + 11 child issues
3. ✅ Create milestone
4. ✅ Create feature branch
5. ✅ Implement fixes, commit after each with issue references
6. ✅ Create PR with template
7. ✅ Review and merge
8. ✅ Create release with tag

### Full Enterprise Approach (Overkill for Now)
- GitHub Projects board
- Automated CI/CD
- Code owners
- Multiple reviewers
- Staging deployment
- Automated release notes

**RECOMMENDATION:** Use **Recommended Approach** - provides good balance of structure without overhead.

---

**Next Steps:**
1. Commit these planning documents
2. Push to GitHub
3. Create feature branch
4. Start implementing fixes!
