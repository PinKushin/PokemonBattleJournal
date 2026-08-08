---
name: project_sentry_three_channels
description: "SentryRedactingSink covers ONE of three channels off the device. Span names and breadcrumb messages bypass it entirely — breadcrumbs carry rendered log text unredacted, so 'log ids not names' is what protects them, not the sink."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-08T21:12:01.194Z
---

Found 2026-08-08 by reading an actual outgoing Sentry payload, not by reasoning about the code.
The 2026-08-07 audit ([[project_sentry_privacy_audit]]) examined the Serilog sink and was correct
about it — but that is one channel of three.

## The three channels

| Channel | Covered by SentryRedactingSink? | What protects it |
|---|---|---|
| Serilog property values | **YES** — forwarded by TYPE, strings withheld | the sink |
| Span names / descriptions / tags | **NO** | `IPerformanceMonitor` takes constants and `ITimedSpan` exposes no string setter — structural |
| **Breadcrumb messages** | **NO** | nothing but call-site discipline |

## The evidence

Straight from a sent envelope:

```json
{"message":"Inserting match entry for trainer 2: Playing=9, Against=2, Result=Win"}
{"message":"Options loaded for trainer 2: 1 trainers, 19 archetypes, 8 tags"}
{"message":"Active trainer loaded: 2"}
```

Note what IS redacted in the same payload — `"logger":"[redacted]"`, `"category":"[redacted]"`,
`"SourceContext":"[redacted]"` — and what is not: **the rendered message text**.

Those examples are clean, and only because every one of them logs ids and counts. That is the
CLAUDE.md rule ("log ids, counts and lengths, not names or paths") doing the work. **A single log
call carrying a trainer or deck NAME would appear in a breadcrumb verbatim, and no sink would
stop it.**

## Consequences

- The "log ids not names" rule is not a style preference or a belt-and-braces measure. For
  breadcrumbs it is the ONLY protection.
- Reviewing a new log statement means asking what it renders to, not just what type its
  properties are.
- `MaxBreadcrumbs` is 1000 in Debug and 300 in Release, so a leak persists across a long window
  of history attached to the next event.
- Sentry's OWN breadcrumbs also carry data the sink never sees, e.g.
  `"data":{"url":"https://limitlesstcg.com/decks",...}`. Benign here (a public site, no user
  content), but it confirms the SDK's breadcrumbs bypass the Serilog path entirely.

## Also verified in that payload

Delivery works end to end: `HttpTransport: Envelope ... successfully sent`, explicit
`release: PokemonBattleJournal@1.0.0.1+1`, a session envelope with `errors:1`
(AutoSessionTracking), and trace context attached. Tracing is in Sentry's FREE tier
permanently — 5M spans/month — so the 1.0 sample rate is safe by orders of magnitude. A
"14-day trial" banner seen in the dashboard was an upsell ad, not a gate on tracing.

## Related

- [[project_sentry_privacy_audit]] — the sink, and why it forwards by type
- [[user_no_server_no_user_data]] — the constraint all of this serves
