---
name: project_ci_android_build_fixes
description: "Linux CI build failures for Android — AppIcon path case, iOS TFM exclusion, EmbedAssembliesIntoApk"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-29T18:56:03.752Z
---

Two root causes broke Android CI builds repeatedly.

**1. AppIcon path case sensitivity**
csproj had `Resources\Appicon\appicon.svg` (lowercase 'i') but actual folder is `Resources\AppIcon\` (capital 'I'). Windows ignores case; Linux CI fails. Fixed to `Resources\AppIcon\appicon.svg` in the `<MauiIcon>` element.

**2. iOS/macOS TFMs on Linux**
The `.csproj` lists `net10.0-ios` and `net10.0-maccatalyst` in `<TargetFrameworks>`. MSBuild validates ALL TFM workload packs even when building only `-f net10.0-android`. iOS/macOS SDK packs are unavailable on Linux runners → NETSDK1178. Fix: csproj condition:
```xml
<TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('linux'))">net10.0-android;net10.0</TargetFrameworks>
```
Do NOT use `-p:TargetFrameworks=net10.0-android` in the build command — it propagates to project references (Scraper) and breaks their assets.json lookup (NETSDK1005).

**3. EmbedAssembliesIntoApk**
Required for Appium Android (Appium only does `adb install`, never pushes fast-deploy assemblies). Pass only at build time via AppiumSetup.cs, not in `.csproj`. Allows `pm clear` for data wipe between runs.

**Why:** Linux CI is case-sensitive; MSBuild multi-TFM validation is greedy.
**How to apply:** Any new icon/image resource paths in csproj must use exact case matching the filesystem. Never add iOS/macOS TFMs without the Linux condition.
