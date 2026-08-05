---
name: feedback_android_local_testing
description: "Always use ANDROID_USE_INSTALLED=1 locally — never trigger AppiumSetup's full EmbedAssembliesIntoApk build"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-05T21:12:46.026Z
---

For local Android UI test runs, always set `ANDROID_USE_INSTALLED=1`. Build and deploy once from VS (Fast Deployment), then run tests without rebuilding.

**Why:** AppiumSetup's internal build uses `EmbedAssembliesIntoApk=true` which takes 7+ minutes. VS Fast Deployment pushes only changed DLLs and takes seconds. The full embedded build is CI-only — it's needed there because `pm clear` is used for a clean seed state, which requires assemblies to be in the APK.

**Local workflow:**
1. Build + deploy from VS once (Fast Deployment)
2. `$env:ANDROID_USE_INSTALLED="1"; dotnet test PokemonBattleJournal.UITests/UITests.Android/UITests.Android.csproj`
3. Re-run tests as many times as needed — no rebuild unless code changed
4. When code changes, deploy again from VS, then re-run tests

**How to apply:** Never run Android UI tests locally without `ANDROID_USE_INSTALLED=1`. Never suggest or trigger the AppiumSetup build path locally. CI is the only place the full embedded build makes sense.

## Step 4 from the CLI — Claude cannot drive VS (added 2026-08-05)

`ANDROID_USE_INSTALLED=1` runs against **whatever APK is already on the emulator**. If app
code changed and the deploy was skipped, the suite tests the *old* build and passes —
a green result that validates nothing. There is no warning; this fails silently.

Nearly shipped that way while verifying the DbSession change
([[project_db_session_lock_pairing]]): app code changed, `ANDROID_USE_INSTALLED=1` was
launched out of habit, and the run would have exercised the previous build's operations
services. Caught before the result was used.

The CLI equivalent of "deploy from VS", which takes ~40s rather than 7 minutes:

```bash
dotnet build PokemonBattleJournal/PokemonBattleJournal.csproj -f net10.0-android -t:Install
```

Requires a booted device. `AppiumSetup.EnsureEmulatorRunning()` normally handles that, but
`-t:Install` runs outside the test host, so boot it first:

```bash
"$ANDROID_HOME/emulator/emulator.exe" -avd pixel_7_-_api_35     # SDK is at "C:\Program Files (x86)\Android\android-sdk"
adb wait-for-device && adb shell getprop sys.boot_completed      # poll until "1"
```

Verify Fast Deployment survived — `adb shell run-as com.PinKushin.PokemonBattleJournal ls
files/` must still list `.__override__/`. If that directory is gone, something ran
`pm clear` and the app will crash on launch ([[project_android_pm_clear]]).

**Decision rule:** app code touched since the last deploy → deploy, then test. Only test
code touched → `ANDROID_USE_INSTALLED=1` straight away.
