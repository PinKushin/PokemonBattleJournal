---
name: project_sqlite_security_pins
description: "SQLitePCLRaw.lib.e_sqlite3 3.53.3 and .android 2.1.12 are SECURITY overrides against GHSA-2m69-gcr7-jv3q. Removing them as duplicate-looking cruft reproduces 14x NU1903. The XA4301 warnings are unrelated and benign."
metadata:
  type: project
---

**Do not remove these two `PackageReference` lines from
`PokemonBattleJournal/PokemonBattleJournal.csproj`:**

```xml
<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3" Version="3.53.3" />
<PackageReference Include="SQLitePCLRaw.lib.e_sqlite3.android" Version="2.1.12" />
```

They look like redundant duplication next to `SQLitePCLRaw.bundle_green` 2.1.11, which
already pulls both transitively. They are not. `bundle_green` 2.1.11 resolves
`lib.e_sqlite3` **2.1.11** and `lib.e_sqlite3.android` **2.1.11**, both of which carry a
known **HIGH severity** vulnerability, [GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
The explicit references override that resolution with patched builds.

**Verified 2026-08-05 by doing exactly the wrong thing.** Removing them while chasing the
XA4301 warnings produced, immediately:

```
warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3' 2.1.11 has a known high severity vulnerability
warning NU1903: Package 'SQLitePCLRaw.lib.e_sqlite3.android' 2.1.11 has a known high severity vulnerability
```

14 occurrences across target frameworks. Reverted. The user flagged the risk before the build
finished — *"i think some of those might have been pinned because of a transient
vulnerability"* — and was right.

## They are already minimum-version, not exact pins

Worth knowing before anyone "loosens" them: in NuGet, `Version="3.53.3"` **already means
`>= 3.53.3`**. Exact pinning requires bracket syntax, `Version="[3.53.3]"`. NuGet resolves the
lowest version satisfying all constraints, so these float upward automatically if another
dependency demands newer. There is nothing to relax — asking for `>=` is what the file
already says.

## XA4301 is a separate, benign issue — do not "fix" it by touching these

```
warning XA4301: APK already contains the item lib/arm64-v8a/libe_sqlite3.so; ignoring.
warning XA4301: APK already contains the item lib/x86_64/libe_sqlite3.so; ignoring.
```

Both `lib.e_sqlite3` (via `runtimes/`) and `lib.e_sqlite3.android` (via an Android library
project/AAR, hence the `obj/Debug/net10.0-android/lp/<n>/jl/jni/<abi>/` path) ship
`libe_sqlite3.so`. Android Packaging keeps one and ignores the other.

Two things settle this:

1. **It is not caused by the pins.** The warning appears identically with the transitive
   2.1.11 versions — removing the pins did not clear it. Whatever fixes XA4301, it is not
   dropping these packages.
2. **No security impact either way.** Both candidate `.so` files come from patched versions
   (3.53.3 and 2.1.12), so whichever one wins, the APK ships a patched SQLite.

So XA4301 is cosmetic. Fixing it properly means excluding one package's native asset for the
Android TFM, not removing a package. Left alone as of 2026-08-05 — 2 warnings in a
full-solution build, alongside 4 pre-existing iOS/MacCatalyst `S1118`/`RCS1102` warnings.

## Related

- [[feedback_security]] — the standing rule this protects
- [[feedback_test_the_hypothesis_first]] — the removal was a hypothesis that the build
  falsified in one step; the warning count going 6 → 8 was the tell
