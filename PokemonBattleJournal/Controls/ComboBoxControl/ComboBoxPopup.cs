using System.Collections;
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
        var closing = false;

        var collectionView = new CollectionView
        {
            ItemsSource = itemsSource,
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

        var closeButton = new Button
        {
            Text = "Cancel",
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
                Children = { titleLabel, collectionView, closeButton },
                WidthRequest = (int)popupWidth,
            }
        };
    }
}
