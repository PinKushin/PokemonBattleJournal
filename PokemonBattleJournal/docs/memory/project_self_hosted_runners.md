---
name: self-hosted-runners
description: "SUPERSEDED — CI is on GitHub-hosted runners (windows-latest / ubuntu-latest). Self-hosted setup kept only as history; do not reinstate it from this file."
metadata:
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-06T00:00:00.000Z
---

## Do not reinstate these. The reason is security, not flakiness.

**This repository is public, and both UI workflows trigger on `pull_request`.** A self-hosted
runner therefore executes workflow code proposed by anyone who opens a pull request, on the
developer's own Windows machine, under their own user account — with their SSH keys, tokens,
browser profiles and source tree all reachable. This is a documented attack path against
self-hosted runners on public repos, not a hypothetical.

The Windows runner is the dangerous one: it ran natively as the logged-in user. The WSL runner
at least sits behind a VM boundary.

Everything below about flakiness and outage-resistance is secondary. Even if the runners were
fast and reliable, this alone rules them out for a public repo. Any future "let's just run CI
locally" idea should be a script that the developer runs deliberately — `build/ci-local.ps1` —
never a daemon that executes whatever arrives from the internet.

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

**No runs created at all** is a third state, distinct from both "failed" and "queued". During
the same incident GitHub throttled webhook delivery to ~15%, so pushes simply did not trigger
workflows — a commit sat on the remote with `total_count: 0` runs against its SHA. Check
`gh api "repos/OWNER/REPO/actions/runs?head_sha=<sha>"` before concluding anything about a
workflow file; an absent run is not a failing one. githubstatus.com settles it in seconds, and
subscribing to Actions incidents there turns this from an hour of debugging into a
notification.

## Why they existed, and why the approach failed

They were never meant to be the permanent CI substrate. They were a way to **reproduce CI
failures semi-locally** — run the workflows on hardware that could be inspected and iterated
on, instead of pushing a commit per hypothesis.

**It did not achieve that**, and it was retired. One physical machine hosted both runners —
the Windows runner natively, the Linux runner in WSL — so unlike a local test-runner session
the Windows UI matrix and the Android UI matrix could run **genuinely in parallel on one
desktop**.

**Interference is real, and it runs the OTHER way.** Measured 2026-08-06 by running both
suites concurrently by hand — see [[project_android_session_poisoning]] for the numbers and
the control.

The recollection was that Android broke Windows, reasoning that Windows Appium needs its
window frontmost while the emulator takes the foreground. The measurement says the reverse:
**Windows passed 80/80 and was not measurably slowed** (`Shell ready` 244ms while the emulator
cold-booted on the same CPU), while **Android failed 79/79**. The focus-sensitive suite was
fine; the emulator-based one collapsed.

So the runners almost certainly did interfere — just not in the direction anyone assumed, and
a Windows job would have been the aggressor rather than the victim.

The plainer problem is not in doubt: **a CI run on the machine you are sitting at is a run you
have to leave alone**, and these runners made that hard to sustain. That alone undercuts the
"reproduce CI locally" premise regardless of whether the focus theory holds.

What is **not** in doubt is the narrower fact underneath it: manual mouse or keyboard input
during a local Windows run can divert a click. Observed directly on 2026-08-06, when a mouse
movement made an About-page click miss and clicking the page by hand let the run continue.
The user also notes this "wasn't always the case" — something made the suite more sensitive
to stray input than it used to be, and nobody has looked into what. That is an open thread,
not a known cause.

**Why the parallel case does not arise locally anyway:** the VS test runner serialises. It
starts the Android emulator at the beginning of a session, waits for it to be ready, and runs
the Android fixtures only after everything else has finished — so the Windows suite is always
done before Android begins. Two separate CI runners had no such coordination.

See [[project_windows_mainpage_click_flake]] for where foreground and pointer state sit in
that investigation — a real mechanism for a dispatched click doing nothing, and still not the
CI cause.

## History — how it was configured

Kept because the same problems return if this is ever revisited. Not current.

- Two runners, both still registered and configured as of 2026-08-06 — stopping them is all
  that was ever done. Neither is installed as a service, so both are manual foreground starts:

  | Runner | Location | Start |
  |---|---|---|
  | `windows-box` | `C:\actions-runner` | `C:\actions-runner\run.cmd` |
  | `UbuntuBox` | `~/actions-runner` in WSL | `wsl -e bash -lc "cd ~/actions-runner && ./run.sh"` |

  The Windows path is **`C:\actions-runner`**, not the `C:\Users\pinku\actions-runner` this
  file used to claim, and the agent name is `windows-box`, not `PinPC`. Read `.runner` in the
  install directory for the registered name rather than trusting either.
- On speed: hosted runners are no longer the slower option for Android anyway —
  see [[project_android_test_execution_strategy]].
- Registered with auto-assigned labels only, so workflows had to say
  `[self-hosted, Windows, X64]` / `[self-hosted, Linux, X64]`. Custom labels (`PinPC`) needed
  re-registration with `--labels`.
- `actions/setup-dotnet` fails on them: it writes to `C:\Program Files\dotnet` or a system
  path, both admin-only. The fix is `DOTNET_INSTALL_DIR` / `DOTNET_ROOT` pointed at
  `${{ runner.temp }}` — **at step level**, on the `setup-dotnet` step itself:

  ```yaml
  - uses: actions/setup-dotnet@v6
    env:
      DOTNET_INSTALL_DIR: ${{ runner.temp }}/.dotnet
      DOTNET_ROOT: ${{ runner.temp }}/.dotnet
    with:
      dotnet-version: '10.x'
  ```

  **This file previously said job-level, and that is wrong** — it cost three dead workflow
  runs on 2026-08-06. The `runner` context is not among the contexts allowed in
  `jobs.<id>.env`; that list is github, needs, strategy, matrix, vars, secrets, inputs.
  Workflow-level is equally invalid. Only step scope works.

  **What the failure looks like, because it is genuinely misleading:** every run completes as
  `failure` with **zero jobs created**. There is no job log, and the REST API surfaces no
  message — `/runs/<id>` just says failure and `/runs/<id>/jobs` returns an empty array. It
  reads as "the runners never connected", and both runners were in fact online and idle
  throughout. Zero jobs plus no log means the workflow never parsed: look at the YAML diff,
  not at the infrastructure.

- Android on bare Ubuntu needed the SDK installed by hand (`ANDROID_HOME`, `emulator`,
  `platform-tools`, `system-images;android-35;google_apis;x86_64`) plus KVM for hardware
  acceleration. `ubuntu-latest` ships the SDK, which is part of why this is moot now.

## Related

- [[project_ci_workflows]] — workflow structure and the matrix caching rule
- [[project_android_ci_gpu_flake]] — the six real Android CI bugs, all fixed on hosted runners
