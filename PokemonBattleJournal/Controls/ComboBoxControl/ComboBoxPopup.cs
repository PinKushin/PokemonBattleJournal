using System.Collections;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace PokemonBattleJournal.Controls;

public class ComboBoxPopup : Popup
{
    public record PickerResult(object? SelectedItem);

    public IEnumerable? ItemsSource { get; set; }
    public string DisplayMemberPath { get; set; } = "Name";
    public string ImageMemberPath { get; set; } = "ImagePath";
    public object? SelectedItem { get; set; }
    public Color BackgroundColor { get; set; } = Colors.White;
    public Color AccentColor { get; set; } = Colors.Gray;
    public double PopupWidth { get; set; } = 280;
    public double ItemHeight { get; set; } = 40;

    public ComboBoxPopup()
    {
        var titleLabel = new Label
        {
            Text = "Select an option",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = AccentColor,
            Margin = new Thickness(0, 8, 0, 5)
        };

        var collectionView = new CollectionView
        {
            ItemsSource = ItemsSource,
            SelectionMode = SelectionMode.Single,
            HeightRequest = 250,
            BackgroundColor = BackgroundColor,
            WidthRequest = (float)PopupWidth
        };

        collectionView.SetBinding(CollectionView.SelectedItemProperty, new Binding(
            nameof(SelectedItem),
            source: this,
            mode: BindingMode.TwoWay));

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
            image.SetBinding(Image.SourceProperty, new Binding(ImageMemberPath));

            var label = new Label
            {
                VerticalTextAlignment = TextAlignment.Center,
                FontSize = 13,
                TextColor = Colors.Black
            };
            label.SetBinding(Label.TextProperty, new Binding(DisplayMemberPath));

            var grid = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                ],
                Children = { image, label },
                HeightRequest = ItemHeight,
                BackgroundColor = BackgroundColor
            };

            return new ViewCell { View = grid };
        });

        collectionView.SelectionChanged += (s, e) =>
        {
            if (e.CurrentSelection.FirstOrDefault() != null)
            {
                Close(new PickerResult(e.CurrentSelection.First()));
            }
        };

        var closeButton = new Button
        {
            Text = "Cancel",
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 120,
            TextColor = AccentColor,
            BackgroundColor = AccentColor.MultiplyAlpha(0.2f),
            Margin = new Thickness(0, 5, 0, 8)
        };
        closeButton.Clicked += (s, e) => Close();

        var container = new VerticalStackLayout
        {
            Children = { titleLabel, collectionView, closeButton },
            WidthRequest = (int)PopupWidth,
            Padding = 12,
            BackgroundColor = BackgroundColor
        };

        Content = container;
    }
}