using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace PokemonBattleJournal.Controls;

public class ComboBoxPopup : Popup<ComboBoxPopup.PickerResult?>
{
    public record PickerResult(object? SelectedItem);

    public ComboBoxPopup(
        IEnumerable itemsSource,
        string displayMemberPath,
        string imageMemberPath,
        Color backgroundColor,
        Color accentColor,
        double popupWidth,
        double itemHeight,
        string imageMemberPath2 = "")
    {
        BackgroundColor = Colors.Transparent;
        bool closing = false;

        // Snapshot all items so we can filter without mutating the source
        List<object> allItems = itemsSource.Cast<object>().ToList();
        ObservableCollection<object> filteredItems = new ObservableCollection<object>(allItems);

        CollectionView collectionView = new CollectionView
        {
            ItemsSource = filteredItems,
            SelectionMode = SelectionMode.None,
            HeightRequest = 250,
            BackgroundColor = backgroundColor,
            WidthRequest = (float)popupWidth
        };

        collectionView.ItemTemplate = new DataTemplate(() =>
        {
            Image image = new Image
            {
                HeightRequest = 26,
                WidthRequest = 26,
                Aspect = Aspect.AspectFit,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(10, 0, 4, 0)
            };
            image.SetBinding(Image.SourceProperty, new Binding(imageMemberPath));
            image.SetBinding(SemanticProperties.DescriptionProperty, new Binding(displayMemberPath, stringFormat: "{0} icon"));

            Image image2 = new Image
            {
                HeightRequest = 26,
                WidthRequest = 26,
                Aspect = Aspect.AspectFit,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 0, 10, 0)
            };
            bool hasSecondIcon = !string.IsNullOrEmpty(imageMemberPath2);
            if (hasSecondIcon)
            {
                image2.SetBinding(Image.SourceProperty, new Binding(imageMemberPath2));
                image2.SetBinding(VisualElement.IsVisibleProperty,
                    new Binding(imageMemberPath2, converter: StringNotEmptyConverter.Instance));
            }
            else
            {
                image2.IsVisible = false;
            }

            Label label = new Label
            {
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 13,
                TextColor = accentColor
            };
            label.SetBinding(Label.TextProperty, new Binding(displayMemberPath));


            HorizontalStackLayout iconStack = new HorizontalStackLayout
            {
                Spacing = 0,
                VerticalOptions = LayoutOptions.Center,
                Children = { image, image2 }
            };

            // Visuals only. InputTransparent so taps fall through to the Button layered over it.
            Grid grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                ],
                Children = { iconStack, label },
                HeightRequest = itemHeight,
                BackgroundColor = backgroundColor,
                InputTransparent = true
            };
            Grid.SetColumn(label, 1);

            // The item was a Grid with a TapGestureRecognizer, which exposes NO UIA pattern.
            // Two consequences, and the accessibility one is the more serious:
            //
            // A screen reader could not select a deck at all. Assistive tech activates controls
            // through UIA patterns, not by clicking pixels, so every archetype in this list was
            // announced correctly (the Hint below said "Double tap to select …") and was
            // impossible to actually pick.
            //
            // And automation could only reach it by synthesized mouse click at screen
            // coordinates, which lands outside the app when the item sits below the window —
            // silently doing nothing on CI. See docs/memory/feedback_invokable_controls.md.
            //
            // MAUI's Button cannot host an icon+label layout as content, so it overlays the
            // Grid instead: transparent, borderless, and carrying the AutomationId and semantics
            // because it is what UIA, Appium and screen readers now see. Minimums are zeroed
            // because the implicit Button style sets them to 44, and a minimum beats an
            // explicit HeightRequest.
            Button button = new Button
            {
                BackgroundColor = Colors.Transparent,
                BorderColor = Colors.Transparent,
                BorderWidth = 0,
                CornerRadius = 0,
                Padding = 0,
                HeightRequest = itemHeight,
                MinimumHeightRequest = 0,
                MinimumWidthRequest = 0
            };
            button.SetBinding(AutomationIdProperty, new Binding(displayMemberPath, stringFormat: "ArchetypeItem_{0}"));
            button.SetBinding(SemanticProperties.DescriptionProperty, new Binding(displayMemberPath));
            button.SetBinding(SemanticProperties.HintProperty, new Binding(displayMemberPath, stringFormat: "Double tap to select {0}"));
            button.Clicked += async (s, e) =>
            {
                if (closing) return;
                closing = true;
                if (s is Button b && b.BindingContext is { } item)
                    await CloseAsync(new PickerResult(item));
            };

            // Button last so it sits on top of the visuals in the same cell.
            return new Grid
            {
                HeightRequest = itemHeight,
                Children = { grid, button }
            };
        });

        Label titleLabel = new Label
        {
            Text = "Select an option",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = accentColor,
            Margin = new Thickness(0, 8, 0, 5)
        };

        SearchBar searchBar = new SearchBar
        {
            Placeholder = "Search...",
            BackgroundColor = backgroundColor,
            TextColor = accentColor,
            PlaceholderColor = accentColor.MultiplyAlpha(0.5f),
            CancelButtonColor = accentColor,
            WidthRequest = popupWidth,
            Margin = new Thickness(0, 0, 0, 4),
            AutomationId = "ArchetypeSearchBar",
        };
        // NOTE: Do NOT call SemanticProperties.SetDescription on this SearchBar.
        // On Android inside a CommunityToolkit Popup, that call causes the entire popup
        // to fail to render (verified 2026-08-04 — DUMP showed no popup elements in tree
        // after PlayerArchetype.Click when Description was set). AutomationId alone maps
        // to resource-id reliably here.
        searchBar.TextChanged += (s, e) =>
        {
            string query = e.NewTextValue?.Trim() ?? string.Empty;
            filteredItems.Clear();
            foreach (object item in FilterItems(allItems, query, displayMemberPath))
                filteredItems.Add(item);
        };

        Button closeButton = new Button
        {
            Text = "Cancel",
            AutomationId = "ArchetypePopupCancel",
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 120,
            TextColor = accentColor,
            BackgroundColor = accentColor.MultiplyAlpha(0.2f),
            Margin = new Thickness(0, 5, 0, 8)
        };
        // Do not set SemanticProperties.Description here either — even though Button is a
        // simpler widget than SearchBar, the safer path is to leave popup element accessibility
        // to AutomationId (resource-id) only. See searchBar comment above.
        closeButton.Clicked += async (s, e) =>
        {
            if (closing) return;
            closing = true;
            await CloseAsync(null);
        };

        Content = new Border
        {
            Stroke = accentColor,
            StrokeThickness = 1.5,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            BackgroundColor = backgroundColor,
            Padding = 12,
            Content = new VerticalStackLayout
            {
                Children = { titleLabel, searchBar, collectionView, closeButton },
                WidthRequest = (int)popupWidth,
            }
        };
    }

    internal static IEnumerable<object> FilterItems(IEnumerable<object> items, string query, string displayMemberPath)
    {
        if (string.IsNullOrEmpty(query))
            return items;

        return items.Where(item =>
        {
            PropertyInfo? prop = item.GetType().GetProperty(displayMemberPath);
            string? text = prop?.GetValue(item)?.ToString();
            return text != null && text.Contains(query, StringComparison.OrdinalIgnoreCase);
        });
    }
}
