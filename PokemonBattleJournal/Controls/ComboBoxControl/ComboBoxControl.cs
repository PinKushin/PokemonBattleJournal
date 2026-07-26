using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace PokemonBattleJournal.Controls;

public class ComboBoxControl : ContentView
{
    private readonly Border _border;
    private readonly Label _selectedLabel;
    private readonly Image _selectedIcon;
    private readonly Label _arrowLabel;
    private readonly Label _placeholderLabel;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ComboBoxControl), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ComboBoxControl), null,
            BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ComboBoxControl), "Name");

    public static readonly BindableProperty ImageMemberPathProperty =
        BindableProperty.Create(nameof(ImageMemberPath), typeof(string), typeof(ComboBoxControl), "ImagePath");

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ComboBoxControl), "Select...");

    public static readonly BindableProperty StrokeColorProperty =
        BindableProperty.Create(nameof(StrokeColor), typeof(Color), typeof(ComboBoxControl), Colors.Gray);

    public static readonly BindableProperty StrokeThicknessProperty =
        BindableProperty.Create(nameof(StrokeThickness), typeof(double), typeof(ComboBoxControl), 1.0);

    public static new readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(ComboBoxControl), Colors.White);

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ComboBoxControl), Colors.Black);

    public static readonly BindableProperty ArrowColorProperty =
        BindableProperty.Create(nameof(ArrowColor), typeof(Color), typeof(ComboBoxControl), Colors.Gray);

    public static readonly BindableProperty PopupBackgroundColorProperty =
        BindableProperty.Create(nameof(PopupBackgroundColor), typeof(Color), typeof(ComboBoxControl), Colors.White);

    public static readonly BindableProperty PopupAccentColorProperty =
        BindableProperty.Create(nameof(PopupAccentColor), typeof(Color), typeof(ComboBoxControl), Colors.Gray);

    public static readonly BindableProperty ItemHeightProperty =
        BindableProperty.Create(nameof(ItemHeight), typeof(double), typeof(ComboBoxControl), 40.0);

    public static readonly BindableProperty PopupWidthProperty =
        BindableProperty.Create(nameof(PopupWidth), typeof(double), typeof(ComboBoxControl), 200.0);

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string DisplayMemberPath
    {
        get => (string)GetValue(DisplayMemberPathProperty);
        set => SetValue(DisplayMemberPathProperty, value);
    }

    public string ImageMemberPath
    {
        get => (string)GetValue(ImageMemberPathProperty);
        set => SetValue(ImageMemberPathProperty, value);
    }

    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public Color StrokeColor
    {
        get => (Color)GetValue(StrokeColorProperty);
        set => SetValue(StrokeColorProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public new Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public Color ArrowColor
    {
        get => (Color)GetValue(ArrowColorProperty);
        set => SetValue(ArrowColorProperty, value);
    }

    public Color PopupBackgroundColor
    {
        get => (Color)GetValue(PopupBackgroundColorProperty);
        set => SetValue(PopupBackgroundColorProperty, value);
    }

    public Color PopupAccentColor
    {
        get => (Color)GetValue(PopupAccentColorProperty);
        set => SetValue(PopupAccentColorProperty, value);
    }

    public double ItemHeight
    {
        get => (double)GetValue(ItemHeightProperty);
        set => SetValue(ItemHeightProperty, value);
    }

    public double PopupWidth
    {
        get => (double)GetValue(PopupWidthProperty);
        set => SetValue(PopupWidthProperty, value);
    }

    public ComboBoxControl()
    {
        _placeholderLabel = new Label
        {
            Text = Placeholder,
            TextColor = Colors.Gray,
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        _placeholderLabel.SetBinding(Label.TextProperty, new Binding(nameof(Placeholder), source: this));
        _placeholderLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(TextColor), source: this));

        _selectedLabel = new Label
        {
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            LineBreakMode = LineBreakMode.TailTruncation,
            IsVisible = false
        };
        _selectedLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(TextColor), source: this));

        _selectedIcon = new Image
        {
            WidthRequest = 28,
            HeightRequest = 28,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            Aspect = Aspect.AspectFit,
            IsVisible = false
        };

        _arrowLabel = new Label
        {
            Text = "\u25BC",
            FontSize = 10,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        _arrowLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(ArrowColor), source: this));

        var iconAndText = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End,
            Children = { _selectedIcon, _selectedLabel, _placeholderLabel }
        };

        var contentLayout = new Grid
        {
            Padding = new Thickness(12, 4),
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };
        Grid.SetColumn(iconAndText, 0);
        Grid.SetColumn(_arrowLabel, 1);
        contentLayout.Add(iconAndText);
        contentLayout.Add(_arrowLabel);

        _border = new Border
        {
            StrokeThickness = 1,
            Padding = 0,
            Content = contentLayout,
        };
        _border.SetBinding(Border.StrokeProperty, new Binding(nameof(StrokeColor), source: this));
        _border.SetBinding(Border.StrokeThicknessProperty, new Binding(nameof(StrokeThickness), source: this));
        _border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(BackgroundColor), source: this));

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnTapped;
        _border.GestureRecognizers.Add(tapGesture);

        Content = _border;

        UpdateDisplay();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is ComboBoxControl control)
        {
            control.UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        _selectedIcon.IsVisible = true;

        if (SelectedItem != null)
        {
            _placeholderLabel.IsVisible = false;
            _selectedLabel.IsVisible = true;
            var name = SelectedItem.GetType().GetProperty(DisplayMemberPath)?.GetValue(SelectedItem)?.ToString() ?? "";
            var imagePath = SelectedItem.GetType().GetProperty(ImageMemberPath)?.GetValue(SelectedItem)?.ToString();
            _selectedLabel.Text = name;
            _selectedIcon.Source = imagePath;
        }
        else
        {
            _placeholderLabel.IsVisible = true;
            _selectedLabel.IsVisible = false;
            _selectedIcon.Source = "ball_icon.png";
        }
    }

    private async void OnTapped(object? sender, EventArgs e)
    {
        if (ItemsSource == null) return;

        var popup = new ComboBoxPopup(
            ItemsSource,
            DisplayMemberPath,
            ImageMemberPath,
            PopupBackgroundColor,
            PopupAccentColor,
            PopupWidth,
            ItemHeight);

        var popupResult = await Shell.Current.CurrentPage.ShowPopupAsync<ComboBoxPopup.PickerResult?>(popup, new PopupOptions
        {
            Shape = null,
            Shadow = null
        });

        if (popupResult?.Result is ComboBoxPopup.PickerResult pickerResult && pickerResult.SelectedItem != null)
        {
            SelectedItem = pickerResult.SelectedItem;
        }
    }
}