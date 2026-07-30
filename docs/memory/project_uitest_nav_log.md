---
name: project_uitest_nav_log
description: NavigateTo writes a diagnostic log to %TEMP%\UITests.NavLog.txt — read after VS test runs to debug cascade failures
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:43:50.783Z
---

`BaseTest.NavigateTo` logs every navigation attempt to `%TEMP%\UITests.NavLog.txt`.

Format:
- `NAV   [TestMethod] 'currentPage' -> 'targetPage'` — navigation attempted
- `OK    [TestMethod] now on 'targetPage'` — navigation succeeded
- `SKIP  [TestMethod] already on 'targetPage'` — skipped (already there)
- `FAIL  [TestMethod] navigating to 'targetPage': ExceptionType: message` — navigation failed

Log is reset at the start of each test run by `AppiumSetup` (both Windows and Android).

**How to apply:** When Android/Windows tests cascade and VS test output is unavailable, read this log immediately after the run. A cascade shows as: one `OK` entry then silence — meaning subsequent tests failed before calling NavigateTo (they have no NavigateTo call). A bad navigation shows as a `FAIL` entry.

`MoveNext` as the caller name = async test method (state machine); that's normal.
