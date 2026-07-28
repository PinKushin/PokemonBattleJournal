---
name: project_uitest_sentinel_file
description: Sentinel file pattern for suppressing UI-only prompts during test runs
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-28T17:35:50.303Z
---

**Pattern:** `UITests.Windows/AppiumSetup.RunBeforeAnyTests()` writes `%TEMP%\PokemonBattleJournal.uitest` before launching the app. `Dispose()` deletes it. The app reads `File.Exists(Path.Combine(Path.GetTempPath(), "PokemonBattleJournal.uitest"))` at runtime to know it's under test.

**Why this instead of `#if DEBUG`:** User wants to manually test first-boot UX in debug sessions. `#if DEBUG` would suppress the prompt always. The sentinel is only present when the test runner is active.

**Current use:** `MainPageViewModel.AppearingAsync()` skips `DisplayPromptAsync` when sentinel is present — prevents [[project_winui_xamlroot_crash]].

**Android:** Sentinel file doesn't cross the emulator boundary (`Path.GetTempPath()` on Android is inside the app sandbox). Android tests rely on the in-app seed activating UITestTrainer, not the sentinel. The first-boot prompt uses ContentDialog on Windows only; on Android, MAUI uses native dialogs which don't require XamlRoot.

**How to apply:** Add sentinel check to any prompt/dialog that should not fire during automated tests but should still work for manual dev testing. Always write in `RunBeforeAnyTests`, delete in `Dispose`.
