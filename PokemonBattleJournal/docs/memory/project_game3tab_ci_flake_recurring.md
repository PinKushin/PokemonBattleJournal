---
name: project_game3tab_ci_flake_recurring
description: MainPage_Game3Tab_ShowsGamePanel / ShowsWhenGame1IsTie fail on Windows CI polling for UserNoteInput2 for 36s+, 3 occurrences this session, always fixed by rerun — not yet root-caused
metadata:
  type: project
---

**Status: open, not root-caused.** Observed 3 separate times in the 2026-08-05 session on
Windows CI (`ui-tests-windows.yml`), always the same test pair, always the same shape,
always resolved by a plain `gh run rerun` with no code change:

- `MainPage_Game3Tab_ShowsGamePanel`
- `MainPage_Game3Tab_ShowsWhenGame1IsTie`

## Symptom

After `EnsureBO3On` reports the Game2 tab appeared, something (a helper polling loop, not
yet traced to its exact call site) checks for `UserNoteInput2` — a Game 2 panel element —
every ~2.8s for 30-40 straight seconds, always "not found," before the test finally times
out and fails with `NoSuchElementException` at the actual assertion target (line ~451,
inside the test body itself, not the polling helper). `ResetGame1Tab` cleanup still runs
and reports panels gone successfully afterward.

Confirmed via the new console-mirroring (`PerfLog`/`NavLog` now echo to `Console.WriteLine`
when `CI=true` — see [[project_ci_workflows]]) on run `30980236625`, job
`Windows UI Tests (MainPageTests)`, 2026-08-05 06:13-06:15 UTC:

```
[PerfLog] START MainPage_Game3Tab_ShowsGamePanel
[PerfLog] EnsureBO3On: Game2Tab appeared in 410ms (attempt 1)
[NavLog] UIA check 'UserNoteInput2': not found   (repeated ~10x, ~2.8s apart)
[PerfLog] ResetGame1Tab: Game 2/3 panels gone on attempt 1
[PerfLog] END MainPage_Game3Tab_ShowsGamePanel [Failed] 44932ms
```

This is genuinely useful live visibility we didn't have before — this is the first time
we've seen the EXACT polling behavior during the hang rather than just the final timeout
error. Previous occurrences of this same flake this session were diagnosed only from the
post-hoc trx error message, without this detail.

## Why this looks like a real, specific gap — not generic flakiness

`EnsureBO3On` (see [[feedback_bo3_state_idempotent]]) already confirms `Game2Tab` appeared
before returning. But `UserNoteInput2` — a sibling element inside the same Game 2 panel —
is not found for 30-40 seconds afterward. That gap between "the tab exists" and "the panel
contents are queryable" suggests the Game 2 panel's own child controls render/bind on a
separate, slower timeline than the tab switch itself — a real timing gap in the app or in
how MAUI/WinAppDriver expose it, not pure CI noise. The consistent recurrence (3/3 this
session, same test pair, same element) supports a real gap over random flakiness, even
though a rerun clears it every time.

## Not yet done

- Trace the exact call site of the `UserNoteInput2` polling loop (likely inside the test
  body's own Game 2 panel wait, not `EnsureBO3On` itself — `EnsureBO3On` only confirms the
  tab, not panel contents).
- Consider extending the click-verify-retry / gate pattern already used for `EnsureBO3On`
  to whatever waits for Game 2 panel contents to be queryable, rather than relying on reruns.
- Held per user instruction (2026-08-05) — Android CI work (crashpad_handler teardown hang,
  see [[project_android_ci_gpu_flake]]) is the active priority; this is parked for later.

## Related

- [[feedback_bo3_state_idempotent]] — the existing EnsureBO3On idempotent-wait pattern
- [[project_game3tab_test_bug]] — a DIFFERENT, already-RESOLVED Game3Tab issue (content-desc
  reset on Android) — do not confuse the two, this one is Windows-only and unresolved
- [[project_ci_workflows]] — the console-mirroring change that surfaced this detail
