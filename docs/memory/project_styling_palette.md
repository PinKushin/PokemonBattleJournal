---
name: project_styling_palette
description: "App palette and font conventions for all pages — PokeYellow headings, PokeBlue accents, SairaRegular body, PokemonSolid titles."
metadata: 
  node_type: memory
  type: project
  originSessionId: 9bcb5645-bb7a-4eb5-8136-ff774166a95e
  modified: 2026-07-26T10:53:56.637Z
---

All pages use a consistent palette defined in `Resources/Styles/Colors.xaml`:

- **Titles/headings:** `FontFamily="PokemonSolid"`, `TextColor="{StaticResource PokeYellow}"` (`#FFDE00`)
- **Section labels / accents:** `TextColor="{StaticResource PokeBlue}"` (`#3B4CCA`), `FontFamily="SairaRegular"`, `FontSize="12"`, text in ALL CAPS
- **Body text:** `FontFamily="SairaRegular"`, `TextColor` via `AppThemeBinding` (MidnightBlue light / Gray200 dark)
- **Input borders:** `Stroke="{StaticResource PokeYellow}"`, `StrokeThickness="1.5"`, `Padding="12,8"`
- **Field label inside border:** PokeYellow, FontSize 12
- **Buttons:** Global style (PokeBlue bg, white/PokeYellow text). Destructive actions use `BackgroundColor="{StaticResource BostonRed}"` with white text.
- **Tag chips:** `BackgroundColor="{StaticResource PokeBlue}"`, `StrokeThickness="0"`, white label text
- **Separators:** `BoxView` with `BackgroundColor="{StaticResource PokeBlue}"`, `HeightRequest="1"`

**Styling pass status (2026-07-26):** All Shell pages styled — MainPage, ReadJournalPage, OptionsPage, AboutPage, FirstStartPage. TrainerPage has placeholder labels pending safe chart lazy-init.

**How to apply:** When adding a new page or section, follow the patterns above. Never use raw `Stroke="Gray"` on input borders — always PokeYellow. Never use plain `Label` for page title — always PokemonSolid + PokeYellow.
