---
name: project_android_seeder_persistent_db
description: Android DB persists across test runs; seeder must check match count before early-returning or tests get 0 data
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T20:52:36.420Z
---

Android SQLite DB is not wiped between test runs (unlike Windows which uses WipeAppData). A failed seed run (e.g. Limitless name mismatch causing archetype lookup to fail) can leave UITestTrainer in the DB with 0 matches. On the next run the seeder sees the trainer exists and returns early — all ReadJournal and TrainerPage tests then fail with no data.

## CORRECTION 2026-08-05 — the DB survives because the wipe is BROKEN, not by design

This note originally read as though DB persistence was an accepted property of the Android
local path. It is not. `AppiumSetup` step 4d intends to wipe it:

```csharp
RunAdb($"shell rm -f /data/data/{AppPackage}/files/*.db3", timeoutMs: 5_000);
```

That command **always fails**. The adb shell user cannot enter another app's data directory
on a non-rooted emulator, and `rm -f` suppresses the error:

```
$ adb shell ls /data/data/com.PinKushin.PokemonBattleJournal/files/
ls: …: Permission denied            EXIT=1

$ adb shell run-as com.PinKushin.PokemonBattleJournal ls -la files/
-rw------- … 90112 2026-08-05 15:41 PokemonBattleJournal.db3     ← survived every run
```

Three swallowed signals let it hide: `RunAdb` ignores the `WaitForExit(int)` return value,
never checks `ExitCode`, and redirects stderr without ever reading it — so
`rm: Permission denied` went nowhere.

**Use `run-as` for a debuggable (Debug) app:**
`adb shell run-as <pkg> rm -f files/PokemonBattleJournal.db3`

**CI is unaffected** — it sets `ANDROID_USE_INSTALLED=0` and takes the `pm clear` path, which
genuinely works. This is a local-only defect, which is exactly why local runs accumulate state
while CI stays clean.

**The fix does NOT make the seeder newly load-bearing** (a claim in the
`fix/clicktab-verify-retry` merge message that overstated the risk — user corrected it
2026-08-05). `pm clear` already wipes all app data, so **every CI run, across all five
fixtures, has always started from an empty DB and re-seeded from scratch** — and has been
green doing so. Fixing the local wipe moves local onto the path CI has been continuously
proving. The risk ran the other way: local was the environment testing against stale
accumulated data, i.e. the weaker signal of the two.

The seeder's count-check (below) is still correct and worth keeping — it defends against a
half-seeded DB regardless of why one exists.

**Why:** `GetByTrainerIdAsync` count check was added in commit 55aeee9. Before that, any reason the seeder aborted mid-seed would silently leave a broken state that persisted forever.

**How to apply:** The seeder checks `GetByTrainerIdAsync(existing.Id, includeRelated: false).Count > 0` before early-returning. If 0 matches, falls through and re-seeds. Always check count, not just existence, when early-returning from a seeder that targets a persistent store.

Also: DebugDataSeeder archetype lookups use `Contains(OrdinalIgnoreCase)` not exact match — live Limitless returns "Charizard ex" not "Charizard"; both must resolve.
