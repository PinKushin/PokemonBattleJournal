---
name: self-hosted-runners
description: "Self-hosted CI runner setup for PinPC (Windows) and UbuntuBox (Linux); labels, permissions, and dotnet install quirks"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T00:59:42.400Z
---

Repo uses two self-hosted GitHub Actions runners:
- **PinPC** — Windows 11 dev machine; runner lives at `C:\Users\pinku\actions-runner`; started via `.\run.cmd` (manual) or as a service
- **UbuntuBox** — WSL Ubuntu; runner registered separately

**Why:** GitHub-hosted runners are too slow/flaky for Appium UI tests; local runners have the dev environment already present.

## Runner labels
Runners are registered with the default auto-assigned labels only: `self-hosted`, `Windows`/`Linux`, `X64`. No custom labels were set during `config.cmd`. Workflows must use these exact labels:

```yaml
runs-on: [self-hosted, Windows, X64]   # PinPC
runs-on: [self-hosted, Linux, X64]     # UbuntuBox
```

**Do NOT use custom labels** (e.g. `PinPC`, `UbuntuBox`) unless re-registering with `--labels`. Custom labels in `userLabels` field of `.runner` file; check with `Get-Content C:\Users\pinku\actions-runner\.runner | ConvertFrom-Json`.

## .NET install permission fix
`actions/setup-dotnet` fails on self-hosted runners — tries to write to `C:\Program Files\dotnet` (Windows) or a system path (Linux), both require admin. Fix: set at **job level** (not workflow level — `runner.temp` is invalid there):

```yaml
jobs:
  my-job:
    env:
      DOTNET_INSTALL_DIR: ${{ runner.temp }}/.dotnet
      DOTNET_ROOT: ${{ runner.temp }}/.dotnet
```

**Why:** `runner.temp` context expression only resolves inside job/step scope, not global `env:` block. Global `env:` with `${{ runner.* }}` causes workflow parse failure.

## Android runner (UbuntuBox)
- Needs Android SDK pre-installed (`ANDROID_HOME` set, `emulator`, `platform-tools`, `system-images;android-35;google_apis;x86_64`)
- GitHub-hosted `ubuntu-latest` ships with Android SDK; bare Ubuntu does not
- KVM must be enabled for emulator hardware acceleration
- `mkdir: Permission denied` on .NET install = same fix as above (job-level `DOTNET_INSTALL_DIR`)
