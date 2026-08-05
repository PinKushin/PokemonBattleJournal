---
name: project_android_pm_clear
description: pm clear crashes VS fast-deploy builds — use force-stop + DB delete instead; pm clear only safe for EmbedAssembliesIntoApk builds
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T08:26:16.247Z
---

`pm clear` wipes the entire app data directory including `files/.__override__/x86_64/` where VS Fast Deployment stores the managed assemblies. After `pm clear`, the app crashes on next launch: "No assemblies found in '.../__override__/x86_64'. Assuming this is part of Fast Deployment. Exiting..."

**When pm clear is safe:** Only when the APK was built with `EmbedAssembliesIntoApk=true` — assemblies are inside the APK itself, not in app data. This is the AppiumSetup CI build path and the `dotnet build -p:EmbedAssembliesIntoApk=true` path.

**When pm clear is NOT safe:** When `ANDROID_USE_INSTALLED=1` (VS-deployed app). VS uses Fast Deployment — DLLs pushed separately to `.__override__/`, not in the APK.

**Correct wipe for VS-deployed path:**
```csharp
RunAdb($"shell am force-stop {AppPackage}", timeoutMs: 5_000);
RunAdb($"shell rm -f /data/data/{AppPackage}/files/*.db3", timeoutMs: 5_000);
```
This stops the app and deletes only the SQLite DB for a clean seed state, leaving `.__override__/` intact.

**How to apply:** AppiumSetup already branches on `useInstalled`. Never move `pm clear` outside that branch or make it unconditional.
