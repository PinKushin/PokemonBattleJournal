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
        var collectionView = new CollectionView
        {
            ItemsSource = itemsSource,
            SelectionMode = SelectionMode.Single,
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
                TextColor = Colors.Black
            };
            label.SetBinding(Label.TextProperty, new Binding(displayMemberPath));
            Grid.SetColumn(label, 1);

            return new Grid
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
        });

        collectionView.SelectionChanged += async (s, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() != null)
            {
                await CloseAsync(new PickerResult(e.CurrentSelection.First()));
            }
        };

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
        closeButton.Clicked += async (s, e) => await CloseAsync(null);

        Content = new VerticalStackLayout
        {
            Children = { titleLabel, collectionView, closeButton },
            WidthRequest = (int)popupWidth,
            Padding = 12,
            BackgroundColor = backgroundColor
        };
    }
}
