using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CalcApp.ViewModels;

namespace CalcApp.Views;

public partial class ConverterView : UserControl
{
    public ConverterView()
    {
        InitializeComponent();
    }

    private bool _landscape;

    /// <summary>Sideways (phone in landscape or wide window): values on the left, keypad on the right.</summary>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        bool landscape = e.NewSize.Width >= 480 && e.NewSize.Width > e.NewSize.Height * 1.15;
        if (landscape == _landscape && Root.ColumnDefinitions.Count > 0)
        {
            return;
        }

        _landscape = landscape;
        if (landscape)
        {
            Root.ColumnDefinitions = ColumnDefinitions.Parse("*,*");
            Root.RowDefinitions = RowDefinitions.Parse("*");
            Root.MaxWidth = double.PositiveInfinity;
            Grid.SetRow(Info, 0);
            Grid.SetColumn(Info, 0);
            Grid.SetRow(Keypad, 0);
            Grid.SetColumn(Keypad, 1);
            Info.Margin = new Thickness(0, 0, 8, 0);
            Value1Box.Height = 42;
            Value2Box.Height = 42;
            Value1Text.FontSize = 30;
            Value2Text.FontSize = 30;
            Suggested.Height = 40;
        }
        else
        {
            Root.ColumnDefinitions = ColumnDefinitions.Parse("*");
            Root.RowDefinitions = RowDefinitions.Parse("Auto,*");
            Root.MaxWidth = 640;
            Grid.SetRow(Info, 0);
            Grid.SetColumn(Info, 0);
            Grid.SetRow(Keypad, 1);
            Grid.SetColumn(Keypad, 0);
            Info.Margin = new Thickness(0);
            Value1Box.Height = 64;
            Value2Box.Height = 64;
            Value1Text.FontSize = 44;
            Value2Text.FontSize = 44;
            Suggested.Height = 56;
        }
    }

    private void OnTapValue1(object? sender, TappedEventArgs e) => Activate("1");

    private void OnTapValue2(object? sender, TappedEventArgs e) => Activate("2");

    private void Activate(string which)
    {
        if (DataContext is ConverterViewModel vm)
        {
            vm.SwitchActiveCommand.Execute(which);
        }
    }
}
