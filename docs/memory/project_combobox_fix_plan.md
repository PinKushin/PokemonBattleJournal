---
name: project-combobox-fix-plan
description: Plan to fix archetype ComboBox popup clickability on Android + Windows UI tests
metadata:
  type: project
---

## Goal
Fix Android UI tests: archetype ComboBox popup items report `clickable=false` in UIAutomator and are silently ignored. Windows tests must stay green.

## Root Cause
`ContentView` + `TapGestureRecognizer` = Android UIAutomator sees `clickable=false`. MAUI renders it as a non-interactive view; UIAutomator `ACTION_CLICK` silently does nothing.

## Fix 1 — ComboBoxControl trigger (PROVEN, from this session)
Replace `TapGestureRecognizer` on `Border` with a transparent `Button` overlay (UraniumUI Dropdown pattern). Button subclass = native Android `clickable=true`.

```csharp
var triggerButton = new Button
{
    BackgroundColor = Colors.Transparent,
    BorderWidth = 0,
    BorderColor = Colors.Transparent,
    Text = string.Empty,
    Opacity = 0,
    InputTransparent = false,
};
triggerButton.Clicked += OnTapped;

var overlayGrid = new Grid();
overlayGrid.Add(_border);
overlayGrid.Add(triggerButton);
Content = overlayGrid;
```

## Fix 2 — ComboBoxPopup items (PROVEN, from this session)
`CollectionView SelectionMode="Single"` with `SelectionChanged="OnItemSelected"` code-behind makes items natively selectable (MAUI marks them clickable). AutomationId binding in DataTemplate is unreliable on Windows — use XPath `//ListItem` in WinAppDriver tests instead.

Key XAML:
```xml
<CollectionView x:Name="ArchetypeList"
                AutomationId="ArchetypeList"
                ItemsSource="{Binding FilteredItems}"
                SelectionMode="Single"
                SelectionChanged="OnItemSelected" />
```

## Fix 3 — Popup result passing (IMPORTANT — DON'T use TCS pattern)
The dual-await pattern (`await ShowPopupAsync` + `await popup.ResultTask`) was broken. Use `CloseAsync(result)` to pass result through `ShowPopupAsync` directly:

```csharp
// In popup code-behind:
private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
{
    if (_closing) return;
    if (e.CurrentSelection.FirstOrDefault() is not ArchetypeDisplayItem item) return;
    _closing = true;
    try { await CloseAsync(item.OriginalItem); }
    catch (Exception ex) { Console.Error.WriteLine($"[ComboBoxPopup] {ex.Message}"); }
}

// In ComboBoxControl.OnTapped:
private async void OnTapped(object? sender, EventArgs e)
{
    if (ItemsSource == null) return;
    try
    {
        var popup = new ComboBoxPopup(...);
        var result = await Shell.Current.CurrentPage.ShowPopupAsync(popup, options);
        if (result != null) SelectedItem = result;
    }
    catch (Exception ex) { Console.Error.WriteLine($"[ComboBoxControl] {ex.Message}"); }
}
```

NOTE: `ShowPopupAsync` extension is in `CommunityToolkit.Maui.Extensions` — keep that using.
`CloseAsync(object? result)` is the non-generic overload — pass result directly, no TCS needed.

## Fix 4 — XAML popup rewrite
Wrote ComboBoxPopup as XAML + code-behind + separate ViewModel. Key files:
- `ComboBoxPopup.xaml` — layout
- `ComboBoxPopup.xaml.cs` — selection handler, cancel handler
- `ComboBoxPopupViewModel.cs` — FilteredItems, SearchText, CancelCommand
- `ArchetypeDisplayItem.cs` — wrapper normalizing Name/ImagePath/AutomationId/OriginalItem

## Test seeding approach

### Windows (NO two-phase — user requirement)
Single launch. After welcome dialog handled, seed 3 matches via UI (select archetype from fixed popup, pick Win result, click Save). With the Button overlay fix, popup items are now clickable in WinAppDriver too.
- WinAppDriver finds ListItems via XPath `//ListItem` (AutomationId binding unreliable on Windows)
- Save button click resets form — repeat 3 times

### Android (two-phase acceptable)
Phase 1: fresh install → handle welcome dialog → force-stop → `adb root` + `adb pull` DB
Phase 2: inject via `TestDbSeeder` (SQL) → `adb push` back → `ActivateApp`
`TestDbSeeder` queries active trainer + first 2 archetype IDs dynamically — no hardcoded IDs.

## TestDbSeeder (keep for Android, reference for Windows if needed)
Location: `PokemonBattleJournal.UITests/UITests.Shared/TestDbSeeder.cs`
- Uses `Microsoft.Data.Sqlite` (add to both csproj)
- Queries trainer ID: `SELECT Id FROM Trainer WHERE IsActive = 1 LIMIT 1`
- Queries archetype IDs: `SELECT Id FROM Archetype LIMIT 2`
- Inserts `Game` + `MatchEntry` rows (Result=0=Win, enums as INTEGER, DateTime as "yyyy-MM-dd HH:mm:ss")
- Column names verbatim C# property names (sqlite-net-pcl convention, no snake_case)

## Archetype click tests (add after seeding works)
In `MainPageTests.cs` (shared):
- `MainPage_PlayerArchetype_OpensPopup` — click trigger, verify Cancel button visible, click cancel
- `MainPage_PlayerArchetype_SelectsItem` — call `SelectFirstArchetypeItem("PlayerArchetype")`, verify back on main page
- `MainPage_RivalArchetype_SelectsItem` — same for rival

`SelectFirstArchetypeItem` helper in `BaseTest`:
- Windows: `SwitchToMainWindow()` before trigger click, find `//ListItem` via XPath, click, `SwitchToMainWindow()` after
- Android: `UiSelector().resourceIdMatches("...ArchetypeItem_.*").instance(0)`

## WinAppDriver NoSuchWindowException
CommunityToolkit.Maui Popup on WinUI may open as separate native HWND. After popup closes, WinAppDriver context may be on dead HWND.
Fix: `SwitchToMainWindow()` helper that tries saved handle first, then iterates `App.WindowHandles`.
Save handle at launch in `AppiumSetup.MainWindowHandle`.

**Why:** `App is WindowsDriver ? App.CurrentWindowHandle : null` called AFTER popup opens = may return popup HWND, not main window. Must save handle BEFORE any popup opens (i.e., at app launch time in AppiumSetup).

## Open source references (in long-term memory)
UraniumUI + Controls.UserDialogs.Maui — used as reference for Button overlay pattern and CollectionView SelectionMode approach. Links in `reference_open_source_controls.md`.
