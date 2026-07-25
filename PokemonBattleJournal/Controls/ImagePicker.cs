using System.Collections;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;

namespace PokemonBattleJournal.Controls;

public class ImagePicker : ContentView
{
    private readonly Button _button;
    private readonly Label _hintLabel;
    private readonly Label _helperLabel;

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(System.Collections.IEnumerable), typeof(ImagePicker), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ImagePicker), null,
            defaultBindingMode: BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ImagePicker), "Name");

    public static readonly BindableProperty ImageMemberPathProperty =
        BindableProperty.Create(nameof(ImageMemberPath), typeof(string), typeof(ImagePicker), "ImagePath");

    public static readonly BindableProperty HintTextProperty =
        BindableProperty.Create(nameof(HintText), typeof(string), typeof(ImagePicker), "");

    public static readonly BindableProperty HelperTextProperty =
        BindableProperty.Create(nameof(HelperText), typeof(string), typeof(ImagePicker), "");

    public static readonly BindableProperty PlaceholderProperty =
        BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(ImagePicker), "Select...");

    public static readonly BindableProperty StrokeColorProperty =
        BindableProperty.Create(nameof(StrokeColor), typeof(Color), typeof(ImagePicker), Colors.Gray);

    public static readonly BindableProperty TextColorProperty =
        BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(ImagePicker), Colors.White);

    public static new readonly BindableProperty BackgroundColorProperty =
        BindableProperty.Create(nameof(BackgroundColor), typeof(Color), typeof(ImagePicker), Colors.Transparent);

    public static readonly BindableProperty HintTextColorProperty =
        BindableProperty.Create(nameof(HintTextColor), typeof(Color), typeof(ImagePicker), Colors.LightGray);

    public static readonly BindableProperty HelperTextColorProperty =
        BindableProperty.Create(nameof(HelperTextColor), typeof(Color), typeof(ImagePicker), Colors.LightGray);

    public static readonly BindableProperty PopupBackgroundColorProperty =
        BindableProperty.Create(nameof(PopupBackgroundColor), typeof(Color), typeof(ImagePicker), Colors.DarkSlateGray);

    public static readonly BindableProperty PopupAccentColorProperty =
        BindableProperty.Create(nameof(PopupAccentColor), typeof(Color), typeof(ImagePicker), Colors.Gray);

    public System.Collections.IEnumerable? ItemsSource
    {
        get => (System.Collections.IEnumerable?)GetValue(ItemsSourceProperty);
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

    public string HintText
    {
        get => (string)GetValue(HintTextProperty);
        set => SetValue(HintTextProperty, value);
    }

    public string HelperText
    {
        get => (string)GetValue(HelperTextProperty);
        set => SetValue(HelperTextProperty, value);
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

    public Color TextColor
    {
        get => (Color)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public new Color BackgroundColor
    {
        get => (Color)GetValue(BackgroundColorProperty);
        set => SetValue(BackgroundColorProperty, value);
    }

    public Color HintTextColor
    {
        get => (Color)GetValue(HintTextColorProperty);
        set => SetValue(HintTextColorProperty, value);
    }

    public Color HelperTextColor
    {
        get => (Color)GetValue(HelperTextColorProperty);
        set => SetValue(HelperTextColorProperty, value);
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

    public ImagePicker()
    {
        _hintLabel = new Label
        {
            FontSize = 11,
            Margin = new Thickness(8, 3, 8, 0)
        };
        _hintLabel.SetBinding(Label.TextProperty, new Binding(nameof(HintText), source: this));
        _hintLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(HintTextColor), source: this));

        _button = new Button
        {
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 32,
            Padding = new Thickness(8, 0),
            FontSize = 13
        };
        _button.SetBinding(Button.TextColorProperty, new Binding(nameof(TextColor), source: this));
        _button.SetBinding(Button.BackgroundColorProperty, new Binding(nameof(BackgroundColor), source: this));
        _button.Clicked += OnButtonClicked;

        _helperLabel = new Label
        {
            FontSize = 9,
            Margin = new Thickness(8, 0, 8, 3)
        };
        _helperLabel.SetBinding(Label.TextProperty, new Binding(nameof(HelperText), source: this));
        _helperLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(HelperTextColor), source: this));

        var border = new Border
        {
            StrokeThickness = 1,
            Padding = 0
        };
        border.SetBinding(Border.StrokeProperty, new Binding(nameof(StrokeColor), source: this));

        var layout = new VerticalStackLayout
        {
            Children = { _hintLabel, _button, _helperLabel }
        };

        border.Content = layout;
        Content = border;

        UpdateButton();
    }

    private static void OnSelectedItemChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is ImagePicker picker)
        {
            picker.UpdateButton();
        }
    }

    private void UpdateButton()
    {
        if (SelectedItem == null)
        {
            _button.Text = Placeholder;
            _button.ImageSource = null;
            return;
        }

        var displayProp = DisplayMemberPath;
        var imageProp = ImageMemberPath;

        var name = SelectedItem.GetType().GetProperty(displayProp)?.GetValue(SelectedItem)?.ToString() ?? "";
        var imagePath = SelectedItem.GetType().GetProperty(imageProp)?.GetValue(SelectedItem)?.ToString();

        _button.Text = name;
        _button.ImageSource = imagePath;
    }

    private async void OnButtonClicked(object? sender, EventArgs e)
    {
        if (ItemsSource == null) return;

        var popup = new ImagePickerPopup(
            ItemsSource,
            DisplayMemberPath,
            ImageMemberPath,
            PopupBackgroundColor,
            TextColor,
            PopupAccentColor)
        {
            SelectedItem = SelectedItem
        };

        var popupResult = await Shell.Current.CurrentPage.ShowPopupAsync<ImagePickerPopup.PickerResult?>(popup, new PopupOptions());

        if (popupResult?.Result is ImagePickerPopup.PickerResult pickerResult && pickerResult.SelectedItem != null)
        {
            SelectedItem = pickerResult.SelectedItem;
        }
    }
}
