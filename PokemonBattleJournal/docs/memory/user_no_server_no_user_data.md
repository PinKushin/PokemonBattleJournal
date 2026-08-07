---
name: user_no_server_no_user_data
description: "Hard constraint: no server, no collecting users' information. Do not propose cloud sync, accounts, telemetry or analytics as features. Stated 2026-08-07 — no hosting budget, and no interest in holding people's data."
metadata:
  type: user
---

**Stated 2026-08-07, unprompted and unambiguous:** *"i have no money to host a server, and i
really dont want peoples information, maybe if someone payed me to do it, but im not being payed
to grab peoples info so im not doing it."*

## What this rules out

Do not propose, design for, or leave hooks for:

- Cloud sync, online backup, or a hosted account system
- User accounts, logins, or anything needing a server the user pays for
- Analytics, usage telemetry, or behavioural data collection
- Any feature whose value depends on data leaving the device

It is **not** on the roadmap and should not be added to it. This is a settled decision, not a
"someday". Pairs with [[user_no_signing_budget]] — the same no-budget constraint, and the same
rule against planning around money that does not exist.

The one stated exception is being **paid to build it for someone else**, which is a different
project, not this one.

## If it ever did happen, the shape is already settled

Recorded so it is not re-derived: it would be a **separate, deliberately lossy pipeline with its
own DTOs**, not the local models persisted somewhere else. The user's reasoning — an upload
would drop most fields, so the uploaded shape is not the domain shape.

The precedent already exists in this repo: `ExportEntry` / `ExportBackup` / `ExportArchetype`
are purpose-built types for a different medium, and the TrainerHill format is intentionally
lossy. **The rule the user arrived at by practice: separate types when the shape actually
differs, not on principle.** That is also why the domain/persistence split was rejected in
[[project_core_library_extraction_plan]] — there the two shapes are identical.

## Sentry was audited against this and fixed — 2026-08-07

**Done. Do not re-open it as an open question.** Full findings in
[[project_sentry_privacy_audit]]; the short version:

Sentry's own configuration was never the leak — every PII-adjacent option defaults to false and
none was overridden. The app's own log strings were, because the Serilog sink turned every
`LogInformation` into a breadcrumb carrying the rendered message. Fixed at the call sites (ids,
counts and lengths instead of names and paths) **and** with `SentryRedactingSink`, which forwards
property values by TYPE, so free text is withheld without anyone having to decide field by field
whether it counts as PII.

All three goals held: local logs stay complete, crash reports still arrive without relying on
GitHub issues or email, and user content stops leaving the device. The fix was to stop putting it
in the strings, not to make logging quieter.
