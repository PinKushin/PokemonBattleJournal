---
name: project_android_ci_gpu_flake
description: "RESOLVED (2026-08-05, feat/ci-matrix-per-fixture, first full-green run b5ba64b): Android CI failures were SIX stacked real bugs, each fixed with direct evidence — see final summary at top. Kept for the investigation record."
metadata:
  type: project
---

**Status: RESOLVED 2026-08-05** — branch `feat/ci-matrix-per-fixture`, first fully-green
run at commit `b5ba64b` (CI + Windows 5/5 + Android 5/5 + build job). The "Android CI
flake" was SIX stacked real bugs, peeled one per run with direct evidence each time:

1. **AVD name mismatch** (`c3184c2`) — workflow api-34 vs code constant api-35 →
   EnsureEmulatorRunning launched a second emulator every CI run.
2. **Our own `adb logcat` hang** — AppiumSetup killed the emulator in teardown, then the
   workflow script's logcat blocked forever on "- waiting for device -". Fixed with
   `timeout 20` + gating BOTH ends of AppiumSetup's emulator lifecycle behind `CI=true`
   (the action owns boot AND kill on CI; local unchanged).
3. **pkill footguns ×2** — bare `pkill crashpad_handler` can never match (kernel comm
   truncation to 15 chars); `pkill -f` then SIGKILLed our own shell (pattern appears in
   its own cmdline). Final form: `pkill -f 'crashpad_handle[r]'` bracket trick.
4. **Transient adbd "device offline"** at driver creation on freshly-booted starved
   emulators — fixed with driver-creation retry (3x, WaitForEmulatorBoot re-poll between).
5. **Launcher ANR dialog** ("Quickstep isn't responding") owning the entire a11y tree
   while the app rendered underneath — caught by the app-ready gate's PageSource dump.
   Fixed with `settings put global hide_error_dialogs 1` + in-gate auto-dismiss clicking
   `android:id/aerr_wait` (the setting only prevents FUTURE dialogs).
6. **THE tail-pair killer**: the note-Editor focus click landed in the Pixel profile's
   gesture-navigation home zone and BACKGROUNDED THE APP (Sentry lifecycle breadcrumbs in
   logcat: Window.Deactivated → stopped, 100ms after the click command). ColorBuffer
   errors were co-occurring noise, not the cause. Fixed: focus click is Windows-only
   (UiAutomator2 SendKeys needs no focus) + `cmd overlay enable
   com.android.internal.systemui.navbar.threebutton` — no gesture zone exists at all.

## #6 HAS RECURRED — the threebutton overlay is NOT reliably preventing it (2026-08-06)

Master `d71fb75`, Android job 31068218325, OptionsPageTests: 18 of 25 failed. Same
signature, same cause, **despite the overlay fix being in the workflow and running.**

What the artifacts show (`android-ui-timing-logs-OptionsPageTests`):

- Seven tests passed first. `OptionsPage_DeleteTag_RemovesFromList` is the first failure;
  everything after it cascades. **Do not read the failure list as "the whole fixture broke
  from the start"** — grepping only `Failed` lines gives exactly that wrong impression.
- Mid-`scrollIntoView` for `SaveTagButton`, logcat shows
  `ActivityTaskManager: START u0 {act=MAIN cat=[HOME]} cmp=com.android.launcher3/…QuickstepLauncher`,
  and the app's own breadcrumbs `Window.Deactivated` / `screen: MainActivity, state: paused`.
  **The app was backgrounded.** Everything afterwards fails on lookup because the app is not
  foreground — nothing to do with layout, scrolling, or the loading indicator.
- `SaveTagButton` sat at `boundsInScreen Rect(251, 2268 - 829, 2391)` on a 1080×2400 screen,
  i.e. its bottom edge is 9px from the bottom of the display.

### ROOT CAUSE: `cmd overlay enable` is ADDITIVE, so the #6 fix never did anything

Verified by hand against a booted API 35 emulator, 2026-08-06:

```
$ adb shell settings get secure navigation_mode
2                                            # 2 = gesture. The AVD default.
$ adb shell cmd overlay list android | grep navbar
[ ] com.android.internal.systemui.navbar.threebutton
[x] com.android.internal.systemui.navbar.gestural

$ adb shell cmd overlay enable com.android.internal.systemui.navbar.threebutton
$ adb shell cmd overlay list android | grep navbar
[x] com.android.internal.systemui.navbar.gestural       # STILL ENABLED
[x] com.android.internal.systemui.navbar.threebutton
$ adb shell settings get secure navigation_mode
2                                            # STILL GESTURE
```

`enable` turns the requested overlay on **without turning the conflicting one off**, and the
device stays in gesture navigation. Exit code 0 either way. So the fix recorded against #6 has
been a no-op in every run since it landed, and the gesture strip has been live the whole time —
which is exactly why #6 kept coming back.

The command that works is `enable-exclusive --category`, which disables the others in the
navbar category. `navigation_mode` flips to 0 on the first poll:

```
$ adb shell cmd overlay enable-exclusive --category com.android.internal.systemui.navbar.threebutton
$ adb shell settings get secure navigation_mode
0
```

**Check `navigation_mode`, never the overlay's `[x]` flag.** The run above is precisely the
case where the flag says yes and the device disagrees. 0 = three-button, 1 = two-button,
2 = gesture.

Now lives in `AppiumSetup.EnsureThreeButtonNavigation` (step 4e) rather than the workflow, so
local runs get it too, and it polls `navigation_mode` instead of sleeping. It logs loudly but
does **not** throw if the mode will not change: gesture nav is a necessary condition for the
backgrounding, not a proven trigger for any single failure, and failing every fixture on that
would trade an intermittent failure for a certain one.

**Still not established:** what precisely raised HOME in run 31068218325. The swipe is the
obvious suspect but `UiScrollable`'s default dead zone is 10% (~240px here), which should
already clear the strip. Removing gesture nav removes the whole class, so the trigger may never
need identifying — but do not write it up as proven.

**Also added:** `LogForegroundApp` on stage-3 lookup failure. Two driver round-trips that say
outright when another package owns the screen, so a backgrounding stops presenting as 17
unrelated `NoSuchElementException`s. Deliberately not `DumpVisibleElements`, which stays opt-in
because its ~30+ round-trips per call have killed sessions before.

**Correction to a claim in the 2026-08-06 handoff:** it said no scroll-to-top exists. Stage 3
of `UITests.Android/BaseTest.cs` has always called `scrollToBeginning(100)` before
`scrollIntoView`. There is no *named helper*, which is what I meant, but the behaviour is
there — do not add a second one.

Structural wins alongside: per-fixture matrix on both platforms (bounds the long-lived
driver session, isolates failures), build-once APK artifact job (~90 runner-min → ~20;
matrix jobs skip the maui-android workload entirely), stage-3 lookup scrolls to top first
(UiScrollable only flings down), 90s app-ready gate with diagnostic tree dump,
soft-keyboard dismissal after typing, nav-drawer click retry, console-mirrored logs with
explicit per-line flush.

**Investigation lesson:** every "flake" here was a real bug wearing a flake costume.
Guessing failed repeatedly (GPU theory, keyboard theory, cold-start theory); what closed
each one was instrumentation — per-fixture PerfLog/NavLog artifacts, live console
mirroring, logcat capture, the gate's PageSource dump, and Sentry's lifecycle breadcrumbs.

---

Historical record below — theories in the order they were held, several since corrected.
Do not act on the sections below without reading the final summary above.

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

**Candidate approaches:**
1. ~~Recycle the driver session once per `[TestFixture]` class instead of once per
   assembly~~ — **IMPLEMENTED 2026-08-05** via CI matrix split (branch
   `feat/ci-matrix-per-fixture`), not an in-process driver recycle. Each of the 5 test-
   fixture classes (`AboutPageTests`, `MainPageTests`, `OptionsPageTests`,
   `ReadJournalPageTests`, `TrainerPageTests`) now runs as its own GitHub Actions matrix
   job with `--filter "FullyQualifiedName~<fixture>"`, giving each a genuinely fresh
   process/driver/emulator instead of just a fresh in-process session — simpler than
   threading a recycle point through `TestBase`/`AppiumSetup`, at the cost of ~5x
   build/boot compute (accepted — see [[project_ci_workflows]]). Windows matrix confirmed
   all 5 jobs green, ~9-10min each running concurrently (wall time flat vs. before, as
   expected — this trades cost for reliability, not speed). Android matrix result pending
   as of this writing — that's the real test, since Android was where the degradation
   actually caused failures.
2. Recycle after N tests within a long fixture (MainPageTests specifically) via a counter
   in `TestBase` — not needed if the matrix split alone resolves it; keep as a fallback if
   any single fixture class is still large enough to degrade on its own.
3. Investigate whether the specific driver/Appium version has a known session-longevity
   fix or a "reset session" capability that's cheaper than a full teardown/recreate — not
   needed given (1) worked.

**Local dev unaffected:** this only changes CI workflow YAML. Local test runs still use one
shared driver session per platform — that's intentional (Fast Deployment reuse, fast
iteration matter more locally, and the degradation isn't hit in normal single-fixture local
runs).

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
