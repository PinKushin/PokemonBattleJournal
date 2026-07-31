---
name: feedback_security
description: "Never introduce security vulnerabilities — SQL injection, XSS, command injection, path traversal, insecure deserialization, sensitive data exposure. Applies to all projects."
metadata:
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-27T20:39:00.258Z
---

Never introduce vulnerabilities. This project has SQL (SQLite), JSON import (scraper output), and future HTTP clients — all are injection vectors.

**Why:** User explicitly required security as a permanent engineering standard after discussing SQL, JSON import/export, and web scraping work.

**How to apply:** Before marking any task done that touches SQL, file I/O, HTTP clients, deserialization, or user-supplied data, confirm no injection vector was introduced.

## Rules

- **SQL injection:** Parameterized queries only. Never interpolate user input into SQL strings. `MatchOperations`, `TrainerOperations`, etc. must use SQLite-net's parameterized API.
- **XSS:** If any web-facing view is added, encode user content before rendering. Never use `innerHTML` with untrusted data.
- **Command injection:** Never pass user input to `Process.Start` argument strings, shell commands, or eval-style calls. Validate and allowlist external input used in system calls.
- **Insecure deserialization:** JSON import must validate and constrain all fields before touching DB. Reject unknown structure; coerce types explicitly (e.g., `turn` as int OR string → `uint`).
- **Sensitive data exposure:** No secrets, API keys, tokens, or PII in source code, logs, or error messages.
- **Path traversal:** Sanitize all file paths from user input — `Path.GetFullPath` + prefix check before any file read/write.
- **Scraper/HTTP responses:** Treat all external data as untrusted. Validate before parsing or storing.

[[feedback_engineering_principles]]
