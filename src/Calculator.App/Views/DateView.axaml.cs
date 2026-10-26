using System;
using Avalonia;
using Avalonia.Controls;

namespace CalcApp.Views;

public partial class DateView : UserControl
{
    private bool _landscape;

    public DateView()
    {
        InitializeComponent();

        // The original calculator supports dates between 1601 and 9999.
        var min = new DateTimeOffset(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var max = new DateTimeOffset(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);
        foreach (DatePicker p in new[] { FromPicker, ToPicker, StartPicker })
        {
            p.MinYear = min;
            p.MaxYear = max;
        }
    }

    /// <summary>Sideways: the inputs are on the left and the result on the right, so that nothing needs scrolling.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        bool landscape = e.NewSize.Width >= 560 && e.NewSize.Width > e.NewSize.Height * 1.3;
        if (landscape == _landscape)
        {
            return;
        }

        _landscape = landscape;
        PageRoot.MaxWidth = landscape ? 900 : 560;
        Arrange(DiffGrid, DiffOutput, landscape);
        Arrange(AddGrid, AddOutput, landscape);
    }

    private static void Arrange(Grid grid, Control output, bool landscape)
    {
        if (landscape)
        {
            grid.ColumnDefinitions = ColumnDefinitions.Parse("*,*");
            grid.RowDefinitions = RowDefinitions.Parse("Auto");
            Grid.SetRow(output, 0);
            Grid.SetColumn(output, 1);
            output.Margin = new Thickness(24, 0, 0, 0);
        }
        else
        {
            grid.ColumnDefinitions = ColumnDefinitions.Parse("*");
            grid.RowDefinitions = RowDefinitions.Parse("Auto,Auto");
            Grid.SetRow(output, 1);
            Grid.SetColumn(output, 0);
            output.Margin = new Thickness(0, 22, 0, 0);
        }
    }
}
