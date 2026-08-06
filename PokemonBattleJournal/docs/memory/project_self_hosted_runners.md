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

## History — how it was configured when it was self-hosted

Kept because the same problems return if this is ever revisited. Not current.

- **PinPC** — Windows 11 dev machine; runner at `C:\Users\pinku\actions-runner`, started via
  `.\run.cmd` or as a service. **UbuntuBox** — WSL Ubuntu, registered separately.
- Original rationale: GitHub-hosted runners were too slow for Appium UI tests and local
  runners already had the dev environment. Both halves of that have since changed —
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
