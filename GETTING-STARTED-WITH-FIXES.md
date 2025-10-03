# Getting Started with Functional Fixes

**Status:** ✅ Ready to Begin
**Branch:** `feature/functional-fixes-2024-10`
**Date:** October 3, 2025

---

## ✅ What's Been Done

### 1. Planning Documents Created
- ✅ **CRITICAL-REVIEW.md** - Complete code review (17 issues identified)
- ✅ **FUNCTIONAL-FIX-PLAN.md** - Detailed implementation plan (11 fixes in 3 phases)
- ✅ **FIX-PROGRESS-TRACKER.md** - Progress tracking with testing checklists
- ✅ **GITHUB-WORKFLOW.md** - GitHub best practices and workflow
- ✅ **CLAUDE.md** - Updated with commands and architecture

### 2. Git Setup Complete
- ✅ All planning docs committed to `master`
- ✅ Pushed to GitHub: https://github.com/GrantWise/industrial-adam-logger
- ✅ Feature branch created: `feature/functional-fixes-2024-10`
- ✅ Feature branch pushed and tracking remote

### 3. Current Branch Status
```bash
Current branch: feature/functional-fixes-2024-10
Tracking: origin/feature/functional-fixes-2024-10
Status: Clean, ready for changes
```

---

## 📋 What We're Fixing (Priority Order)

### Phase 1: Critical (Day 1 - 6-8 hours)
1. **Add DataQuality.Unavailable** (1 hour) ⬅️ START HERE
2. **Fix Async Void Event Handler** (2 hours)
3. **Fix Device Restart Race Condition** (3 hours)

### Phase 2: High Priority (Day 2 - 5-6 hours)
4. **Implement IAsyncDisposable** (2 hours)
5. **Fix Blocking GetHealthStatus** (1 hour)
6. **Add Circuit Breaker** (2 hours)

### Phase 3: Medium Priority (Day 3 - 3-4 hours)
7. **Add DLQ File Retry** (1.5 hours)
8. **Validate Table Name** (30 min)
9. **Fix Timer Disposal** (30 min)
10. **Add DB Init Timeout** (30 min)
11. **Integration Testing** (2 hours)

**Total Effort:** 14-18 hours over 3 days

---

## 🎯 Recommended Next Steps

### Option A: Quick Start (Recommended for Solo Dev)
```bash
# 1. Start with easiest fix (DataQuality.Unavailable)
# See FUNCTIONAL-FIX-PLAN.md for implementation details

# 2. Make changes, test, commit
git add .
git commit -m "fix(models): add DataQuality.Unavailable state for 21 CFR Part 11

- Add Unavailable enum value to DataQuality
- Update ModbusDevicePool to use Unavailable on device failure
- Update DataProcessor to handle Unavailable quality
- Add unit tests for new quality state

Part of functional fixes effort"

# 3. Continue with remaining fixes

# 4. When all done, create PR
gh pr create --title "Fix critical functional issues for production readiness" \
  --body "Resolves 11 critical/high functional issues. See FUNCTIONAL-FIX-PLAN.md" \
  --base master
```

### Option B: GitHub Issues Workflow (Recommended for Team)
```bash
# 1. Create Epic issue on GitHub
gh issue create --title "[EPIC] Fix Critical Functional Issues for Production Readiness" \
  --body "Parent issue tracking 11 fixes. See FUNCTIONAL-FIX-PLAN.md" \
  --label "epic,priority-high,type-bug"

# 2. Create 11 child issues (one per fix)
# Can be done via GitHub UI or scripted

# 3. Implement fixes, reference issues in commits
git commit -m "fix(models): add DataQuality.Unavailable state

Closes #2
Part of #1"

# 4. Create PR when complete
```

---

## 🚀 Implementation Workflow

### Daily Pattern

**Morning:**
```bash
# 1. Pull latest
git checkout feature/functional-fixes-2024-10
git pull

# 2. Check FIX-PROGRESS-TRACKER.md
# - See what's next
# - Review checklist

# 3. Read FUNCTIONAL-FIX-PLAN.md for current fix
# - Implementation approach
# - Code examples
# - Testing requirements
```

**During Work:**
```bash
# 1. Make changes
# 2. Run tests: dotnet test
# 3. Verify build: dotnet build
# 4. Update FIX-PROGRESS-TRACKER.md
#    - Check off completed tasks
#    - Note any issues
```

**End of Day:**
```bash
# 1. Commit work (even if not complete)
git add .
git commit -m "wip: working on async void fix"
git push

# 2. Update FIX-PROGRESS-TRACKER.md daily status
# 3. Update TODO list for tomorrow
```

### Per-Fix Pattern

**For Each Fix:**
```bash
# 1. ✅ Read implementation plan
# 2. ✅ Make code changes
# 3. ✅ Run unit tests
# 4. ✅ Run integration tests
# 5. ✅ Update documentation
# 6. ✅ Update CHANGELOG.md
# 7. ✅ Mark complete in tracker
# 8. ✅ Commit with descriptive message
# 9. ✅ Push to GitHub

# Commit message format:
git commit -m "fix(scope): description

- Change 1
- Change 2
- Change 3

Closes #issue (if using issues)
Part of functional fixes"
```

---

## 📚 Key Documents Reference

### Planning & Tracking
- **FUNCTIONAL-FIX-PLAN.md** - What to do (implementation details)
- **FIX-PROGRESS-TRACKER.md** - Track progress (checklists)
- **CRITICAL-REVIEW.md** - Why we're doing this (issues found)

### Process & Standards
- **GITHUB-WORKFLOW.md** - How to work with Git/GitHub
- **CLAUDE.md** - Coding standards and architecture

### Testing
```bash
# Run all tests
dotnet test

# Run specific project
dotnet test src/Industrial.Adam.Logger.Core.Tests

# Run with coverage (if configured)
./scripts/run-coverage.sh

# Integration tests (requires Docker)
docker-compose up -d timescaledb
dotnet test src/Industrial.Adam.Logger.IntegrationTests
```

---

## 🔍 First Fix: DataQuality.Unavailable (START HERE!)

### Why This First?
- ✅ Easiest (1 hour)
- ✅ High impact (compliance)
- ✅ No dependencies
- ✅ Quick win

### Implementation Steps

**1. Update Enum:**
```csharp
// File: src/Industrial.Adam.Logger.Core/Models/DataQuality.cs
public enum DataQuality
{
    Good = 0,
    Degraded = 1,
    Bad = 2,
    Unavailable = 3  // ADD THIS
}
```

**2. Update ModbusDevicePool:**
```csharp
// File: src/Industrial.Adam.Logger.Core/Devices/ModbusDevicePool.cs:237-244
if (!result.Success)
{
    _healthTracker.RecordFailure(deviceId, result.Error ?? "Unknown error");

    // CREATE UNAVAILABLE READING
    var unavailableReading = new DeviceReading
    {
        DeviceId = deviceId,
        Channel = channel.ChannelNumber,
        RawValue = 0,
        ProcessedValue = 0,
        Timestamp = DateTimeOffset.UtcNow,
        Quality = DataQuality.Unavailable,  // USE NEW STATE
        Unit = channel.Unit
    };

    ReadingReceived?.Invoke(unavailableReading);
}
```

**3. Test:**
```bash
# Run tests
dotnet test src/Industrial.Adam.Logger.Core.Tests

# Should all pass
```

**4. Update Tracker:**
- Open FIX-PROGRESS-TRACKER.md
- Check off all tasks for Fix 1
- Mark as completed with date

**5. Commit:**
```bash
git add .
git commit -m "fix(models): add DataQuality.Unavailable state for 21 CFR Part 11

- Add Unavailable enum value to DataQuality
- Update ModbusDevicePool to use Unavailable on device failure
- Update DataProcessor to handle Unavailable quality
- Add unit tests for new quality state

Part of functional fixes effort"

git push
```

**Done! Move to Fix 2.**

---

## 🧪 Testing Strategy

### After Each Fix
```bash
# 1. Unit tests
dotnet test src/Industrial.Adam.Logger.Core.Tests

# 2. Build verification
dotnet build

# 3. Check for warnings
# Should see 0 errors, minimal warnings
```

### After All Fixes (Integration)
```bash
# 1. Start infrastructure
cd docker
docker-compose up -d timescaledb

# 2. Start simulators
./scripts/start-simulators.sh

# 3. Run integration tests
dotnet test src/Industrial.Adam.Logger.IntegrationTests

# 4. Run logger
dotnet run --project src/Industrial.Adam.Logger.WebApi

# 5. Manual testing
curl http://localhost:5000/health
curl http://localhost:5000/data/latest

# 6. Load test (if benchmarks exist)
dotnet run --project src/Industrial.Adam.Logger.Benchmarks -c Release
```

---

## 📝 Documentation Updates

### Files to Update (As You Go)
- **CHANGELOG.md** - Add entry for each fix
- **FIX-PROGRESS-TRACKER.md** - Check off tasks
- **API Docs** - Update Swagger comments if API changes
- **CLAUDE.md** - Add new patterns if applicable

### CHANGELOG.md Format
```markdown
## [Unreleased]

### Fixed
- Add DataQuality.Unavailable state for 21 CFR Part 11 compliance (#2)
- Replace async void event handler with Channel-based processing (#1)
- Fix race condition in device restart operations (#3)
- [... continue for all fixes]

### Changed
- Implement IAsyncDisposable pattern for clean shutdown (#4)
- Add circuit breaker to database operations for resilience (#6)

### Added
- Retry logic for Dead Letter Queue file I/O (#7)
- Table name validation to prevent SQL injection (#8)
- Timeout protection for database initialization (#10)
```

---

## 🎉 When You're Done

### Pre-PR Checklist
- [ ] All 11 fixes implemented
- [ ] All tests passing (100%)
- [ ] FIX-PROGRESS-TRACKER.md fully checked off
- [ ] CHANGELOG.md updated
- [ ] Documentation updated
- [ ] No warnings in build
- [ ] Code self-reviewed

### Create Pull Request
```bash
# Via GitHub CLI
gh pr create \
  --title "Fix critical functional issues for production readiness" \
  --body "$(cat <<'EOF'
## Summary
Resolves 11 critical/high functional issues identified in code review.

## Documents
- CRITICAL-REVIEW.md - Original findings
- FUNCTIONAL-FIX-PLAN.md - Implementation plan
- FIX-PROGRESS-TRACKER.md - Progress tracking

## Changes
- ✅ Add DataQuality.Unavailable state
- ✅ Fix async void event handler
- ✅ Fix device restart race condition
- ✅ Implement IAsyncDisposable pattern
- ✅ Fix blocking async in GetHealthStatus
- ✅ Add circuit breaker for database
- ✅ Add retry logic to DLQ file I/O
- ✅ Validate table name (SQL injection)
- ✅ Fix timer disposal race condition
- ✅ Add database init timeout
- ✅ Complete integration testing

## Testing
- Unit tests: ✅ 100% passing
- Integration tests: ✅ All passing
- Load test: X readings/sec, X ms latency
- Manual testing: ✅ Complete

## Breaking Changes
None (all fixes are backward compatible)

## Documentation
- [x] CHANGELOG.md updated
- [x] API docs updated
- [x] CLAUDE.md updated
- [x] Code commented
EOF
)" \
  --base master \
  --head feature/functional-fixes-2024-10 \
  --label "priority-high,type-bug"

# Or via GitHub UI
# Visit: https://github.com/GrantWise/industrial-adam-logger/pull/new/feature/functional-fixes-2024-10
```

### After PR Merged
```bash
# 1. Switch back to master
git checkout master
git pull

# 2. Delete feature branch (optional)
git branch -d feature/functional-fixes-2024-10
git push origin --delete feature/functional-fixes-2024-10

# 3. Create release (optional)
git tag -a v2.1.0 -m "Release v2.1.0 - Functional Fixes"
git push origin v2.1.0
```

---

## 🆘 Troubleshooting

### Build Fails
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Tests Fail
```bash
# Run specific test
dotnet test --filter "FullyQualifiedName~TestName"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Merge Conflicts
```bash
# Update from master
git fetch origin
git merge origin/master

# Resolve conflicts, then:
git add .
git commit -m "merge: resolve conflicts with master"
```

### Need Help
- Check FUNCTIONAL-FIX-PLAN.md for implementation details
- Check CRITICAL-REVIEW.md for context on why
- Check GITHUB-WORKFLOW.md for Git/GitHub questions
- Check CLAUDE.md for coding standards

---

## 📊 Progress Tracking

Update this daily in FIX-PROGRESS-TRACKER.md:

**Day 1:** __ / 3 fixes complete (Target: Fix 1-3)
**Day 2:** __ / 6 fixes complete (Target: Fix 4-6)
**Day 3:** __ / 11 fixes complete (Target: Fix 7-11)

---

## ✅ You're Ready!

**Current Status:**
- ✅ All planning complete
- ✅ Git setup complete
- ✅ Feature branch created
- ✅ Documentation ready

**Next Action:**
👉 **Start with Fix 1: Add DataQuality.Unavailable state**
   - Open FUNCTIONAL-FIX-PLAN.md, scroll to Fix 1
   - Follow implementation steps
   - Should take ~1 hour
   - Easy win to build momentum!

Good luck! 🚀

---

**Created:** October 3, 2025
**Branch:** feature/functional-fixes-2024-10
**Status:** Ready to begin
