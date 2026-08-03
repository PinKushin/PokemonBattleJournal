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
        double itemHeight)
    {
        BackgroundColor = Colors.Transparent;
        var closing = false;

        // Snapshot all items so we can filter without mutating the source
        var allItems = itemsSource.Cast<object>().ToList();
        var filteredItems = new ObservableCollection<object>(allItems);

        var collectionView = new CollectionView
        {
            ItemsSource = filteredItems,
            SelectionMode = SelectionMode.None,
            HeightRequest = 250,
            BackgroundColor = backgroundColor,
            WidthRequest = (float)popupWidth
        };

        collectionView.ItemTemplate = new DataTemplate(() =>
        {
            var image = new Image
            {
                HeightRequest = 26,
                WidthRequest = 26,
                Aspect = Aspect.AspectFit,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                Margin = new Thickness(10, 0, 10, 0)
            };
            image.SetBinding(Image.SourceProperty, new Binding(imageMemberPath));
            image.SetBinding(SemanticProperties.DescriptionProperty, new Binding(displayMemberPath, stringFormat: "{0} icon"));

            var label = new Label
            {
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 13,
                TextColor = accentColor
            };
            label.SetBinding(Label.TextProperty, new Binding(displayMemberPath));
            Grid.SetColumn(label, 1);

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (s, e) =>
            {
                if (closing) return;
                closing = true;
                if (s is Grid g && g.BindingContext is { } item)
                    await CloseAsync(new PickerResult(item));
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                ],
                Children = { image, label },
                HeightRequest = itemHeight,
                BackgroundColor = backgroundColor
            };
            grid.SetBinding(AutomationIdProperty, new Binding(displayMemberPath, stringFormat: "ArchetypeItem_{0}"));
            grid.SetBinding(SemanticProperties.HintProperty, new Binding(displayMemberPath, stringFormat: "Double tap to select {0}"));
            grid.GestureRecognizers.Add(tap);
            return grid;
        });

        var titleLabel = new Label
        {
            Text = "Select an option",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = accentColor,
            Margin = new Thickness(0, 8, 0, 5)
        };

        var searchBar = new SearchBar
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
        searchBar.TextChanged += (s, e) =>
        {
            string query = e.NewTextValue?.Trim() ?? string.Empty;
            filteredItems.Clear();
            foreach (object item in FilterItems(allItems, query, displayMemberPath))
                filteredItems.Add(item);
        };

        var closeButton = new Button
        {
            Text = "Cancel",
            AutomationId = "ArchetypePopupCancel",
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 120,
            TextColor = accentColor,
            BackgroundColor = accentColor.MultiplyAlpha(0.2f),
            Margin = new Thickness(0, 5, 0, 8)
        };
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
