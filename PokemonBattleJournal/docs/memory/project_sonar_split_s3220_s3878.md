---
name: project_sonar_split_s3220_s3878
description: string.Split with two char args triggers conflicting Sonar warnings S3220 and S3878; fix is explicit non-params overload
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T20:52:52.327Z
---

`string.Split` with two char literals triggers a Sonar catch-22:
- `Split('a', 'b')` → **S3220**: "partially matches overload without params" (`Split(char, int, StringSplitOptions)`)
- `Split(['a', 'b'])` → **S3878**: "remove array creation and pass elements directly" (collection expression passed to params)
- `Split(new char[] { 'a', 'b' })` → also **S3878**

**Fix:** Use the explicit `char[]` overload with `StringSplitOptions`:
```csharp
deckName.Split(['&', '/'], StringSplitOptions.None)[0]
```
This calls `Split(char[] separator, StringSplitOptions options)` which is NOT a params method, so neither S3878 nor S3220 fires.

**How to apply:** Whenever splitting on two+ char literals, always add `StringSplitOptions.None` as second argument.
