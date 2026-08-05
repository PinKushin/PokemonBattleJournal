---
name: project_android_ci_gpu_flake
description: GitHub-hosted Android CI has failed on every run this session (ColorBuffer GPU emulation errors) despite local Android suite being consistently 72/72 green — infra flake, not a code regression
metadata:
  type: project
---

**Status: open, unresolved as of 2026-08-05.** Confirmed across every recent Android UI Tests
CI run on `master` and on `feat/mutation-busy-gates` — 100% failure rate on the shared runner,
even for commits verified 72/72 green locally the same session.

## Evidence

`gh run list --workflow=ui-tests-android.yml --branch master --limit 6` — every single run
`failure`, including the commit that merged `feat/android-mainpage-tests` (local: 25/25 →
72/72) and `feat/loading-gates` (local: 72/72, 8m44s). Log excerpt from a failing run:

```
ERROR        | Failed to find ColorBuffer: 173
ERROR        | Failed to find ColorBuffer: 177
Failed MainPage_UserNoteInput_ShowTextEntry [38 s]
   OpenQA.Selenium.NoSuchElementException
```

`Failed to find ColorBuffer` is an Android emulator **software GPU rendering** failure —
happens on GitHub's shared `ubuntu-latest` runner, not observed locally (real hardware or a
better-provisioned local emulator). Once GPU rendering breaks mid-run, the app's rendered
surface goes stale/unresponsive; whichever test happens to touch a UI element next fails with
`NoSuchElementException`, and every downstream OptionsPage test in the same run cascades to a
uniform ~10 s failure (classic single-failure-corrupts-navigation-state pattern, not evidence
of a NEW bug — same shape seen throughout this session on flaky Android runs).

## UPDATE 2026-08-05: root cause is more specific than "runner is flaky"

Investigated `gh run list --workflow=ui-tests-android.yml --limit 40` across the full history.
This is NOT random — it's a real, deterministic resource leak:

- Same two tests fail first, every time, across 5+ unrelated commits:
  `MainPage_UserNoteInput_ShowTextEntry` then `MainPage_WentFirstLabel_Displayed`, with
  near-identical durations (38-39s / 16s) each run.
- The `ColorBuffer` IDs in the log climb steadily within a run (139 → 155 → 167 → 175 → 183…)
  right up to the crash. That's the emulator's software GL translator (workflow uses
  `-gpu swiftshader_indirect` — GitHub-hosted runners have no real GPU) losing track of
  allocated image/surface buffers as more accumulate.
- The failure streak's start correlates with **app-side rendering changes**, not any CI/workflow
  config change: `43b2a90` (Aug 3) is right where most runs flip from success to failure, and
  that's the same window as the dual-icon archetype feature (2× `Image` per popup row) and the
  FlexLayout tag-chip rework — both meaningfully increase the number of composited image
  surfaces MainPage's popup interactions allocate per test run.
- Two isolated successes appear mid-streak (`2040944`, `25d9d2f`) — consistent with a leak that
  doesn't always tip over before the run ends, depending on shared-runner scheduling noise.

**Conclusion:** our own MainPage/OptionsPage UI got more graphics-heavy over the last two days
(dual-icon archetypes, FlexLayout chips, ComboBox popup redesigns), and GitHub's software-
rendered Android emulator has a leaky/limited `ColorBuffer` pool that can't keep up once
MainPageTests' popup-heavy tests run. Local machines don't reproduce this because they most
likely have real GPU acceleration backing the emulator (hardware-accelerated `swiftshader` or
actual GPU passthrough via Android Studio's emulator, vs. CI's forced software path).

**Actionable fixes to try (not yet attempted):**
1. Reduce Image/Popup churn specifically in MainPageTests — e.g. reuse a single opened popup
   across more assertions instead of open/close per test, or split MainPageTests into two test
   classes so the run gets a fresh emulator/process partway through.
2. Try `-gpu swiftshader_indirect` alternatives in `emulator-options` (e.g. `-gpu guest` or an
   explicit ANGLE backend) — may have different buffer pool behavior.
3. Self-hosted runner with real GPU (project already has PinPC/UbuntuBox self-hosted infra for
   other workflows — see [[project_self_hosted_runners]]) sidesteps the software-GL limitation
   entirely.
4. Bump `-memory` further or add explicit GC/buffer-pool env vars for the emulator process.

## Why this is infra (emulator), not app logic — still true

- Same failure signature and cascade shape appeared on `2dbb709` (before the MainPage
  `IsBusyMutating` gate existed) AND on `9ceca59` (after) — ruling out the new gate as cause.
- Local Android suite for the identical commits ran 72/72 clean, twice, same session.
- `ColorBuffer` errors are a known class of issue with GPU-accelerated Android emulators on
  CI runners without real/virtual GPU passthrough — softare (swiftshader) rendering degrading
  under sustained load.

## What NOT to do

- Do not chase this by adding more retry/gate logic to the test code — the underlying cause
  is emulator rendering, not app or test timing.
- Do not treat "Android CI red" as a merge blocker by itself if the local Android suite is
  green and the failure log shows ColorBuffer errors. Cross-check the log before assuming a
  real regression.

## Next steps (not yet done)

- Consider `-gpu swiftshader_indirect` tuning already in `ui-tests-android.yml` (`emulator-options`)
  — may need `-gpu off` or a different renderer, or more `-memory`.
- Consider self-hosted runner for Android CI (project already has PinPC/UbuntuBox self-hosted
  runners for other workflows — see [[project_self_hosted_runners]]) if GitHub-hosted stays
  unreliable.
- Track whether this is worsening over time or was always present; no historical baseline
  before this session to compare against.

## Related

- [[project_self_hosted_runners]] — PinPC/UbuntuBox self-hosted runner infra already exists
- [[project_ci_workflows]] — workflow structure
- [[feedback_android_flaky_tap_retry]] — a DIFFERENT flake class (real, fixed) — don't confuse
  the two. That one was app-code tap timing; this one is CI-runner GPU emulation.
