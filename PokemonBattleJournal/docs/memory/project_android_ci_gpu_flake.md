---
name: project_android_ci_gpu_flake
description: RESOLVED — Android CI GPU ColorBuffer errors were caused by a duplicate emulator process from an AVD name mismatch between the workflow and AppiumSetup.cs, not app graphics complexity or runner flakiness
metadata:
  type: project
---

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
