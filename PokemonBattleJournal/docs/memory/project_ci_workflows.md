---
name: project_ci_workflows
description: "CI split into 3 separate workflows — ci.yml (unit+integration+coverage), ui-tests-windows.yml, ui-tests-android.yml"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:44:02.672Z
---

Three independent GitHub Actions workflows:

| File | Trigger | What it does |
|------|---------|--------------|
| `ci.yml` | push/PR | Unit tests + integration tests, both with XPlat Code Coverage → ReportGenerator HTML + GitHub step summary |
| `ui-tests-windows.yml` | push/PR | Windows UI tests (WinAppDriver, windows-latest) |
| `ui-tests-android.yml` | push/PR | Android UI tests (UIAutomator2, ubuntu-latest, API 34, pixel_7 profile, avd-name `pixel_7_-_api_34`) |

**Why separated:** Windows and Android UI test jobs were combined in one `ui-tests.yml`. A failing Android job marked the whole workflow failed, hiding the Windows pass/fail status. Now each has its own badge and notification.

**Android CI AVD:** `avd-name: pixel_7_-_api_34` (API 34 default image) on CI. Local AVD is `pixel_7_-_api_35` (API 35). These differ intentionally — CI uses `default` target (API 34) because API 35 `google_apis` target crashes on 2-core runners.

**Coverage:** Unit and integration tests produce Cobertura XML → ReportGenerator publishes HTML artifact + Markdown summary inline on the CI run page.

**Android emulator:** Always shut down after tests (local + CI). `ShutdownEmulator()` called unconditionally in `AppiumSetup.Dispose()`.
