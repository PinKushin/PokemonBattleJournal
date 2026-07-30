---
name: project_android_element_lookup
description: "Android FindUIElement uses direct resourceId first, UiScrollable fallback — UiScrollable is slow even for visible elements"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-30T07:43:41.397Z
---

`FindUIElement` on Android uses a two-stage lookup:

1. **Direct resourceId** (3s timeout) — instant when element is already on screen
2. **UiScrollable.scrollIntoView** (10s timeout) — falls back only when element is off-screen

**Why:** `UiScrollable.scrollIntoView` physically scrolls through the entire view even when the target element is already visible, causing 13+ second waits for elements that appear in under 1 second with a direct lookup.

**Implementation** (`BaseTest.cs`):
```csharp
string resourceId = $"com.PinKushin.PokemonBattleJournal:id/{id}";
try
{
    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(3);
    return App.FindElement(MobileBy.AndroidUIAutomator(
        $"new UiSelector().resourceId(\"{resourceId}\")"));
}
catch (OpenQA.Selenium.NoSuchElementException)
{
    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
    return App.FindElement(MobileBy.AndroidUIAutomator(
        $"new UiScrollable(new UiSelector().scrollable(true).instance(0))" +
        $".scrollIntoView(new UiSelector().resourceId(\"{resourceId}\"))"));
}
finally
{
    App.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
}
```

**Also:** Android empty `Entry` returns placeholder text (e.g. `"Archetype name"`) as `.Text`, not null/empty. Assert with `ShouldNotContain(typedValue)` not `ShouldBeNullOrEmpty()`.
