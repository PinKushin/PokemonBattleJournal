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

**Why:** `GetByTrainerIdAsync` count check was added in commit 55aeee9. Before that, any reason the seeder aborted mid-seed would silently leave a broken state that persisted forever.

**How to apply:** The seeder checks `GetByTrainerIdAsync(existing.Id, includeRelated: false).Count > 0` before early-returning. If 0 matches, falls through and re-seeds. Always check count, not just existence, when early-returning from a seeder that targets a persistent store.

Also: DebugDataSeeder archetype lookups use `Contains(OrdinalIgnoreCase)` not exact match — live Limitless returns "Charizard ex" not "Charizard"; both must resolve.
