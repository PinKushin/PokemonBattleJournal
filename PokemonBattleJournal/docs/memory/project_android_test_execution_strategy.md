---
name: project_android_test_execution_strategy
description: "Planned: run Android UI tests on CI by default, add local parallelism, and auto-target a real phone when attached (else boot an emulator). Not started — scoped 2026-08-05."
metadata:
  type: project
---

**Status: planned, not started.** User decision 2026-08-05, explicitly scoped as future work.

## Why

Android CI jobs now finish **faster than the Windows ones** (the per-fixture matrix runs 5
emulators concurrently on separate runners; see [[project_ci_workflows]]). Locally the same
72 tests run serially in one session and take **8m55s**, versus a Windows local suite of
73 tests in **1m28s** ([[project_game3tab_ci_flake_recurring]]).

User's read: their machine is more powerful than a GitHub Ubuntu runner, but it pays for
running the Android emulator on Windows. So Android's local disadvantage is the execution
environment, not the hardware.

## Decisions

1. **Default to CI for Android UI tests** rather than running the full local suite on every
   change. Keep local runs for targeted debugging (`--filter`) rather than full sweeps.
2. **Investigate local parallelism** to match what CI gets from the matrix.
3. **Auto-target a real device.** User wants to test on their actual phone. The selection
   should be automatic or trivially switchable: if a physical device is attached, run there;
   otherwise boot the AVD. An env var override (in the shape of the existing
   `ANDROID_USE_INSTALLED`, see [[feedback_android_local_testing]]) plus `adb devices`
   detection is the obvious form.

## Known constraints to design around (not yet validated)

- **Local parallelism needs more than a `dotnet test` flag.** `AppiumSetup` is a single
  `[SetUpFixture]` owning one driver, one Appium server port, and one emulator. Running
  fixtures concurrently needs distinct AVDs, distinct adb/Appium ports, and a distinct app
  data/DB per instance — otherwise the instances fight over the same `.db3` and produce
  exactly the cross-contamination the CI matrix was built to avoid.
- **WSL2 (Ubuntu is installed).** Running the emulator inside WSL2 requires nested
  virtualization and KVM access; WSL2 is itself a VM, so this is the part most likely not to
  work cheaply. Worth a spike before committing to it. Note the emulator would then contend
  with Windows' Hyper-V for the same hardware, so it is not obviously a win over just running
  more AVDs natively.
- **Real device over USB.** Straightforward from Windows (`adb devices` sees it directly).
  From WSL2 it needs `usbipd-win` to forward the USB device — another reason the phone path
  and the WSL path should be evaluated separately, not bundled.
- **`ANDROID_USE_INSTALLED=1` semantics carry over.** A real phone must have the app deployed
  first; never `pm clear` a Fast-Deployment install ([[project_android_pm_clear]]).
- **A real phone is not a clean room.** The DB persists between runs exactly as the emulator's
  does ([[project_android_seeder_persistent_db]]), and the seeder's count check matters more
  there, not less.

## Related

- [[project_ci_workflows]] — the per-fixture matrix that made CI fast
- [[feedback_android_local_testing]] — ANDROID_USE_INSTALLED=1 local workflow
- [[project_android_pm_clear]] — why pm clear is unsafe on a VS-deployed app
- [[project_roadmap]] — carries this as a roadmap item
