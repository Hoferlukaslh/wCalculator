using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Calculator.Engine;
using CalcApp.Localization;
using Calculator.Engine.UnitConversion;

namespace CalcApp.ViewModels;

public enum NavKind
{
    Standard,
    Scientific,
    Programmer,
    Date,
    Converter,
    Settings,
}

public sealed class NavItem
{
    public NavItem(string title, NavKind kind, int categoryId = 0, string glyph = "")
    {
        Title = title;
        Kind = kind;
        CategoryId = categoryId;
        Glyph = glyph;
    }

    public string Title { get; }

    public NavKind Kind { get; }

    public int CategoryId { get; }

    public string Glyph { get; }

    public override string ToString() => Title;
}

/// <summary>Root view model: owns the navigation list and the active page.</summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly Loc _loc = Loc.Instance;
    private readonly CalculatorViewModel _calculator;
    private readonly DateViewModel _date;
    private readonly ConverterViewModel _converter;
    private readonly SettingsViewModel _settings;
    private NavItem? _selected;
    private object? _current;
    private bool _isPaneOpen;

    public MainViewModel()
    {
        _calculator = new CalculatorViewModel();
        _date = new DateViewModel();
        _converter = new ConverterViewModel();
        _settings = new SettingsViewModel();

        var items = new List<NavItem>
        {
            new NavItem(_loc.Get("Standard"), NavKind.Standard, 0, Icons.Calculator),
            new NavItem(_loc.Get("Scientific"), NavKind.Scientific, 0, Icons.Function),
            new NavItem(_loc.Get("Programmer"), NavKind.Programmer, 0, Icons.Code),
            new NavItem(_loc.Get("DateCalculation"), NavKind.Date, 0, Icons.CalendarBlank),
        };

        foreach (Category c in _converter.Categories)
        {
            items.Add(new NavItem(c.Name, NavKind.Converter, c.Id, Icons.ForConverterCategory(c.Id)));
        }

        NavItems = new ObservableCollection<NavItem>(items);
        SettingsItem = new NavItem(_loc.Get("Settings"), NavKind.Settings, 0, Icons.Gear);
        OpenSettingsCommand = new RelayCommand(() => Selected = SettingsItem);
        Selected = NavItems[0];
    }

    /// <summary>The settings entry, shown at the bottom of the navigation pane (it is not part of <see cref="NavItems"/>).</summary>
    public NavItem SettingsItem { get; }

    public System.Windows.Input.ICommand OpenSettingsCommand { get; }

    public bool IsSettingsSelected => _selected == SettingsItem;

    /// <summary>The copy / paste buttons of the title bar make no sense on the settings page.</summary>
    public bool ShowClipboardButtons => _selected != SettingsItem;

    /// <summary>The selection shown by the navigation list: empty while the settings are open.</summary>
    public NavItem? ListSelected
    {
        get => _selected == SettingsItem ? null : _selected;
        set
        {
            if (value != null)
            {
                Selected = value;
            }
        }
    }

    public ObservableCollection<NavItem> NavItems { get; }

    public CalculatorViewModel Calculator => _calculator;

    public NavItem? Selected
    {
        get => _selected;
        set
        {
            if (value == null || !SetProperty(ref _selected, value))
            {
                return;
            }

            Navigate(value);
            OnPropertyChanged(nameof(ListSelected));
            OnPropertyChanged(nameof(IsSettingsSelected));
            OnPropertyChanged(nameof(ShowClipboardButtons));
        }
    }

    public object? Current
    {
        get => _current;
        private set => SetProperty(ref _current, value);
    }

    public bool IsPaneOpen
    {
        get => _isPaneOpen;
        set => SetProperty(ref _isPaneOpen, value);
    }

    public string HeaderTitle => _selected?.Title ?? string.Empty;

    private void Navigate(NavItem item)
    {
        switch (item.Kind)
        {
            case NavKind.Standard:
                _calculator.SetKind(CalcKind.Standard);
                Current = _calculator;
                break;
            case NavKind.Scientific:
                _calculator.SetKind(CalcKind.Scientific);
                Current = _calculator;
                break;
            case NavKind.Programmer:
                _calculator.SetKind(CalcKind.Programmer);
                Current = _calculator;
                break;
            case NavKind.Date:
                Current = _date;
                break;
            case NavKind.Settings:
                Current = _settings;
                break;
            default:
                _converter.SetCategory(item.CategoryId);
                Current = _converter;
                break;
        }

        OnPropertyChanged(nameof(HeaderTitle));
    }
}
