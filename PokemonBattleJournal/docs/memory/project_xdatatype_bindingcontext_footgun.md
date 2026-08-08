---
name: project_xdatatype_bindingcontext_footgun
description: "x:DataType does NOT follow BindingContext except in a DataTemplate — an element that re-anchors its context needs its own x:DataType or every binding under it compiles against the wrong type and fails SILENTLY: blank labels, commands that never fire, UIA Invoke still 'succeeds'."
metadata: 
  node_type: memory
  type: project
  originSessionId: 4938edd8-4dd8-4200-98f4-755f11ee9fd8
  modified: 2026-08-08T15:55:37.807Z
---

Found 2026-08-08 building the conflict-walk card (`feat/conflict-overlay`, OptionsPage).

## The trap

The page root declares `x:DataType="viewmodel:OptionsPageViewModel"`. A child element that
changes its own context — `<VerticalStackLayout BindingContext="{Binding CurrentConflict}">` —
**keeps the ambient compiled-binding type**. Every `{Binding ...}` inside compiled against the
PAGE view model while the runtime context was a `ConflictRowViewModel`.

Failure mode is fully silent:
- No build error (`Title` happened to exist on both types; others just produced nothing)
- Labels render blank
- Button commands never fire — and the UI test PerfLog still shows `UIA Invoke` succeeding,
  because the invoke lands on the control; the dead binding is underneath it

Only a `DataTemplate` resets the ambient type via its own `x:DataType`. That is why the old
CollectionView version worked and the extracted single card did not.

## The fix

Declare the type at the point the context changes, and type the context binding itself:

```xml
<VerticalStackLayout
    x:DataType="viewmodel:ConflictRowViewModel"
    BindingContext="{Binding CurrentConflict, x:DataType=viewmodel:OptionsPageViewModel}">
```

## How it was caught

`OptionsPage_ConflictWalk_NextShowsTheSecondMatch` asserts the title **changes** across Next —
element-exists assertions had passed the whole time. Direct instance of
[[feedback_tests_that_cannot_fail]]: assert the behaviour, not the presence.
