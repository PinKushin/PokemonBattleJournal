---
name: project_sentry_privacy_audit
description: "DONE 2026-08-07. Sentry's own defaults were never the leak — the Serilog sink was, shipping rendered log messages off-device. Fixed at the call sites AND with SentryRedactingSink, which forwards values by TYPE so a future log line is safe by default."
metadata:
  type: project
---

**Audited and fixed 2026-08-07** ([[user_no_server_no_user_data]] is the standard it was measured
against). Do not re-derive this; the conclusions below were checked against the 6.8.0 package
docs and a payload dump, not recalled.

## Sentry's configuration was clean — the app was the leak

Every option that could have leaked by default is false by default and none is overridden in
`MauiProgram`: `SendDefaultPii`, `IncludeTextInBreadcrumbs`, `IncludeTitleInBreadcrumbs`,
`IncludeBackgroundingStateInBreadcrumbs`, `AttachScreenshot`. **Nothing the SDK does on its own
sends user data.** Notably `IncludeTextInBreadcrumbs=false` is what kept `Editor` contents —
match notes — out of breadcrumbs.

What sent it was the Serilog sink. `MinimumBreadcrumbLevel = Information` turned every
`LogInformation` into a breadcrumb carrying the **rendered** message, and
`MinimumEventLevel = Error` shipped the last 300 of those with every error event. So trainer
names, tag text, deck names and the full export path — which embeds the OS account name — all
left the device.

The DSN being hardcoded in public source is **not** a finding. A DSN is a public client
credential by design; the worst case is quota abuse.

## The fix is two layers, and both are load-bearing

1. **Call sites** log ids where an id exists, **lengths** where one does not (a failed save has
   no id yet, and the length is the property that actually explains a rejection — empty, or long
   enough to hit a column limit), counts instead of `{@X}`, and an `ExportFormat` enum instead of
   the file path.
2. **`Logging/SentryRedactingSink`** decorates the Sentry sink and forwards a **copy** of each
   event carrying only values whose **TYPE** cannot express user content: numbers, bools, enums,
   `DateTime`, `Guid`, null. Strings and destructured objects are withheld unless the property
   name is on a short allowlist of app-authored text (`ValidationMessage`, `Problem`).

Why both: layer 1 depends on discipline and a log line written in two years will not have read
the comment. Layer 2 alone would leave the content in the strings, one deleted filter from
shipping. Wired once via `.WriteTo.RedactedSentry()`; the tests call the same extension, so they
cannot drift from production.

**It builds a new `LogEvent` rather than editing the one it is given.** Serilog hands the same
instance to every sink, so mutating it would redact the local file log too, with the outcome
depending on sink order.

## Type-based, so no field ever has to be adjudicated

The user's read, which is right: an event name, a deck name and most tags are not really PII on
their own — thousands attend a regional. The rule deliberately does not care. **A number cannot
hold a sentence; a string can hold anything.** Free text is unbounded, so it is withheld without
anyone deciding what its contents might be. That is why the planned online/in-person + event name
feature (F-30) needs no new privacy work.

## Where content is KEPT on purpose

Import and restore error lists name entries from the user's own file. A count cannot diagnose
"2 failed", so those stay complete on the device and are withheld on the way out. **That split is
the reason for having two layers rather than one.** `TrainerHillImportService` and
`OptionsPageViewModel` both carry a comment saying so — they are not oversights.

## Accepted residue

- `LogError(ex, …)` forwards the **exception itself**, and SQLite can quote an offending value in
  a constraint message. Rewriting exception text would destroy what a crash report is for.
- Stack frames carry the **developer's** absolute source paths from the PDB. Not user data; the
  leak test must not assert on a bare `C:\Users` prefix because of it.
- Sentry assigns a stable installation id (`user.id` in the payload), so events correlate per
  device. Nothing identifies a person, but the events are a per-install series, not isolated.

## The near miss, worth remembering

`MatchOperations` logged `{@MatchEntry}`, which walked every navigation property. It did **not**
carry match notes — but only because `Game1/2/3` are still null at that line, assigned after the
insert. Populating them earlier, or one `LogDebug("{@Game}")`, would have shipped the user's
free-text notes with nothing in the way.

## The test that could not fail

The first leak test searched the serialized JSON **bytes** for `Ash's Pikachu Deck`.
`Utf8JsonWriter` defaults to `JavaScriptEncoder.Default`, which escapes an apostrophe to
`\u0027` — so the value was fully present in the payload and absent as a literal substring, and
the test passed on the leak it was written to catch. `SentryPayloadPiiTests` now parses the
document and walks it, asserting on **decoded content, never on an encoding**. Shape #3 from
[[feedback_tests_that_cannot_fail]], found in a test ten minutes old.

The fixture also pins the other direction on purpose: one test asserts counts and ids **still**
arrive, one asserts a redacted breadcrumb still names the code path, and one pins the allowlist
from both sides in a single call — so a misspelled allowlist entry cannot silently delete the
reason from every crash report.

## Related

- [[user_no_server_no_user_data]] — the constraint this was audited against
- [[feedback_security]] — no PII in logs or error messages; this is where that got tested
- [[feedback_no_silent_guards]] — guards still say WHY; they now say it with a length
