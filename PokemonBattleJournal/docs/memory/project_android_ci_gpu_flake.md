---
name: project_android_ci_gpu_flake
description: Real root cause is a single long-lived Appium driver session shared across all ~72 tests, degrading over the run on BOTH Windows and Android — not GPU/emulator/graphics-churn. AVD mismatch was a separate, real, already-fixed bug. Not yet fixed.
metadata:
  type: project
---

**Status: PARTIALLY RESOLVED 2026-08-05.** The AVD name mismatch (below) was real and is
fixed (`c3184c2`). A run after that fix (30972824626, PerfLog pulled directly) then showed
FindUIElement latency climbing steadily through the suite — but the ACTUAL root cause is
below (single long-lived driver session), not GPU/graphics-churn. Keep reading before
acting on the graphics-churn section — it was a plausible-looking dead end.

## UPDATE 2026-08-05 (final): real root cause is a single long-lived driver session, NOT Android GPU

A local Windows UI test PerfLog from earlier the same session (`%TEMP%\UITests.PerfLog.txt`,
run ~19:15-19:28, well before any Android CI work) shows the **identical** degradation
shape — `FIND '...' STAGE3_FAIL after 11000ms+` climbing steadily, same as the Android CI
run. Windows runs on real hardware with a real GPU; there is no shared rendering pipeline
between Windows (WinAppDriver/UIA) and Android (UIAutomator2/emulator). A GPU-emulation
theory cannot explain the same symptom appearing identically on both platforms — the
"app graphics churn" theory in the section below was a coincidental-looking dead end.

The real shared factor: both `PokemonBattleJournal.UITests/UITests.Windows/AppiumSetup.cs`
and `PokemonBattleJournal.UITests/UITests.Android/AppiumSetup.cs` are `[SetUpFixture]`
classes using `[OneTimeSetUp]`/`[OneTimeTearDown]` — **one driver session is created once
for the entire assembly and reused across all ~72 tests in every fixture class**, never
recycled mid-run. Long-lived Appium/WebDriver sessions are a known category of issue:
internal element-cache growth and per-call JS/IPC overhead climb over the life of the
session. That matches the symptom exactly: fast for the first ~20 tests, degrading
steadily, eventually every `FindElement` times out regardless of platform.

**Not yet fixed — real tradeoff, not a quick patch.** Recycling the driver periodically
(e.g. once per test-fixture class, or every N tests) would fix the degradation but costs
real time per recycle (~10s+ driver startup on Android, several seconds on Windows) ×
however many recycle points are chosen. That directly cuts against the ReadJournal
FlexLayout win this session (18m19s → 8m44s on Android), which came specifically from
*avoiding* per-test overhead — see [[project_readjournal_android_slow]]. Any fix here
needs a deliberate speed-vs-reliability call, not a freelanced change.

**Candidate approaches (not attempted):**
1. Recycle the driver session once per `[TestFixture]` class instead of once per assembly
   — bounds the degradation window to however many tests are in the largest fixture
   (MainPageTests, ~25 tests) instead of the full ~72-test suite.
2. Recycle after N tests within a long fixture (MainPageTests specifically) via a counter
   in `TestBase`.
3. Investigate whether the specific driver/Appium version has a known session-longevity
   fix or a "reset session" capability that's cheaper than a full teardown/recreate.

## Original graphics-churn dead end (documented for the record, do not act on this)

With the double-emulator noise gone, one clean run's PerfLog shows `FindUIElement` STAGE1
latencies climbing steadily through `MainPageTests` as popup open/close cycles accumulate:
~20-50ms for the first dozen finds, then 5000ms+ STAGE1_MISS forcing STAGE2/STAGE3 fallback
by `MainPage_PlayerArchetype_DualIconDeck_ShowsBothIcons` (43.8s for one test), climbing to
11s+ scrollIntoView calls by `MainPage_UserNoteInput_ShowTextEntry` (which then fails
outright — element goes stale then unfindable). The next two tests
(`MainPage_WentFirstLabel_Displayed` fails at 16.7s) and then **every subsequent test
fixture's `NavigateTo` fails at the "open drawer" step alone** — `AccessibilityId("Open
navigation drawer")` never resolves again for the rest of the run. That's total UI/
UiAutomator unresponsiveness, not a one-off miss — degradation that started small and
compounded until the automation layer could no longer talk to the app at all.

This matches the original theory almost exactly: MainPage's popup-heavy tests
(`OpenArchetypePopup`/`DismissArchetypePopup`, each round-tripping the dual-icon Image
controls) progressively degrade something in the emulator's UI/rendering pipeline until
the whole app becomes unresponsive to Appium queries. 27/72 passed, 45/72 failed in that
run, all in the same "everything after MainPage's popup tests is dead" shape.

**Not yet fixed.** Candidate approaches (unchanged from the original list, still valid):
1. Reduce Image/Popup churn specifically in `MainPageTests` — reuse a single opened popup
   across more assertions instead of open/close per test, or split the class so a fresh
   Automator/driver session resets mid-run.
2. Try alternate `-gpu` backends in `emulator-options`.
3. Self-hosted runner with real GPU (PinPC/UbuntuBox infra already exists).
4. Bump `-memory` / add explicit buffer-pool env vars.

## Original AVD-mismatch fix (still valid, real, and worth keeping)

**Status: RESOLVED 2026-08-05**, commit `c3184c2` on master.

## Real root cause

`AppiumSetup.cs:16` — `private const string AvdName = "pixel_7_-_api_35"`, matching
local dev AVDs. `.github/workflows/ui-tests-android.yml` booted `api-level: 34` /
`avd-name: pixel_7_-_api_34` — a different AVD name.

`EnsureEmulatorRunning()` (AppiumSetup.cs:193) checks `adb emu avd name` against the
`AvdName` constant before deciding whether to launch an emulator. On CI the names never
matched, so `correctAvdRunning` was always false, and it launched a **second emulator
process** targeting `pixel_7_-_api_35` — an AVD image that doesn't exist on the CI runner
(only `_34` was created by `reactivecircus/android-emulator-runner`). Two emulator
processes then contended for KVM/`swiftshader_indirect` GPU resources on a 3-core
GitHub-hosted runner, which is what produced the `Failed to find ColorBuffer` errors and
the climbing buffer IDs previously documented below.

**Fix:** workflow now boots `api-level: 35` / `avd-name: pixel_7_-_api_35` to match the
code constant and local dev setup. `EnsureEmulatorRunning` now finds the correct AVD
already running on the first check and never spawns a second one.

## Original (incorrect) theory — kept for record

Earlier investigation in this doc concluded the failures correlated with app-side
rendering growth (dual-icon archetypes, FlexLayout chips, `43b2a90`) and recommended
reducing Image/Popup churn, trying alternate `-gpu` backends, or moving to a self-hosted
runner. That correlation was coincidental — the AVD mismatch was introduced around the
same time the api-level was bumped in the workflow for an unrelated reason, and both
changes landed in the same multi-day window as the app graphics work. The actual
mechanism (duplicate emulator process) explains the deterministic "same two tests fail
first, climbing ColorBuffer IDs" pattern far better than a gradual rendering leak would.

**Lesson:** when a hardcoded constant (`AvdName`) has to match an external config value
(workflow `avd-name`), that pairing needs either a single source of truth or an explicit
comment cross-referencing both sides — this one only had a comment on the workflow side
pointing at the code, not a check that would fail loudly if they drifted.

## Related

- [[project_self_hosted_runners]] — PinPC/UbuntuBox self-hosted runner infra (not needed
  for this fix, but still useful context for future infra decisions)
- [[project_ci_workflows]] — workflow structure
- [[feedback_android_flaky_tap_retry]] — a DIFFERENT, real flake class (app-code tap
  timing) — unrelated to this one
