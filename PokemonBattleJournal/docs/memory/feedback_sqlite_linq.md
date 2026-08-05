---
name: feedback_sqlite_linq
description: "New SQLite queries should use the SQLite-net LINQ API (Table<T>().Where(...)) rather than raw SQL strings"
metadata:
  node_type: memory
  type: feedback
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-04T16:48:06.719Z
---

New DB queries should use the SQLite-net-pcl LINQ API (`connection.Table<T>().Where(...).ToListAsync()`) rather than raw SQL strings.

**Why:** User prefers LINQ and asked that new queries use it when appropriate (2026-08-04). Existing raw SQL is acceptable as-is — converting it is a separate task.

**How to apply:** When writing new `*Operations` methods, prefer `Table<Tags>().Where(t => t.TrainerId == id).ToListAsync()` over `QueryAsync<Tags>("SELECT * FROM Tags WHERE TrainerId = ?", id)`. Raw SQL is still fine for complex joins or CTEs where LINQ would be less clear.
