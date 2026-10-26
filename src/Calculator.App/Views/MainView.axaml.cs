using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CalcApp.Services;
using CalcApp.ViewModels;

namespace CalcApp.Views;

public partial class MainView : UserControl
{
    private const double WideThreshold = 1000;
    private bool _wide;

    public MainView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => Focus());
        DataContextChanged += (_, _) => HookViewModel();
        // Vibration when a key is touched (mobile only; handled events count too, a button handles its own press).
        AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, true);
        AttachedToVisualTree += (_, _) => AppearanceManager.Changed += UpdateBlur;
        DetachedFromVisualTree += (_, _) => AppearanceManager.Changed -= UpdateBlur;
    }

    private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!Haptics.IsAvailable || e.Source is not Visual source)
        {
            return;
        }

        for (Visual? v = source; v != null && !ReferenceEquals(v, this); v = v.GetVisualParent())
        {
            if (v is Button { IsEffectivelyEnabled: true } and not RepeatButton)
            {
                Haptics.Buzz();
                return;
            }
        }
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private MainViewModel? _hooked;

    private void HookViewModel()
    {
        if (_hooked != null)
        {
            _hooked.PropertyChanged -= OnVmPropertyChanged;
        }

        _hooked = DataContext as MainViewModel;
        if (_hooked != null)
        {
            _hooked.PropertyChanged += OnVmPropertyChanged;
        }

        UpdateBlur();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.IsPaneOpen) or nameof(MainViewModel.Current))
        {
            if (e.PropertyName == nameof(MainViewModel.Current))
            {
                ReleaseSnapshot();
            }

            UpdateBlur();
        }
    }

    private RenderTargetBitmap? _snapshot;

    /// <summary>
    /// While the menu floats over the page (small windows), a snapshot of the page taken when the menu opened is drawn
    /// blurred behind it, so only the part of the page under the menu is blurred. (A second live copy of the page would
    /// have its own scroll position and state, and show something different from the page.)
    /// </summary>
    private void UpdateBlur()
    {
        int radius = SettingsService.Instance.Current.MenuBlur;
        bool overlay = Vm?.IsPaneOpen == true && !_wide;
        int transparency = SettingsService.Instance.Current.MenuTransparency;
        if (overlay && transparency > 0)
        {
            // The page is drawn behind the menu (blurred, or sharp when the blur is 0) as far as the menu is transparent.
            if (_snapshot == null)
            {
                TakeSnapshot();
            }

            PaneCopy.Effect = radius > 0 ? new BlurEffect { Radius = radius } : null;
        }
        else
        {
            ReleaseSnapshot();
        }

        // The menu is always drawn over an opaque base; the page only shows through as far as the transparency allows.
        PaneBlur.IsVisible = overlay;
        PaneTint.IsVisible = overlay;

        // With a menu transparency of 0 nothing of the page may be seen through the menu, whatever the blur.
        PaneCopy.Opacity = transparency / 100.0;
    }

    private void TakeSnapshot()
    {
        double width = ContentHost.Bounds.Width;
        double height = ContentHost.Bounds.Height;
        if (width < 1 || height < 1)
        {
            return;
        }

        try
        {
            // One bitmap pixel per layout unit, at 96 dpi: whatever the screen density, the page is then drawn at its
            // natural size into the bitmap. (With a bigger bitmap and a dpi of 96 * density, Android drew the page
            // at 1:1 into the top-left corner of the bitmap, so the snapshot showed an enlarged part of the page.)
            // The bitmap is stretched over the menu afterwards; the blur hides the lower resolution.
            var size = new PixelSize(Math.Max(1, (int)Math.Ceiling(width)), Math.Max(1, (int)Math.Ceiling(height)));
            var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
            bitmap.Render(ContentHost);
            _snapshot = bitmap;
            PaneCopy.Width = size.Width;
            PaneCopy.Height = size.Height;
            RenderOptions.SetBitmapInterpolationMode(PaneCopy, BitmapInterpolationMode.HighQuality);
            PaneCopy.Source = bitmap;
        }
        catch (Exception)
        {
            // No blur if the platform cannot render the page into a bitmap.
            ReleaseSnapshot();
        }
    }

    private void ReleaseSnapshot()
    {
        PaneCopy.Source = null;
        _snapshot?.Dispose();
        _snapshot = null;
    }

    private bool _refreshQueued;

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        // The snapshot behind the menu shows the page at its old size: take a new one as soon as the new layout is
        // done (at render priority, once per frame), so that it follows the window while it is being resized.
        if (PaneBlur.IsVisible && !_refreshQueued)
        {
            _refreshQueued = true;
            Dispatcher.UIThread.Post(
                () =>
                {
                    _refreshQueued = false;
                    ReleaseSnapshot();
                    UpdateBlur();
                },
                DispatcherPriority.Render);
        }

        // A phone held sideways has very little height: use a thinner title bar to leave room for the keypad.
        TopBar.Height = e.NewSize.Height < 480 ? 40 : 48;

        bool wide = e.NewSize.Width >= WideThreshold;
        if (wide != _wide)
        {
            _wide = wide;
            Split.DisplayMode = wide ? SplitViewDisplayMode.Inline : SplitViewDisplayMode.Overlay;
            UpdateBlur();
            if (Vm != null)
            {
                Vm.IsPaneOpen = wide;
            }
        }
    }

    private void OnTogglePane(object? sender, RoutedEventArgs e)
    {
        if (Vm != null)
        {
            Vm.IsPaneOpen = !Vm.IsPaneOpen;
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        if (!_wide && Vm != null)
        {
            Vm.IsPaneOpen = false;
        }
    }

    private void OnNavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_wide && Vm != null)
        {
            Vm.IsPaneOpen = false;
        }

        Dispatcher.UIThread.Post(() => Focus());
    }

    // ------------------------------------------------------------------ keyboard

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        switch (Vm?.Current)
        {
            case CalculatorViewModel calc:
                e.Handled = calc.HandleText(e.Text);
                break;
            case ConverterViewModel conv:
                e.Handled = conv.HandleText(e.Text);
                break;
        }
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (ctrl && e.Key == Key.C)
        {
            await CopyAsync();
            e.Handled = true;
            return;
        }

        if (ctrl && e.Key == Key.V)
        {
            await PasteAsync();
            e.Handled = true;
            return;
        }

        switch (Vm?.Current)
        {
            case CalculatorViewModel calc:
                switch (e.Key)
                {
                    case Key.Enter:
                        calc.PressEquals();
                        e.Handled = true;
                        break;
                    case Key.Escape:
                        calc.PressClear();
                        e.Handled = true;
                        break;
                    case Key.Back:
                        calc.PressBackspace();
                        e.Handled = true;
                        break;
                    case Key.Delete:
                        calc.PressClearEntry();
                        e.Handled = true;
                        break;
                }

                break;
            case ConverterViewModel conv:
                switch (e.Key)
                {
                    case Key.Escape:
                    case Key.Delete:
                        conv.PressClear();
                        e.Handled = true;
                        break;
                    case Key.Back:
                        conv.PressBackspace();
                        e.Handled = true;
                        break;
                }

                break;
        }
    }

    // ------------------------------------------------------------------ clipboard

    private async System.Threading.Tasks.Task CopyAsync()
    {
        string? text = Vm?.Current switch
        {
            CalculatorViewModel calc => calc.DisplayValue,
            DateViewModel date => date.ResultToCopy,
            ConverterViewModel conv => conv.ActiveValueForCopy,
            _ => null,
        };

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null && text != null)
        {
            try
            {
                await clipboard.SetTextAsync(text);
            }
            catch
            {
                // Clipboard not available.
            }
        }
    }

    private async System.Threading.Tasks.Task PasteAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null)
        {
            return;
        }

        string? text;
        try
        {
            text = await clipboard.TryGetTextAsync();
        }
        catch
        {
            return;
        }

        switch (Vm?.Current)
        {
            case CalculatorViewModel calc:
                calc.Paste(text);
                break;
            case ConverterViewModel conv:
                conv.Paste(text);
                break;
        }
    }

    private async void OnCopy(object? sender, RoutedEventArgs e) => await CopyAsync();

    private async void OnPaste(object? sender, RoutedEventArgs e) => await PasteAsync();
}
