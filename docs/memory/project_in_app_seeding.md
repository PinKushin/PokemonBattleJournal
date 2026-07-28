---
name: project_in_app_seeding
description: "How UI test data is seeded — in-app #if DEBUG in App constructor, not externally"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T17:35:22.971Z
---

Seeding lives in `PokemonBattleJournal/App.xaml.cs` `SeedDebugDataAsync`, compiled only under `#if DEBUG`. Called from App constructor via `Task.Run(...).GetAwaiter().GetResult()` so it completes before MAUI's visual tree starts.

**Why in-app, not external:** MAUI unpackaged Windows stores the DB under a machine-specific GUID path (`%LOCALAPPDATA%\User Name\{GUID}\Data\PokemonBattleJournal.db3`). External processes can't reliably compute this path. The app itself uses `Constants.DatabasePath` which resolves correctly.

**Seed logic (idempotent):**
1. `GetAllAsync()` — look for existing UITestTrainer
2. If found but `IsActive=false` → call `SetActiveAsync` (previous run may have crashed before AppShellViewModel activated it), then return
3. If not found → `SaveAsync("UITestTrainer")`, `GetByNameAsync`, `SetActiveAsync` (CRITICAL — see below), then insert 3 Win matches using "Other" archetype (queried directly from DB, bypassing the HTTP meta-deck call in `ArchetypeOperations.GetAllAsync`)

**Why SetActiveAsync is critical:** `TrainerOperations.SaveAsync` inserts with `IsActive=0`. `GetActiveAsync()` filters `WHERE IsActive=1`. If UITestTrainer is inactive, `MainPageViewModel.AppearingAsync` sees `_trainer = null` and calls `DisplayPromptAsync` → WinUI ContentDialog crash (see [[project_winui_xamlroot_crash]]).

**How to apply:** Never skip `SetActiveAsync` after creating a trainer in seed. Also handle the "exists but inactive" case (step 2 above) — crashed previous runs leave it inactive.
