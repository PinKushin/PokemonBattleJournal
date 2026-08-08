# Security Policy

## Reporting a vulnerability

Report privately through GitHub's [Security Advisories](https://github.com/PinKushin/PokemonBattleJournal/security/advisories/new)
rather than opening a public issue.

This is a hobby project maintained by one person. There is no guaranteed
response time and no bug bounty. Reports are read and taken seriously, but
expect days rather than hours.

## Supported versions

The `master` branch only. There are no released builds, no versioned branches,
and no backports.

## What this app does with your data

**Nothing leaves your device except crash reports, and those are filtered.**
That is a deliberate design constraint rather than a current limitation:

- **No accounts, no server, no cloud sync, no analytics.** There is no backend
  to breach. None is planned.
- **All data is a local SQLite file** in the app's own data directory —
  matches, trainer names, deck archetypes, tags and notes.
- **Export and backup files are written where you choose them** and are plain
  JSON. They contain the trainer and deck names you typed. Treat a backup file
  the way you would treat any document you wrote.

### Crash reporting

Sentry is enabled for crash reports. The Serilog sink is wrapped by
`SentryRedactingSink`, which forwards log property values **by type**: numbers,
booleans, enums, `DateTime`, `Guid`. Strings and destructured objects are
withheld and replaced with `[redacted]`, with two exceptions that are written by
the app rather than by a person (`ValidationMessage` and `Problem`).

The practical consequence for contributors: **log ids, counts and lengths — not
names or paths.** A name in a log template still reaches the local log file, but
arrives at Sentry as `[redacted]`, which is a worse crash report than the id
would have been. Do not widen the allowlist to get a string through; use an
enum or an id.

## Scope

In scope:

- Anything that sends user content off the device
- SQL injection, path traversal, or command injection
- Unsafe deserialization of an imported or restored file — the TrainerHill
  import and the backup restore both parse untrusted JSON
- Dependency vulnerabilities with a practical path to exploitation here

Out of scope:

- Anything requiring physical access to an unlocked device. Local data is not
  encrypted at rest, by design — the threat model is a personal match journal,
  not a secrets store.
- Attacks that require the user to deliberately import a hostile file *and*
  where the impact is limited to their own local database
- Missing hardening on a build that is unsigned by design (there is no
  code-signing certificate for this project)

## Automated security checks

These run in CI and are worth knowing about before reporting something a tool
already covers:

- **CodeQL** — `csharp` and `actions`, on push, PR and weekly
  ([.github/workflows/codeql.yml](.github/workflows/codeql.yml))
- **Dependabot** — dependency updates
- **Fuzzing** — SharpFuzz + libFuzzer against the note-diff, conflict-merge and
  log-redaction paths, weekly
  ([.github/workflows/fuzz.yml](.github/workflows/fuzz.yml))
- **Mutation testing** — Stryker.NET over `PokemonBattleJournal.Core`, run
  manually rather than in CI

## Note on pinned dependencies

`SQLitePCLRaw.lib.e_sqlite3` (3.53.3) and `SQLitePCLRaw.lib.e_sqlite3.android`
(2.1.12) carry explicit minimum versions that override the 2.1.11 that
`bundle_green` would otherwise resolve transitively. They look like removable duplication and are not: they exist to pull
the transitive SQLite past
[GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).
Removing them reintroduces the advisory.
