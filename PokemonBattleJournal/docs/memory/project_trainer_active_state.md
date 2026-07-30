---
name: project_trainer_active_state
description: TrainerOperations.SaveAsync always inserts with IsActive=0 — must call SetActiveAsync separately
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T17:36:03.391Z
---

`TrainerOperations.SaveAsync(string trainerName)` does `tran.Insert(trainer)` where `trainer = new Trainer { Name = trainerName }`. Default `IsActive = false`. It does NOT activate the new trainer.

`GetActiveAsync()` queries `WHERE IsActive = 1`. If no trainer is active, it returns null.

**Normal flow:** `AppShellViewModel.LoadAsync()` calls `SwitchToAsync(trainer)` which calls `SetActiveAsync` → sets IsActive=1. This runs after `CreateWindow()` so the visual tree exists.

**Problem in seed:** Seed runs in `App` constructor before the visual tree exists. If seed creates a trainer without `SetActiveAsync`, and the app crashes or the shell never loads, the trainer stays inactive forever. Subsequent seed runs see the trainer exists and return early — leaving it inactive.

**Fix pattern in seed:**
```csharp
var existing = trainers.FirstOrDefault(t => t.Name == "UITestTrainer");
if (existing != null)
{
    if (!existing.IsActive)
        await factory.Trainers.SetActiveAsync(existing);
    return;
}
// ... create trainer ...
await factory.Trainers.SetActiveAsync(trainer); // ALWAYS after create
```

**How to apply:** Whenever creating a trainer programmatically (seed, migration, test helper), always call `SetActiveAsync` immediately after. Never assume AppShellViewModel will run to activate it.
