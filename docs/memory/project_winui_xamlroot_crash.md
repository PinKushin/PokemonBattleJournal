---
name: project_winui_xamlroot_crash
description: WinUI XamlRoot crash from ContentDialog before window is composed — root cause and fix
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T17:35:39.332Z
---

**Symptom:** App crashes on startup with `System.ArgumentException: "The parameter is incorrect. This element does not have a XamlRoot."` (HRESULT `0x80070057`). Stack: `ContentDialog.ShowAsync()` → `AlertManager.AlertRequestHelper.<ShowPrompt>` → `AlertManager.AlertRequestHelper.<OnPromptRequested>` → `DispatcherQueueSynchronizationContext.<Post>`.

**Root cause:** `MainPageViewModel.AppearingAsync()` calls `Shell.Current.DisplayPromptAsync(...)` (the first-boot trainer-name prompt) when `_trainer` is null. MAUI's `AlertManager` calls `ContentDialog.ShowAsync()`. On WinUI 3, ContentDialog requires `XamlRoot` to be set — but this fires before the window's visual tree is fully composed, so XamlRoot is null.

**Guard that fires:** `_trainer = _switchService.ActiveTrainer ?? await _connection.Trainers.GetActiveAsync()`. If both return null (UITestTrainer inactive or absent), the prompt fires.

**How fixed:**
1. `App.xaml.cs` seed always calls `SetActiveAsync` after creating or finding UITestTrainer → `GetActiveAsync()` returns it → `_trainer` non-null → no prompt
2. `MainPageViewModel.AppearingAsync`: skip prompt when `%TEMP%\PokemonBattleJournal.uitest` sentinel file exists (written by `UITests.Windows/AppiumSetup.RunBeforeAnyTests()`, deleted in `Dispose()`)
3. The `#if DEBUG` `Debugger.Break()` in generated `App.g.cs` is what VS stops at — not an error in itself; it fires because the unhandled exception propagates to WinUI's `UnhandledException` handler

**How to apply:** Any new `DisplayAlertAsync` / `DisplayPromptAsync` called from page Appearing events must be guarded. If it can fire before `CreateWindow()` returns and composes the visual tree, it will crash the same way.
