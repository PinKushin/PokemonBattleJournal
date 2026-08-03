---
name: project-optionspage-crash-fresh-db
description: OptionsPage crashes WinUI (0xc000027b in Microsoft.UI.Xaml.dll) on fresh DB when Limitless HTTP fails
metadata:
  type: project
---

OptionsPage navigation crashes the app on a fresh DB. Confirmed via Windows Event Log: exception code 0xc000027b (STATUS_STOWED_EXCEPTION) in Microsoft.UI.Xaml.dll offset 0x3ad79d — same crash, same offset, repeatable.

**Root cause:** `ArchetypeOperations.GetAllAsync` makes a live Limitless HTTP call (`GetTopDecksAsync`) BEFORE acquiring the DB lock. If that call throws (network timeout, etc.), the catch block calls `ModalErrorHandler.HandleError(ex)` → `shell.DisplayAlertAsync(...)`. On WinUI, showing a ContentDialog during OptionsPage `AppearingAsync` (i.e., while the page is still composing) crashes with the XamlRoot exception — same class of bug as [[project-winui-xamlroot-crash]].

**With existing DB:** GetAllAsync still makes the HTTP call, but the DB already has archetypes. If the HTTP call succeeds, inserts are fast no-ops. If it fails, the error handler fires — but on a warm DB the timing is different and apparently doesn't always crash.

**With fresh DB:** The HTTP call is the first meaningful action. Any failure → dialog during page load → crash. Also: with no existing archetypes, the DB state after the crash is empty, so subsequent tests find nothing.

**Why:** `GetAllAsync` is called from `AppearingAsync` which fires on every navigation to OptionsPage. The Limitless HTTP call has no timeout guard in the path to ModalErrorHandler.

**Fix candidates:**
1. Move the Limitless fetch out of `GetAllAsync` into a separate background refresh that doesn't block `AppearingAsync`.
2. Add a null/availability guard to `ModalErrorHandler.HandleError` — only show the dialog if `Shell.Current?.CurrentPage?.Window?.Content` has a XamlRoot.
3. Wrap the `GetTopDecksAsync` call in its own try/catch that swallows network errors silently (log only, no dialog) — the DB fallback handles the offline case.

**How to apply:** Do not call `ModalErrorHandler.HandleError` from inside `ArchetypeOperations.GetAllAsync`. Log the error instead. The offline fallback (hardcoded seed) already handles the no-network case gracefully.
