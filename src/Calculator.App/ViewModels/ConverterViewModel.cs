using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Calculator.Engine;
using CalcApp.Localization;
using Calculator.Engine.UnitConversion;
using CalcApp.Services;

namespace CalcApp.ViewModels;

/// <summary>A unit as shown in the unit drop-downs.</summary>
public sealed class UnitItem
{
    public UnitItem(Unit unit)
    {
        Model = unit;
    }

    public Unit Model { get; }

    public int Id => Model.Id;

    public string Name => Model.Name;

    public string Abbreviation => Model.Abbreviation;

    public override string ToString() => Model.Name;
}

public sealed class SuggestedItem
{
    public SuggestedItem(string value, string unit)
    {
        Value = value;
        Unit = unit;
    }

    public string Value { get; }

    public string Unit { get; }
}

/// <summary>View model of the unit converters (volume, length, weight, ...).</summary>
public sealed class ConverterViewModel : ViewModelBase, IUnitConverterVMCallback
{
    private readonly UnitConverter _model;
    private readonly CurrencyConverterDataLoader _loader;
    private readonly CurrencyService _currency = CurrencyService.Instance;
    private readonly Loc _loc = Loc.Instance;

    private Category _category;
    private bool _supportsNegative;
    private bool _value1IsSource = true;
    private string _fromRaw = "0";
    private string _toRaw = "0";
    private string _value1 = "0";
    private string _value2 = "0";
    private UnitItem? _unit1;
    private UnitItem? _unit2;
    private IReadOnlyList<UnitItem> _units = new List<UnitItem>();
    private bool _isUpdatingUnits;
    private string _title = string.Empty;
    private List<(string Value, Unit Unit)> _lastSuggested = new List<(string Value, Unit Unit)>();

    public ConverterViewModel()
    {
        string? region = null;
        string? homeCurrency = null;
        try
        {
            RegionInfo info = RegionInfo.CurrentRegion;
            region = info.TwoLetterISORegionName;
            homeCurrency = info.ISOCurrencySymbol;
        }
        catch
        {
            // ignored: default region handling in the data loader
        }

        var units = new UnitConverterDataLoader(key => _loc.Get(key), region);
        _loader = new CurrencyConverterDataLoader(units, key => _loc.Get(key), () => _currency.Current, homeCurrency);
        _model = new UnitConverter(_loader);
        _model.Initialize();
        _model.SetViewModelCallback(this);

        PressCommand = new RelayCommand(p =>
        {
            if (p is UnitConversionCommand c)
            {
                Press(c);
            }
        });
        SwitchActiveCommand = new RelayCommand(p =>
        {
            string which = p as string ?? "1";
            if ((which == "1") != _value1IsSource)
            {
                SwitchActive();
            }
        });
        SwapCommand = new RelayCommand(SwitchActive);
        RefreshCurrencyCommand = new RelayCommand(() => _ = RefreshCurrencyAsync(true));
        _currency.Changed += OnCurrencyChanged;
    }

    public IReadOnlyList<Category> Categories => _model.GetCategories();

    public ICommand PressCommand { get; }

    public ICommand SwitchActiveCommand { get; }

    public ICommand SwapCommand { get; }

    public ICommand RefreshCurrencyCommand { get; }

    /// <summary>True while the currency category is shown (the page then shows the status of the rates).</summary>
    public bool IsCurrency => _category.Id == (int)ConverterCategory.Currency;

    /// <summary>True while the rates are being downloaded.</summary>
    public bool IsCurrencyBusy => _currency.State == CurrencyState.Refreshing;

    /// <summary>Describes the rates in use: their date, or why there are none.</summary>
    public string CurrencyStatus
    {
        get
        {
            CurrencyRates? rates = _currency.Current;
            string date = rates == null ? string.Empty : FormatRatesDate(rates);
            if (_currency.State == CurrencyState.Refreshing)
            {
                return _loc.Get("CurrencyRefreshing");
            }

            if (rates == null)
            {
                return _loc.Get("CurrencyNoRates");
            }

            return _currency.State == CurrencyState.Offline
                ? _loc.Format("CurrencyOffline", date)
                : _loc.Format("CurrencyUpdated", date);
        }
    }

    private string FormatRatesDate(CurrencyRates rates)
    {
        if (DateTime.TryParseExact(rates.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime d))
        {
            return d.ToString("d", _loc.Culture);
        }

        return rates.FetchedUtc == DateTime.MinValue ? rates.Date : rates.FetchedUtc.ToLocalTime().ToString("d", _loc.Culture);
    }

    public ObservableCollection<SuggestedItem> SuggestedValues { get; } = new ObservableCollection<SuggestedItem>();

    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    public IReadOnlyList<UnitItem> Units
    {
        get => _units;
        private set => SetProperty(ref _units, value);
    }

    public bool SupportsNegative
    {
        get => _supportsNegative;
        private set => SetProperty(ref _supportsNegative, value);
    }

    public string Value1
    {
        get => _value1;
        private set => SetProperty(ref _value1, value);
    }

    public string Value2
    {
        get => _value2;
        private set => SetProperty(ref _value2, value);
    }

    public bool IsValue1Active => _value1IsSource;

    public bool IsValue2Active => !_value1IsSource;

    public UnitItem? Unit1
    {
        get => _unit1;
        set
        {
            if (value == null || SetProperty(ref _unit1, value) == false)
            {
                return;
            }

            if (!_isUpdatingUnits)
            {
                OnUnitChanged();
            }
        }
    }

    public UnitItem? Unit2
    {
        get => _unit2;
        set
        {
            if (value == null || SetProperty(ref _unit2, value) == false)
            {
                return;
            }

            if (!_isUpdatingUnits)
            {
                OnUnitChanged();
            }
        }
    }

    private UnitItem? UnitFrom => _value1IsSource ? _unit1 : _unit2;

    private UnitItem? UnitTo => _value1IsSource ? _unit2 : _unit1;

    /// <summary>Selects the converter category (one of <see cref="ConverterCategory"/>).</summary>
    public void SetCategory(int categoryId)
    {
        ApplyCategory(categoryId);
        if (categoryId == (int)ConverterCategory.Currency)
        {
            _ = RefreshCurrencyAsync(false);
        }
    }

    private void ApplyCategory(int categoryId)
    {
        Category? found = null;
        foreach (Category c in _model.GetCategories())
        {
            if (c.Id == categoryId)
            {
                found = c;
                break;
            }
        }

        if (found == null)
        {
            return;
        }

        bool isCurrency = found.Value.Id == (int)ConverterCategory.Currency;
        if (isCurrency && _loader.IsStale)
        {
            // New rates arrived since the units were built: rebuild the data of the converter.
            _model.ResetCategoriesAndRatios();
        }

        _category = found.Value;
        Title = _category.Name;
        SupportsNegative = _category.SupportsNegative;
        OnPropertyChanged(nameof(IsCurrency));
        OnPropertyChanged(nameof(CurrencyStatus));
        OnPropertyChanged(nameof(IsCurrencyBusy));

        // The model always works with "from" being the active field: reset our mapping.
        _value1IsSource = true;
        OnPropertyChanged(nameof(IsValue1Active));
        OnPropertyChanged(nameof(IsValue2Active));

        (List<Unit> units, Unit fromModel, Unit toModel) = _model.SetCurrentCategory(_category);
        _isUpdatingUnits = true;
        try
        {
            var items = units.Select(u => new UnitItem(u)).ToList();
            Units = items;
            _unit1 = items.FirstOrDefault(i => i.Id == fromModel.Id);
            _unit2 = items.FirstOrDefault(i => i.Id == toModel.Id);
            OnPropertyChanged(nameof(Unit1));
            OnPropertyChanged(nameof(Unit2));
        }
        finally
        {
            _isUpdatingUnits = false;
        }

        OnUnitChanged();
    }

    private async System.Threading.Tasks.Task RefreshCurrencyAsync(bool force)
    {
        try
        {
            await _currency.RefreshAsync(force);
        }
        catch (Exception)
        {
            // RefreshAsync reports failures through its state.
        }
    }

    private void OnCurrencyChanged()
    {
        OnPropertyChanged(nameof(CurrencyStatus));
        OnPropertyChanged(nameof(IsCurrencyBusy));
        if (!IsCurrency || _currency.State == CurrencyState.Refreshing || !_loader.IsStale)
        {
            return;
        }

        // The rates changed while the currency page is shown: rebuild the units and keep the chosen currencies.
        string? fromCode = _unit1?.Abbreviation;
        string? toCode = _unit2?.Abbreviation;
        int categoryId = _category.Id;
        _category = default;
        ApplyCategory(categoryId);
        if (fromCode != null && toCode != null)
        {
            UnitItem? a = _units.FirstOrDefault(u => u.Abbreviation == fromCode);
            UnitItem? b = _units.FirstOrDefault(u => u.Abbreviation == toCode);
            if (a != null && b != null)
            {
                _isUpdatingUnits = true;
                try
                {
                    _unit1 = a;
                    _unit2 = b;
                    OnPropertyChanged(nameof(Unit1));
                    OnPropertyChanged(nameof(Unit2));
                }
                finally
                {
                    _isUpdatingUnits = false;
                }

                OnUnitChanged();
            }
        }
    }

    private void OnUnitChanged()
    {
        UnitItem? source = UnitFrom;
        UnitItem? target = UnitTo;
        if (source == null || target == null)
        {
            return;
        }

        _model.SetCurrentUnitTypes(source.Model, target.Model);
    }

    private void SwitchActive()
    {
        _value1IsSource = !_value1IsSource;
        (_fromRaw, _toRaw) = (_toRaw, _fromRaw);
        _model.SwitchActive(string.IsNullOrEmpty(_fromRaw) ? "0" : _fromRaw);
        OnPropertyChanged(nameof(IsValue1Active));
        OnPropertyChanged(nameof(IsValue2Active));
        RefreshSuggested();
    }

    public void Press(UnitConversionCommand command) => _model.SendCommand(command);

    public bool TryMapCharacter(char ch, out UnitConversionCommand command)
    {
        command = UnitConversionCommand.None;
        if (ch >= '0' && ch <= '9')
        {
            command = (UnitConversionCommand)(ch - '0');
            return true;
        }

        if (ch == '.' || ch == ',' || ch == _loc.Culture.NumberFormat.NumberDecimalSeparator[0])
        {
            command = UnitConversionCommand.Decimal;
            return true;
        }

        if (ch == '-' && _supportsNegative)
        {
            command = UnitConversionCommand.Negate;
            return true;
        }

        return false;
    }

    public bool HandleText(string text)
    {
        bool any = false;
        foreach (char ch in text)
        {
            if (TryMapCharacter(ch, out UnitConversionCommand c))
            {
                Press(c);
                any = true;
            }
        }

        return any;
    }

    public void PressBackspace() => Press(UnitConversionCommand.Backspace);

    public void PressClear() => Press(UnitConversionCommand.Clear);

    /// <summary>The raw (invariant) text of the active value, suitable for the clipboard.</summary>
    public string ActiveValueForCopy => _value1IsSource ? _value1 : _value2;

    public bool Paste(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string t = text.Trim();
        bool negative = false;
        var commands = new List<UnitConversionCommand>();
        foreach (char ch in t)
        {
            if (char.IsWhiteSpace(ch) || ch == ' ' || ch == ' ')
            {
                continue;
            }

            if (ch == '-' && commands.Count == 0)
            {
                negative = true;
                continue;
            }

            if (ch == '+' && commands.Count == 0)
            {
                continue;
            }

            if (ch == ',' && _loc.Culture.NumberFormat.NumberDecimalSeparator != ",")
            {
                continue; // group separator
            }

            if (!TryMapCharacter(ch, out UnitConversionCommand c) || c == UnitConversionCommand.Negate)
            {
                return false;
            }

            commands.Add(c);
        }

        if (commands.Count == 0)
        {
            return false;
        }

        Press(UnitConversionCommand.Clear);
        foreach (UnitConversionCommand c in commands)
        {
            Press(c);
        }

        if (negative && _supportsNegative)
        {
            Press(UnitConversionCommand.Negate);
        }

        return true;
    }

    // ------------------------------------------------------------------ IUnitConverterVMCallback

    void IUnitConverterVMCallback.DisplayCallback(string fromValue, string toValue)
    {
        _fromRaw = fromValue;
        _toRaw = toValue;
        Value1 = Localize(_value1IsSource ? fromValue : toValue);
        Value2 = Localize(_value1IsSource ? toValue : fromValue);
    }

    void IUnitConverterVMCallback.SuggestedValueCallback(List<(string Value, Unit Unit)> suggestedValues)
    {
        _lastSuggested = suggestedValues;
        RefreshSuggested();
    }

    void IUnitConverterVMCallback.MaxDigitsReached()
    {
    }

    private void RefreshSuggested()
    {
        SuggestedValues.Clear();
        foreach ((string value, Unit unit) in _lastSuggested)
        {
            SuggestedValues.Add(new SuggestedItem(Localize(value), unit.Abbreviation));
        }
    }

    /// <summary>Converts an invariant number string ("-1234.5", "1.5e+10") to the display format of the current culture.</summary>
    private string Localize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        NumberFormatInfo nfi = _loc.Culture.NumberFormat;
        int expPos = value.IndexOfAny(new[] { 'e', 'E' });
        string mantissa = expPos >= 0 ? value.Substring(0, expPos) : value;
        string exponent = expPos >= 0 ? value.Substring(expPos) : string.Empty;

        bool negative = mantissa.StartsWith("-", StringComparison.Ordinal);
        if (negative)
        {
            mantissa = mantissa.Substring(1);
        }

        string integer = mantissa;
        string fraction = string.Empty;
        bool hasDecimal = false;
        int dot = mantissa.IndexOf('.');
        if (dot >= 0)
        {
            integer = mantissa.Substring(0, dot);
            fraction = mantissa.Substring(dot + 1);
            hasDecimal = true;
        }

        int groupSize = nfi.NumberGroupSizes.Length > 0 ? nfi.NumberGroupSizes[0] : 0;
        if (groupSize > 0 && integer.Length > groupSize)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < integer.Length; i++)
            {
                if (i > 0 && (integer.Length - i) % groupSize == 0)
                {
                    sb.Append(nfi.NumberGroupSeparator);
                }

                sb.Append(integer[i]);
            }

            integer = sb.ToString();
        }

        var result = new StringBuilder();
        if (negative)
        {
            result.Append('-');
        }

        result.Append(integer);
        if (hasDecimal)
        {
            result.Append(nfi.NumberDecimalSeparator).Append(fraction);
        }

        result.Append(exponent);
        return result.ToString();
    }
}
