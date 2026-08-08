---
name: project_sentry_three_channels
description: "SentryRedactingSink DOES cover breadcrumbs (verified: string values render as [redacted]). What it does NOT cover: span names/descriptions, and Sentry's own structured breadcrumb data. Written after I claimed a leak that did not exist."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-08T21:17:18.055Z
---

Written 2026-08-08 from a real outgoing envelope — including a correction to my own first reading
of it, which is the more useful half.

## What the sink covers, verified

**Serilog-originated breadcrumbs ARE protected.** The rendered message has its string values
replaced before it leaves:

```
"message":"Start processing HTTP request \"[redacted]\" \"[redacted]\""
```

That is a two-string template rendered with both values redacted. Six such messages in one
envelope.

## The mistake I made, because it is an easy one

The same payload contains:

```
"message":"Inserting match entry for trainer 2: Playing=9, Against=2, Result=Win"
"message":"Options loaded for trainer 2: 1 trainers, 19 archetypes, 8 tags"
```

I saw real values in a rendered breadcrumb and concluded breadcrumbs were unredacted — then
raised a leak against three `LogWarning` sites and wrote it up as fact. **Wrong.** Those values
are ints and an enum, which the sink deliberately ALLOWS: it forwards by type. Their survival is
the design working, not failing.

The test that distinguishes the two hypotheses is whether a **string** value survives. It does
not. I had that evidence in the same file and did not look for it.

Same shape as the leak test that could not fail and the presence check that changed meaning:
**I measured a proxy (does any value appear) rather than the variable (does a STRING appear).**
[[feedback_tests_that_cannot_fail]].

## What genuinely bypasses the sink

1. **Span names, descriptions and tags.** Tracing is a separate channel; `SentryRedactingSink`
   governs Serilog property values only. Defended structurally instead — `IPerformanceMonitor`
   takes constants and `ITimedSpan` exposes no string setter, so varying detail can only be
   numeric. A reflection contract test pins that shape.

2. **Sentry's OWN structured breadcrumb data**, which never passes through Serilog at all:

   ```
   "data":{"url":"https://limitlesstcg.com/decks","method":"GET","status_code":"200"}
   ```

   Benign here — a public site, no user content — but it confirms the SDK's own breadcrumbs are
   outside the sink. Anything that ever puts user data in an HTTP URL would leave this way.

3. Minor, Debug-only: stack frames carry `abs_path` with the developer's home directory. Release
   builds have no local PDB paths.

## Also verified in that envelope

Delivery works end to end: `HttpTransport: Envelope ... successfully sent`, explicit
`release: PokemonBattleJournal@1.0.0.1+1`, a session envelope with `errors:1`
(AutoSessionTracking), trace context attached. Tracing is in Sentry's FREE tier permanently —
**5M spans/month** — so a 1.0 sample rate is safe by orders of magnitude. A "14-day trial"
banner in the dashboard was an upsell ad, not a gate on tracing.

## Related

- [[project_sentry_privacy_audit]] — the 2026-08-07 audit, which got breadcrumbs RIGHT; AI-CONTEXT
  already recorded that `MinimumBreadcrumbLevel = Information` was the original leak and the sink
  was the fix. Reading that first would have prevented this.
- [[user_no_server_no_user_data]] — the constraint all of this serves
