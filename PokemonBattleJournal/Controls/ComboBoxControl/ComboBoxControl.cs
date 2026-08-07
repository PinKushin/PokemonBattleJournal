using System.Collections;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls;

namespace PokemonBattleJournal.Controls;

#pragma warning disable S1450 // MAUI UI elements must be fields to prevent GC when added to the visual tree
public class ComboBoxControl : ContentView
{
    private readonly Border _border;
    private readonly Label _selectedLabel;
    private readonly Image _selectedIcon;
    private readonly Image _selectedIcon2;
    private readonly Border _icon2Wrapper;
    private readonly Label _arrowLabel;
    private readonly Label _placeholderLabel;
#pragma warning restore S1450

    public static readonly BindableProperty ItemsSourceProperty =
        BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(ComboBoxControl), null);

    public static readonly BindableProperty SelectedItemProperty =
        BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(ComboBoxControl), null,
            BindingMode.TwoWay, propertyChanged: OnSelectedItemChanged);

    public static readonly BindableProperty DisplayMemberPathProperty =
        BindableProperty.Create(nameof(DisplayMemberPath), typeof(string), typeof(ComboBoxControl), "Name");

    public static readonly BindableProperty ImageMemberPathProperty =
        BindableProperty.Create(nameof(ImageMemberPath), typeof(string), typeof(ComboBoxControl), "ImagePath");

    public static readonly BindableProperty ImageMemberPath2Property =
        BindableProperty.Create(nameof(ImageMemberPath2), typeof(string), typeof(ComboBoxControl), "ImagePath2");

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

    public string ImageMemberPath2
    {
        get => (string)GetValue(ImageMemberPath2Property);
        set => SetValue(ImageMemberPath2Property, value);
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
            Aspect = Aspect.AspectFit,
            IsVisible = false
        };

        _selectedIcon2 = new Image
        {
            WidthRequest = 28,
            HeightRequest = 28,
            VerticalOptions = LayoutOptions.Center,
            Aspect = Aspect.AspectFit,
        };

        _icon2Wrapper = new Border
        {
            Padding = 0,
            StrokeThickness = 0,
            BackgroundColor = Colors.Transparent,
            Content = _selectedIcon2,
            IsVisible = false,
        };
        Microsoft.Maui.Controls.SemanticProperties.SetDescription(_icon2Wrapper, "Second deck icon");

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
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            Children = { _selectedIcon, _icon2Wrapper, _selectedLabel, _placeholderLabel }
        };

        var contentLayout = new Grid
        {
            Padding = new Thickness(12, 4, 20, 4),
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

        // A Border with a TapGestureRecognizer exposes no UIA pattern: unusable by a screen
        // reader, and reachable by automation only through a coordinate click that lands outside
        // the app when the control sits below the window. A transparent Button overlays it and
        // owns activation instead — MAUI's Button cannot host this layout as content, so it sits
        // on top. See docs/memory/feedback_invokable_controls.md.
        _border.InputTransparent = true;

        var activator = new Button
        {
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 0,
            Padding = 0,
            MinimumHeightRequest = 0,
            MinimumWidthRequest = 0
        };
        activator.Clicked += (s, e) => OnTapped(s, EventArgs.Empty);
        _activator = activator;

        Content = new Grid { Children = { _border, activator } };

        // Apply whatever was set before the activator existed, then stay in sync via
        // OnPropertyChanged / the bindable property's changed handler.
        if (!string.IsNullOrEmpty(ActivatorAutomationId))
            activator.AutomationId = ActivatorAutomationId;
        ForwardAutomationToActivator();

        UpdateDisplay();
    }

    /// <summary>
    /// Moves the screen-reader semantics from this ContentView onto the inner activator Button.
    /// </summary>
    /// <remarks>
    /// The Button is the only part of this control exposing a UIA InvokePattern, so it is what a
    /// screen reader can act on — but consumers set the description and hint on the ContentView,
    /// which has no pattern. Left there, the named element is not invokable and the invokable
    /// element is anonymous.
    ///
    /// Moved rather than copied so the description is announced once, against the control that
    /// can actually be activated.
    /// </remarks>
    private void ForwardAutomationToActivator()
    {
        if (_activator is null || _forwardingAutomation)
            return;

        _forwardingAutomation = true;
        try
        {
            // AutomationId is deliberately NOT forwarded. MAUI throws
            // "AutomationId may only be set one time" — it cannot be cleared or reassigned once
            // XAML has set it, so it can only be COPIED to the activator, and a duplicate id
            // resolves to this ContentView first because a lookup finds the parent. Copying
            // would change nothing and add an ambiguous id to the tree.
            //
            // Consequence: automation still reaches this control by its guarded mouse click,
            // while a screen reader gets an invokable, named Button. Making the id land on the
            // Button would mean not setting it on the control at all — a consumer-facing API
            // change, and not worth it for a click that is already verified inside the window.
            string? description = SemanticProperties.GetDescription(this);
            if (!string.IsNullOrEmpty(description))
            {
                SemanticProperties.SetDescription(_activator, description);
                SemanticProperties.SetDescription(this, string.Empty);
            }

            string? hint = SemanticProperties.GetHint(this);
            if (!string.IsNullOrEmpty(hint))
            {
                SemanticProperties.SetHint(_activator, hint);
                SemanticProperties.SetHint(this, string.Empty);
            }
        }
        finally
        {
            _forwardingAutomation = false;
        }
    }

    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (_forwardingAutomation)
            return;

        if (propertyName is "Description" or "Hint")
        {
            ForwardAutomationToActivator();
        }
    }

    /// <summary>
    /// AutomationId applied to the control's activator Button rather than to this ContentView.
    /// </summary>
    /// <remarks>
    /// Use this instead of <c>AutomationId</c> on a ComboBoxControl. The activator Button is the
    /// only part of this control exposing a UIA InvokePattern, so it is what automation and
    /// assistive tech can act on — an id on the ContentView identifies an element neither can
    /// activate, leaving clicks to fall back to a screen-coordinate mouse press.
    ///
    /// It cannot simply be forwarded from <c>AutomationId</c>: MAUI throws
    /// "AutomationId may only be set one time", so the host's id can be neither cleared nor
    /// moved, and copying it would put a duplicate in the UIA tree that resolves to the
    /// non-invokable parent first. Setting it here, and NOT setting AutomationId, is what puts
    /// the id on the element holding the behaviour.
    /// </remarks>
    public static readonly BindableProperty ActivatorAutomationIdProperty =
        BindableProperty.Create(
            nameof(ActivatorAutomationId),
            typeof(string),
            typeof(ComboBoxControl),
            default(string),
            propertyChanged: OnActivatorAutomationIdChanged);

    public string? ActivatorAutomationId
    {
        get => (string?)GetValue(ActivatorAutomationIdProperty);
        set => SetValue(ActivatorAutomationIdProperty, value);
    }

    private static void OnActivatorAutomationIdChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        if (bindable is ComboBoxControl control && control._activator is not null && newValue is string id && !string.IsNullOrEmpty(id))
            control._activator.AutomationId = id;
    }

    private readonly Button? _activator;
    private bool _forwardingAutomation;

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
            var type = SelectedItem.GetType();
            var name = type.GetProperty(DisplayMemberPath)?.GetValue(SelectedItem)?.ToString() ?? "";
            var imagePath = type.GetProperty(ImageMemberPath)?.GetValue(SelectedItem)?.ToString();
            var imagePath2 = type.GetProperty(ImageMemberPath2)?.GetValue(SelectedItem)?.ToString();
            _selectedLabel.Text = name;
            _selectedIcon.Source = imagePath;
            _selectedIcon2.Source = imagePath2;
            _icon2Wrapper.IsVisible = !string.IsNullOrEmpty(imagePath2);
        }
        else
        {
            _placeholderLabel.IsVisible = true;
            _selectedLabel.IsVisible = false;
            _selectedIcon.Source = "ball_icon.png";
            _icon2Wrapper.IsVisible = false;
        }
    }

    private static async Task LogPopupEventAsync(string message)
    {
        try
        {
            string path = Path.Combine(FileSystem.Current.CacheDirectory, "combobox_popup.log");
            await File.AppendAllTextAsync(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Diagnostic log write is best-effort — losing a line must never break the popup.
        }
        catch (UnauthorizedAccessException)
        {
            // Same: cache dir may be briefly locked/unavailable during app teardown.
        }
        System.Diagnostics.Debug.WriteLine(message);
    }

    private async void OnTapped(object? sender, EventArgs e)
    {
        try
        {
            await LogPopupEventAsync($"[ComboBoxControl] OnTapped fired. AutomationId={AutomationId}, ItemsSource={(ItemsSource == null ? "NULL" : "set")}");
            if (ItemsSource == null)
            {
                await LogPopupEventAsync("[ComboBoxControl] OnTapped: ItemsSource null, returning");
                return;
            }

            var popup = new ComboBoxPopup(
                ItemsSource,
                DisplayMemberPath,
                ImageMemberPath,
                PopupBackgroundColor,
                PopupAccentColor,
                PopupWidth,
                ItemHeight,
                ImageMemberPath2);
            await LogPopupEventAsync("[ComboBoxControl] OnTapped: popup constructed, calling ShowPopupAsync");

            Page? currentPage = Shell.Current?.CurrentPage;
            await LogPopupEventAsync($"[ComboBoxControl] OnTapped: Shell.Current.CurrentPage={(currentPage == null ? "NULL" : currentPage.GetType().Name)}");
            if (currentPage == null)
            {
                await LogPopupEventAsync("[ComboBoxControl] OnTapped: no CurrentPage — aborting");
                return;
            }

            var popupResult = await currentPage.ShowPopupAsync<ComboBoxPopup.PickerResult?>(popup, new PopupOptions
            {
                Shape = null,
                Shadow = null
            });
            await LogPopupEventAsync($"[ComboBoxControl] OnTapped: ShowPopupAsync returned, result={(popupResult?.Result == null ? "null" : "picked")}");

            if (popupResult?.Result is ComboBoxPopup.PickerResult pickerResult && pickerResult.SelectedItem != null)
            {
                SelectedItem = pickerResult.SelectedItem;
            }
        }
        catch (Exception ex)
        {
            await LogPopupEventAsync($"[ComboBoxControl] OnTapped THREW: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
        }
    }
}