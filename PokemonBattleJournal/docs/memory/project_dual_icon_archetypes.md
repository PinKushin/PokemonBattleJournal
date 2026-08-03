---
name: project_dual_icon_archetypes
description: "Dual-icon archetype support — Archetype.ImagePath2, MetaDeck.SecondaryImageUrl, ComboBox and ReadJournal UI changes"
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-08-03T21:15:01.811Z
---

`Archetype` model has `ImagePath` (always present) and `ImagePath2` (optional, null when not applicable). `IArchetypeOperations.SaveAsync` accepts optional `imgPath2 = null`.

`MetaDeck` record has `SecondaryImageUrl?` — populated by `LimitlessDeckParser` when a row has two `img.pokemon` elements.

`ArchetypeOperations.GetAllAsync` upserts `ImagePath2` from scraper: INSERT OR IGNORE includes both columns; separate UPDATE backfills `ImagePath2 IS NULL` rows on first run after schema change.

**ComboBox:** `ComboBoxControl` has `_selectedIcon2` field and `ImageMemberPath2` bindable property (default `"ImagePath2"`). `UpdateDisplay()` shows/hides `_selectedIcon2` based on whether the property value is non-empty. `iconAndText` uses `HorizontalOptions.Center` so single icon centers, dual icons center together. `ComboBoxPopup` accepts `imageMemberPath2` param; uses `StringNotEmptyConverter` on `IsVisible` binding for the second image.

**ReadJournalPageViewModel:** `PlayingIconSource` / `AgainstIconSource` are `string` (never null — always "substitute.png" minimum). `PlayingIconSource2` / `AgainstIconSource2` are `string?` (null when no second icon). `HasPlayingIcon2` / `HasAgainstIcon2` are `bool` properties used for `IsVisible` in XAML — no converter needed in XAML.

**Why no converter in XAML for ReadJournal:** `IsNotNullConverter` from CommunityToolkit crashed the OptionsPage in a prior session. Using explicit bool VM properties is safer.

**DB:** No migration needed — app has no production releases. Wipe local `.db3` to pick up new `ImagePath2` column on fresh sqlite-net schema creation.

**How to apply:** Always keep `ImagePath` (primary) non-nullable in the model; `ImagePath2` nullable. Any UI showing archetype icons must handle null `ImagePath2` gracefully (hide the second slot). Never use `IsNotNullConverter` in XAML for icon visibility — use a bool VM property instead.
