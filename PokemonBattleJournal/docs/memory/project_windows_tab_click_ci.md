---
name: project_windows_tab_click_ci
description: Game tabs must be Button not Border — Border+TapGestureRecognizer has no UIA InvokePattern; all pointer simulation fails on Windows CI
metadata:
  type: project
---

`Border+TapGestureRecognizer` does not expose UIA `InvokePattern`. WinAppDriver's `.Click()` falls back to pointer simulation, and every pointer type fails in some environment:
- `PointerKind.Touch` — silent no-op on Windows Server CI (no touch driver)
- `PointerKind.Mouse` — rejected by WinAppDriver locally: `UnsupportedOperationException: Currently only pen and touch pointer input source types are supported`
- `PointerKind.Pen` — accepted by WinAppDriver but doesn't fire `TapGestureRecognizer` on CI (no interactive session / WinUI3 pen event handling differs)

**Fix:** change tab elements from `Border` to `Button` with `BorderWidth="0"` and `CornerRadius`. `Button` exposes UIA `InvokePattern` — WinAppDriver's `.Click()` calls `Invoke()` directly, no pointer simulation, works on every Windows configuration.

```xml
<Button
    AutomationId="Game2Tab"
    BackgroundColor="{Binding IsGame2Selected, Converter={StaticResource TabActiveBgConverter}}"
    BorderWidth="0"
    Command="{Binding SelectGame2Command}"
    CornerRadius="6"
    FontAttributes="{Binding IsGame2Selected, Converter={StaticResource TabActiveFontConverter}}"
    FontFamily="SairaRegular"
    FontSize="14"
    IsVisible="{Binding BO3Toggle}"
    Padding="16,8"
    Text="Game 2"
    TextColor="{AppThemeBinding Light={StaticResource MidnightBlue}, Dark={StaticResource PokeYellow}}" />
```

**Why:** InvokePattern is a UIA contract that works headlessly. Pointer simulation depends on input drivers and interactive session state — neither of which is reliable on CI.

**How to apply:** Any clickable MAUI non-Button tappable needed by Windows UI tests must be a `Button` or expose `InvokePattern` via a platform handler. Never use `Border+TapGestureRecognizer` for elements that need Appium interaction.
