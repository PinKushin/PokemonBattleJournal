---
name: project_ci_workflows
description: "CI split into 3 separate workflows — ci.yml (unit+integration+coverage), ui-tests-windows.yml, ui-tests-android.yml. UI test workflows now matrix per test-fixture-class."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-05T00:00:00.000Z
---

Three independent GitHub Actions workflows:

| File | Trigger | What it does |
|------|---------|--------------|
| `ci.yml` | push/PR | Unit tests + integration tests, both with XPlat Code Coverage → ReportGenerator HTML + GitHub step summary |
| `ui-tests-windows.yml` | push/PR | Windows UI tests (WinAppDriver, windows-latest), matrixed per fixture class |
| `ui-tests-android.yml` | push/PR | Android UI tests (UIAutomator2, ubuntu-latest, API 35, pixel_7 profile, avd-name `pixel_7_-_api_35`), matrixed per fixture class |

**Why separated:** Windows and Android UI test jobs were combined in one `ui-tests.yml`. A failing Android job marked the whole workflow failed, hiding the Windows pass/fail status. Now each has its own badge and notification.

**Android CI AVD (corrected 2026-08-05):** both workflow and `AppiumSetup.cs`'s hardcoded
`AvdName` constant now use `pixel_7_-_api_35` / API 35 `default` target — they MUST match.
The previous version of this doc claimed the CI/local mismatch (api-34 vs api-35) was
intentional ("API 35 google_apis crashes on 2-core runners") — that was wrong. The mismatch
was an undiscovered bug: `EnsureEmulatorRunning()` compares the running AVD's name against
the `AvdName` constant, and when they didn't match it launched a SECOND emulator process
(targeting an AVD that didn't even exist on CI), which contended for KVM/GPU resources and
produced `Failed to find ColorBuffer` errors that looked like a graphics/GPU flake. Fixed
in `c3184c2` by aligning both to API 35. See [[project_android_ci_gpu_flake]] for the full
investigation, including a SECOND real bug found after this one (see below).

**UI test matrix split (2026-08-05):** both `ui-tests-windows.yml` and
`ui-tests-android.yml` use `strategy.matrix.fixture` over the 5 test-fixture classes
(`AboutPageTests`, `MainPageTests`, `OptionsPageTests`, `ReadJournalPageTests`,
`TrainerPageTests`), each running as its own job with `--filter "FullyQualifiedName~<fixture>"`.
Root cause: `AppiumSetup.cs` (both platforms) uses one `[SetUpFixture]` driver session for
the whole ~72-test assembly, never recycled — long-lived Appium sessions degrade over the
run (element-cache growth, climbing per-call latency) until `FindElement` calls fail
outright. Confirmed on BOTH Windows (real GPU hardware) and Android (emulator) with an
identical symptom shape, which is what ruled out GPU/graphics-churn as the actual cause.
Matrix jobs run concurrently and each gets a fresh driver, bounding the degradation window
to one fixture's worth of tests. `fail-fast: false` so one fixture's failure doesn't cancel
the others. Cost: ~5x the build/boot compute (fresh APK build + emulator boot per job) —
accepted tradeoff since GitHub-hosted job concurrency means wall time stays flat rather than
multiplying. See [[project_android_ci_gpu_flake]] for the evidence trail.

**Coverage:** Unit and integration tests produce Cobertura XML → ReportGenerator publishes HTML artifact + Markdown summary inline on the CI run page.

**Android emulator:** Always shut down after tests (local + CI). `ShutdownEmulator()` called unconditionally in `AppiumSetup.Dispose()`. Android job also has `timeout-minutes: 40` to prevent a hung emulator teardown from blocking artifact upload indefinitely (observed once: test run completed but `reactivecircus/android-emulator-runner`'s own teardown hung on `- waiting for device -` after the emulator process was already gone).
