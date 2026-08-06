---
name: self-hosted-runners
description: "SUPERSEDED — CI is on GitHub-hosted runners (windows-latest / ubuntu-latest). Self-hosted setup kept only as history; do not reinstate it from this file."
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-06T00:00:00.000Z
---

**Status 2026-08-06: no longer self-hosted.** Every workflow runs on GitHub-hosted
runners — `windows-latest` for CI, the Windows UI matrix and the scraper monitor,
`ubuntu-latest` for Android UI and Pages. Verify with `grep -rn "runs-on" .github/workflows/`
rather than trusting any prose, this file included.

Two runner registrations (`UbuntuBox`, `windows-box`) still exist in repo settings and both
report `offline`. They are leftovers, not a fallback — nothing targets their labels. An
offline self-hosted runner in the listing is therefore **not** an explanation for a red CI
run; look at the failing step instead.

## Reading a red run

`Set up job` failing is GitHub's infrastructure, not this repo — it is where actions are
resolved and downloaded, before any project code runs. During the 2026-08-06 Actions
incident it took out three of five Windows UI matrix jobs and the whole Android build while
the jobs that did start passed. A run whose only failures are `Set up job` proves nothing
about the commit.

## Why they existed, and why the approach failed

They were never meant to be the permanent CI substrate. They were a way to **reproduce CI
failures semi-locally** — run the workflows on hardware that could be inspected and iterated
on, instead of pushing a commit per hypothesis.

**It did not work, for a structural reason that will recur if anyone tries it again.** One
physical machine hosted both runners — the Windows runner natively, the Linux runner in WSL —
so the Windows UI matrix and the Android UI matrix ran **in parallel on the same desktop**.
Windows Appium tests require the app window to be in the foreground; the Android side does
not care about focus at all, but the emulator and its tooling take the foreground anyway.
The interference is therefore one-directional: Android runs break Windows runs, not the
reverse. Clicks on Windows never disturbed the emulator.

The result was Windows failures that looked like product bugs and were scheduling artifacts,
which is worse than the CI failures the setup was built to diagnose. GitHub-hosted runners
give each matrix job its own VM, so the two suites cannot see each other.

**Consequence for local work:** never run the Windows UI suite and the Android UI suite
concurrently on this machine. Same reason a mouse movement during a Windows run can divert a
click — see [[project_windows_mainpage_click_flake]], where foreground and pointer state are
a demonstrated mechanism for Windows clicks missing, though not the CI cause there.

## History — how it was configured

Kept because the same problems return if this is ever revisited. Not current.

- **PinPC** — Windows 11 dev machine; runner at `C:\Users\pinku\actions-runner`, started via
  `.\run.cmd` or as a service. **UbuntuBox** — WSL Ubuntu, registered separately.
- On speed: hosted runners are no longer the slower option for Android anyway —
  see [[project_android_test_execution_strategy]].
- Registered with auto-assigned labels only, so workflows had to say
  `[self-hosted, Windows, X64]` / `[self-hosted, Linux, X64]`. Custom labels (`PinPC`) needed
  re-registration with `--labels`.
- `actions/setup-dotnet` failed on them: it writes to `C:\Program Files\dotnet` or a system
  path, both admin-only. Fix was **job-level** env, never workflow-level — `runner.temp`
  only resolves in job/step scope, and a global `env:` using it is a workflow parse error:

  ```yaml
  jobs:
    my-job:
      env:
        DOTNET_INSTALL_DIR: ${{ runner.temp }}/.dotnet
        DOTNET_ROOT: ${{ runner.temp }}/.dotnet
  ```

- Android on bare Ubuntu needed the SDK installed by hand (`ANDROID_HOME`, `emulator`,
  `platform-tools`, `system-images;android-35;google_apis;x86_64`) plus KVM for hardware
  acceleration. `ubuntu-latest` ships the SDK, which is part of why this is moot now.

## Related

- [[project_ci_workflows]] — workflow structure and the matrix caching rule
- [[project_android_ci_gpu_flake]] — the six real Android CI bugs, all fixed on hosted runners
