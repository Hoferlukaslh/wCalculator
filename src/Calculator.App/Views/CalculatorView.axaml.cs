using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using CalcApp.Services;
using CalcApp.ViewModels;

namespace CalcApp.Views;

public partial class CalculatorView : UserControl
{
    private const double WideThreshold = 520;
    private CalculatorViewModel? _vm;

    public CalculatorView()
    {
        StartupTrace.Mark("calculator-view-start");
        InitializeComponent();
        StartupTrace.Mark("calculator-view-loaded");
        DataContextChanged += (_, _) => HookViewModel();
        HookViewModel();
    }

    private void HookViewModel()
    {
        if (_vm != null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = DataContext as CalculatorViewModel;
        if (_vm != null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }

        UpdatePadLayout();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CalculatorViewModel.Kind) or nameof(CalculatorViewModel.IsBitFlip)
            or nameof(CalculatorViewModel.HasSidePanel) or nameof(CalculatorViewModel.IsKeypadVisible))
        {
            UpdatePadLayout();
        }
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateRootLayout();
        UpdatePadLayout();
    }

    /// <summary>
    /// A phone held sideways (or a wide window) is wider than it is high: the display goes to the left and the
    /// keypads to the right, instead of stacking them (which squeezes the keys into a thin strip).
    /// </summary>
    private bool IsLandscape => Bounds.Width >= 480 && Bounds.Width > Bounds.Height * 1.15;

    private bool _landscape;
    private bool _landscapeWide;

    private void UpdateRootLayout()
    {
        if (Root == null)
        {
            return;
        }

        bool landscape = IsLandscape;
        bool wideKeys = _vm?.HasSidePanel ?? false;
        if (landscape == _landscape && wideKeys == _landscapeWide && Root.ColumnDefinitions.Count > 0)
        {
            return;
        }

        _landscape = landscape;
        _landscapeWide = wideKeys;

        if (landscape)
        {
            // The scientific and programmer pads need more room than the standard one.
            Root.ColumnDefinitions = ColumnDefinitions.Parse(wideKeys ? "3*,7*" : "5*,6*");
            Root.RowDefinitions = RowDefinitions.Parse("*");
            Place(DisplayPanel, 0, 0, 1, 1);
            Place(PadHost, 0, 1, 1, 1);
            Place(HistoryFlyout, 0, 1, 1, 1);
            DisplayPanel.Margin = new Thickness(4, 0, 10, 6);
            DispText.FontSize = 40;
            DispBox.Height = 56;
        }
        else
        {
            Root.ColumnDefinitions = ColumnDefinitions.Parse("*");
            Root.RowDefinitions = RowDefinitions.Parse("Auto,*");
            Place(DisplayPanel, 0, 0, 1, 1);
            Place(PadHost, 1, 0, 1, 1);
            Place(HistoryFlyout, 1, 0, 1, 1);
            DisplayPanel.Margin = new Thickness(4, 0, 4, 6);
            DispText.FontSize = 54;
            DispBox.Height = 80;
        }
    }

    /// <summary>Places the side panel (scientific / programmer functions) beside or above the main keypad.</summary>
    private void UpdatePadLayout()
    {
        if (PadHost == null || SidePanel == null || MainPad == null)
        {
            return;
        }

        UpdateRootLayout();

        bool hasSide = _vm?.HasSidePanel ?? false;
        bool mainVisible = _vm?.IsKeypadVisible ?? true;

        // Side by side when sideways, or when the portrait view is wide enough for nine columns of keys.
        bool wide = _landscape || (Bounds.Width - 16) >= WideThreshold;

        if (!hasSide)
        {
            Place(MainPad, 0, 0, 2, 2);
            return;
        }

        if (!mainVisible)
        {
            Place(SidePanel, 0, 0, 2, 2);
            return;
        }

        if (wide)
        {
            Place(SidePanel, 0, 0, 2, 1);
            Place(MainPad, 0, 1, 2, 1);
        }
        else
        {
            Place(SidePanel, 0, 0, 1, 2);
            Place(MainPad, 1, 0, 1, 2);
        }
    }

    private static void Place(Control c, int row, int column, int rowSpan, int columnSpan)
    {
        Grid.SetRow(c, row);
        Grid.SetColumn(c, column);
        Grid.SetRowSpan(c, rowSpan);
        Grid.SetColumnSpan(c, columnSpan);
    }
}
