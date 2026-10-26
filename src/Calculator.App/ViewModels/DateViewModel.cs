using System;
using System.Collections.Generic;
using System.Globalization;
using Calculator.Engine;
using CalcApp.Localization;
using Calculator.Engine.DateCalculation;

namespace CalcApp.ViewModels;

/// <summary>Date calculation: difference between two dates, or add / subtract a duration.</summary>
public sealed class DateViewModel : ViewModelBase
{
    private const int MaxOffsetValue = 999;

    private readonly DateCalculationEngine _engine = new DateCalculationEngine();
    private readonly Loc _loc = Loc.Instance;
    private readonly DateUnit _daysOutputFormat = DateUnit.Day;
    private readonly DateUnit _allDateUnitsOutputFormat = DateUnit.Year | DateUnit.Month | DateUnit.Week | DateUnit.Day;

    private int _modeIndex;
    private bool _isAddMode = true;
    private int _years;
    private int _months;
    private int _days;
    private DateTimeOffset? _fromDate;
    private DateTimeOffset? _toDate;
    private DateTimeOffset? _startDate;
    private DateTime _dateResult;
    private bool _isOutOfBound;
    private DateDifference _diff = DateCalculationEngine.DateDifferenceUnknown;
    private DateDifference _diffInDays = DateCalculationEngine.DateDifferenceUnknown;
    private bool _isDiffInDays;
    private string _diffResult = string.Empty;
    private string _diffResultInDays = string.Empty;
    private string _dateResultText = string.Empty;

    public DateViewModel()
    {
        DateTime today = DateTime.Today;
        var d = new DateTimeOffset(today.Year, today.Month, today.Day, 0, 0, 0, TimeSpan.Zero);
        _fromDate = d;
        _toDate = d;
        _startDate = d;
        _dateResult = today;
        Recompute();
    }

    public string Title => _loc.Get("DateCalculation");

    public int ModeIndex
    {
        get => _modeIndex;
        set
        {
            if (SetProperty(ref _modeIndex, value))
            {
                OnPropertyChanged(nameof(IsDateDiffMode));
                OnPropertyChanged(nameof(IsAddSubtractMode));
                Recompute();
            }
        }
    }

    public bool IsDateDiffMode => _modeIndex == 0;

    public bool IsAddSubtractMode => _modeIndex != 0;

    public bool IsAddMode
    {
        get => _isAddMode;
        set
        {
            if (SetProperty(ref _isAddMode, value))
            {
                OnPropertyChanged(nameof(IsSubtractMode));
                Recompute();
            }
        }
    }

    public bool IsSubtractMode
    {
        get => !_isAddMode;
        set => IsAddMode = !value;
    }

    public decimal? YearsOffset
    {
        get => _years;
        set
        {
            if (SetProperty(ref _years, Clamp(value)))
            {
                Recompute();
            }
        }
    }

    public decimal? MonthsOffset
    {
        get => _months;
        set
        {
            if (SetProperty(ref _months, Clamp(value)))
            {
                Recompute();
            }
        }
    }

    public decimal? DaysOffset
    {
        get => _days;
        set
        {
            if (SetProperty(ref _days, Clamp(value)))
            {
                Recompute();
            }
        }
    }

    private static int Clamp(decimal? v) => v == null ? 0 : (int)Math.Max(0, Math.Min(MaxOffsetValue, v.Value));

    public DateTimeOffset? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                Recompute();
            }
        }
    }

    public DateTimeOffset? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                Recompute();
            }
        }
    }

    public DateTimeOffset? StartDate
    {
        get => _startDate;
        set
        {
            if (SetProperty(ref _startDate, value))
            {
                Recompute();
            }
        }
    }

    public bool IsDiffInDays
    {
        get => _isDiffInDays;
        private set => SetProperty(ref _isDiffInDays, value);
    }

    public string DiffResult
    {
        get => _diffResult;
        private set => SetProperty(ref _diffResult, value);
    }

    public string DiffResultInDays
    {
        get => _diffResultInDays;
        private set => SetProperty(ref _diffResultInDays, value);
    }

    public bool HasDiffResultInDays => !string.IsNullOrEmpty(_diffResultInDays);

    public string DateResultText
    {
        get => _dateResultText;
        private set => SetProperty(ref _dateResultText, value);
    }

    /// <summary>The text that should be copied to the clipboard.</summary>
    public string ResultToCopy => IsDateDiffMode ? DiffResult : DateResultText;

    private static DateTime Clip(DateTimeOffset? d) =>
        d == null ? DateTime.Today : new DateTime(d.Value.Year, d.Value.Month, d.Value.Day);

    private void Recompute()
    {
        if (IsDateDiffMode)
        {
            DateTime from = Clip(_fromDate);
            DateTime to = Clip(_toDate);
            DateDifference? inDays = _engine.TryGetDateDifference(from, to, _daysOutputFormat);
            if (inDays.HasValue)
            {
                _diffInDays = inDays.Value;
                _diff = _engine.TryGetDateDifference(from, to, _allDateUnitsOutputFormat) ?? _diffInDays;
            }
            else
            {
                _diff = DateCalculationEngine.DateDifferenceUnknown;
                _diffInDays = DateCalculationEngine.DateDifferenceUnknown;
            }
        }
        else
        {
            _isOutOfBound = false;
            var duration = new DateDifference(_years, _months, 0, _days);
            DateTime start = Clip(_startDate);
            DateTime? result = _isAddMode ? _engine.AddDuration(start, duration) : _engine.SubtractDuration(start, duration);
            if (result.HasValue)
            {
                _dateResult = result.Value;
            }
            else
            {
                _isOutOfBound = true;
            }
        }

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (IsDateDiffMode)
        {
            if (_diffInDays == DateCalculationEngine.DateDifferenceUnknown)
            {
                IsDiffInDays = false;
                DiffResultInDays = string.Empty;
                DiffResult = _loc.GetOr("CalculationFailed", "Calculation failed");
            }
            else if (_diffInDays.Day == 0)
            {
                IsDiffInDays = true;
                DiffResultInDays = string.Empty;
                DiffResult = _loc.GetOr("Date_SameDates", "Same dates");
            }
            else if (_diff == DateCalculationEngine.DateDifferenceUnknown
                || (_diff.Year == 0 && _diff.Month == 0 && _diff.Week == 0))
            {
                IsDiffInDays = true;
                DiffResultInDays = string.Empty;
                DiffResult = DiffInDaysString();
            }
            else
            {
                IsDiffInDays = false;
                DiffResult = DiffString();
                DiffResultInDays = DiffInDaysString();
            }

            OnPropertyChanged(nameof(HasDiffResultInDays));
        }
        else
        {
            DateResultText = _isOutOfBound
                ? _loc.GetOr("Date_OutOfBoundMessage", "Date out of bound")
                : _dateResult.ToString("D", _loc.Culture);
        }

        OnPropertyChanged(nameof(ResultToCopy));
    }

    private string Unit(int n, string single, string plural, string defSingle, string defPlural) =>
        n.ToString(CultureInfo.InvariantCulture) + " " + (n == 1 ? _loc.GetOr(single, defSingle) : _loc.GetOr(plural, defPlural));

    private string DiffString()
    {
        var parts = new List<string>();
        if (_diff.Year > 0)
        {
            parts.Add(Unit(_diff.Year, "Date_Year", "Date_Years", "year", "years"));
        }

        if (_diff.Month > 0)
        {
            parts.Add(Unit(_diff.Month, "Date_Month", "Date_Months", "month", "months"));
        }

        if (_diff.Week > 0)
        {
            parts.Add(Unit(_diff.Week, "Date_Week", "Date_Weeks", "week", "weeks"));
        }

        if (_diff.Day > 0 || parts.Count == 0)
        {
            parts.Add(Unit(_diff.Day, "Date_Day", "Date_Days", "day", "days"));
        }

        string sep = _loc.Culture.TextInfo.ListSeparator;
        return string.Join(sep + " ", parts);
    }

    private string DiffInDaysString() =>
        Unit(_diffInDays.Day, "Date_Day", "Date_Days", "day", "days");
}
