// Port of CalcManager UnitConverter (currency conversion is not supported in this port).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static Calculator.Engine.UnitConversion.NumberFormattingUtils;

namespace Calculator.Engine.UnitConversion;

public struct Unit : IEquatable<Unit>
{
    public Unit(int id, string name, string abbreviation, bool isConversionSource, bool isConversionTarget, bool isWhimsical)
    {
        Id = id;
        Name = name;
        AccessibleName = name;
        Abbreviation = abbreviation;
        IsConversionSource = isConversionSource;
        IsConversionTarget = isConversionTarget;
        IsWhimsical = isWhimsical;
    }

    public int Id { get; set; }

    public string Name { get; set; }

    public string AccessibleName { get; set; }

    public string Abbreviation { get; set; }

    public bool IsConversionSource { get; set; }

    public bool IsConversionTarget { get; set; }

    public bool IsWhimsical { get; set; }

    /// <summary>The EMPTY_UNIT acts as a 'null-struct' so that units can safely be used without null checks.</summary>
    public static readonly Unit Empty = new Unit(-1, string.Empty, string.Empty, true, true, false);

    public bool Equals(Unit other) => other.Id == Id;

    public override bool Equals(object? obj) => obj is Unit u && Equals(u);

    public override int GetHashCode() => Id;

    public static bool operator ==(Unit left, Unit right) => left.Equals(right);

    public static bool operator !=(Unit left, Unit right) => !left.Equals(right);

    public override string ToString() => Name ?? string.Empty;
}

public struct Category : IEquatable<Category>
{
    public Category(int id, string name, bool supportsNegative)
    {
        Id = id;
        Name = name;
        SupportsNegative = supportsNegative;
    }

    public int Id { get; set; }

    public string Name { get; set; }

    public bool SupportsNegative { get; set; }

    public bool Equals(Category other) => other.Id == Id;

    public override bool Equals(object? obj) => obj is Category c && Equals(c);

    public override int GetHashCode() => Id;

    public static bool operator ==(Category left, Category right) => left.Equals(right);

    public static bool operator !=(Category left, Category right) => !left.Equals(right);

    public override string ToString() => Name ?? string.Empty;
}

public struct ConversionData
{
    public ConversionData(double ratio, double offset, bool offsetFirst)
    {
        Ratio = ratio;
        Offset = offset;
        OffsetFirst = offsetFirst;
    }

    public double Ratio { get; set; }

    public double Offset { get; set; }

    public bool OffsetFirst { get; set; }
}

/// <summary>Loads category / unit / ratio data for the unit converter.</summary>
public interface IConverterDataLoader
{
    /// <summary>Prepare data if necessary before calling other functions.</summary>
    void LoadData();

    List<Category> GetOrderedCategories();

    List<Unit> GetOrderedUnits(Category c);

    Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit u);

    bool SupportsCategory(Category target);
}

/// <summary>Callback interface implemented by the view model.</summary>
public interface IUnitConverterVMCallback
{
    void DisplayCallback(string fromValue, string toValue);

    void SuggestedValueCallback(List<(string Value, Unit Unit)> suggestedValues);

    void MaxDigitsReached();
}

public sealed class UnitConverter
{
    private const int ExpectedSerializedCategoryTokenCount = 3;
    private const int ExpectedSerializedUnitTokenCount = 6;

    private const uint MaximumDigitsAllowed = 15;
    private const uint OptimalDigitsAllowed = 7;

    private const char LeftEscapeChar = '{';
    private const char RightEscapeChar = '}';

    private const double OptimalDecimalAllowed = 1e-6;  // pow(10, -1 * (OPTIMALDIGITSALLOWED - 1));
    private const double MinimumDecimalAllowed = 1e-14; // pow(10, -1 * (MAXIMUMDIGITSALLOWED - 1));

    private static readonly Dictionary<char, string> s_quoteConversions = new Dictionary<char, string>
    {
        { '|', "{p}" },
        { '[', "{lc}" },
        { ']', "{rc}" },
        { ':', "{co}" },
        { ',', "{cm}" },
        { ';', "{sc}" },
        { LeftEscapeChar, "{lb}" },
        { RightEscapeChar, "{rb}" },
    };

    private static readonly Dictionary<string, char> s_unquoteConversions = s_quoteConversions.ToDictionary(kv => kv.Value, kv => kv.Key);

    private readonly IConverterDataLoader _dataLoader;
    private IUnitConverterVMCallback? _vmCallback;
    private List<Category> _categories = new List<Category>();
    private readonly Dictionary<int, List<Unit>> _categoryToUnits = new Dictionary<int, List<Unit>>();
    private readonly Dictionary<Unit, Dictionary<Unit, ConversionData>> _ratioMap = new Dictionary<Unit, Dictionary<Unit, ConversionData>>();
    private Category _currentCategory;
    private Unit _fromType = Unit.Empty;
    private Unit _toType = Unit.Empty;
    private string _currentDisplay = "0";
    private string _returnDisplay = "0";
    private bool _currentHasDecimal;
    private bool _returnHasDecimal;
    private bool _switchedActive;

    /// <param name="dataLoader">Used to read in category/unit names and conversion data.</param>
    public UnitConverter(IConverterDataLoader dataLoader)
    {
        _dataLoader = dataLoader;
        ClearValues();
        ResetCategoriesAndRatios();
    }

    /// <summary>Use to initialize first time, use RestoreUserPreferences instead to rehydrate.</summary>
    public void Initialize() => _dataLoader.LoadData();

    private bool CheckLoad()
    {
        if (_categories.Count == 0)
        {
            ResetCategoriesAndRatios();
        }

        return _categories.Count != 0;
    }

    /// <summary>Returns a list of the categories in use by this converter.</summary>
    public List<Category> GetCategories()
    {
        CheckLoad();
        return _categories;
    }

    /// <summary>
    /// Sets the current category in use by this converter,
    /// and returns a list of unit types that exist under the given category.
    /// </summary>
    public (List<Unit> Units, Unit From, Unit To) SetCurrentCategory(Category input)
    {
        var newUnitList = new List<Unit>();
        if (CheckLoad())
        {
            if (_currentCategory.Id != input.Id)
            {
                if (_categoryToUnits.TryGetValue(_currentCategory.Id, out List<Unit>? oldUnits))
                {
                    for (int i = 0; i < oldUnits.Count; i++)
                    {
                        Unit unit = oldUnits[i];
                        unit.IsConversionSource = unit.Id == _fromType.Id;
                        unit.IsConversionTarget = unit.Id == _toType.Id;
                        oldUnits[i] = unit;
                    }
                }

                _currentCategory = input;
                if (!_currentCategory.SupportsNegative && _currentDisplay.Length > 0 && _currentDisplay[0] == '-')
                {
                    _currentDisplay = _currentDisplay.Substring(1);
                }
            }

            if (_categoryToUnits.TryGetValue(input.Id, out List<Unit>? units))
            {
                newUnitList = new List<Unit>(units);
            }
        }

        InitializeSelectedUnits();
        return (newUnitList, _fromType, _toType);
    }

    /// <summary>Gets the category currently being used.</summary>
    public Category GetCurrentCategory() => _currentCategory;

    /// <summary>
    /// Sets the current unit types to be used, indicates a likely change in the
    /// display values, so we re-calculate and callback the updated values.
    /// </summary>
    public void SetCurrentUnitTypes(Unit fromType, Unit toType)
    {
        if (!CheckLoad())
        {
            return;
        }

        if (_fromType != fromType)
        {
            _switchedActive = true;
        }

        _fromType = fromType;
        _toType = toType;
        Calculate();
    }

    /// <summary>
    /// Switches the active field, indicating that we are now entering data into
    /// what was originally the return field, and storing results into what was
    /// originally the current field. We swap appropriate values, but do not callback, as values have not changed.
    /// </summary>
    /// <param name="newValue">The value the user had in the field they've just activated.</param>
    public void SwitchActive(string newValue)
    {
        if (!CheckLoad())
        {
            return;
        }

        (_fromType, _toType) = (_toType, _fromType);
        (_currentHasDecimal, _returnHasDecimal) = (_returnHasDecimal, _currentHasDecimal);
        _returnDisplay = _currentDisplay;
        _currentDisplay = newValue;
        _currentHasDecimal = _currentDisplay.IndexOf('.') >= 0;
        _switchedActive = true;
    }

    public bool IsSwitchedActive() => _switchedActive;

    private static string CategoryToString(Category c, string delimiter)
    {
        return Quote(c.Id.ToString(CultureInfo.InvariantCulture)) + delimiter
            + Quote(c.SupportsNegative ? "1" : "0") + delimiter
            + Quote(c.Name) + delimiter;
    }

    public static List<string> StringToVector(string w, string delimiter, bool addRemainder = false)
    {
        int delimiterIndex = w.IndexOf(delimiter, StringComparison.Ordinal);
        int startIndex = 0;
        var serializedTokens = new List<string>();
        while (delimiterIndex >= 0)
        {
            serializedTokens.Add(w.Substring(startIndex, delimiterIndex - startIndex));
            startIndex = delimiterIndex + delimiter.Length;
            delimiterIndex = w.IndexOf(delimiter, startIndex, StringComparison.Ordinal);
        }

        if (addRemainder)
        {
            serializedTokens.Add(w.Substring(startIndex));
        }

        return serializedTokens;
    }

    private static string UnitToString(Unit u, string delimiter)
    {
        return Quote(u.Id.ToString(CultureInfo.InvariantCulture)) + delimiter
            + Quote(u.Name ?? string.Empty) + delimiter
            + Quote(u.Abbreviation ?? string.Empty) + delimiter
            + (u.IsConversionSource ? "1" : "0") + delimiter
            + (u.IsConversionTarget ? "1" : "0") + delimiter
            + (u.IsWhimsical ? "1" : "0") + delimiter;
    }

    private static int ParseInt(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

    private static Unit StringToUnit(string w)
    {
        List<string> tokenList = StringToVector(w, ";");
        if (tokenList.Count != ExpectedSerializedUnitTokenCount)
        {
            return Unit.Empty;
        }

        var serializedUnit = new Unit
        {
            Id = ParseInt(Unquote(tokenList[0])),
            Name = Unquote(tokenList[1]),
            Abbreviation = Unquote(tokenList[2]),
            IsConversionSource = tokenList[3] == "1",
            IsConversionTarget = tokenList[4] == "1",
            IsWhimsical = tokenList[5] == "1",
        };
        serializedUnit.AccessibleName = serializedUnit.Name;
        return serializedUnit;
    }

    private static Category StringToCategory(string w)
    {
        List<string> tokenList = StringToVector(w, ";");
        if (tokenList.Count != ExpectedSerializedCategoryTokenCount)
        {
            return default;
        }

        return new Category(ParseInt(Unquote(tokenList[0])), Unquote(tokenList[2]), tokenList[1] == "1");
    }

    /// <summary>De-serializes the data in the converter from a string. If it does not have the expected number of parameters, it is ignored.</summary>
    public void RestoreUserPreferences(string userPreferences)
    {
        if (string.IsNullOrEmpty(userPreferences))
        {
            return;
        }

        List<string> outerTokens = StringToVector(userPreferences, "|");
        if (outerTokens.Count != 3)
        {
            return;
        }

        Unit fromType = StringToUnit(outerTokens[0]);
        Unit toType = StringToUnit(outerTokens[1]);
        _currentCategory = StringToCategory(outerTokens[2]);

        // Only restore from the saved units if they are valid in the current available units.
        if (_categoryToUnits.TryGetValue(_currentCategory.Id, out List<Unit>? curUnits))
        {
            if (curUnits.Contains(fromType))
            {
                _fromType = fromType;
            }

            if (curUnits.Contains(toType))
            {
                _toType = toType;
            }
        }
    }

    /// <summary>Serializes the Category and associated Units in the converter and returns it as a string.</summary>
    public string SaveUserPreferences()
    {
        const string delimiter = ";";
        const string pipe = "|";
        return UnitToString(_fromType, delimiter) + pipe + UnitToString(_toType, delimiter) + pipe + CategoryToString(_currentCategory, delimiter) + pipe;
    }

    /// <summary>Sanitizes the input string, escape quoting any symbols we rely on for our delimiters.</summary>
    public static string Quote(string s)
    {
        var quotedString = new StringBuilder();

        foreach (char ch in s)
        {
            if (s_quoteConversions.TryGetValue(ch, out string? quoted))
            {
                quotedString.Append(quoted);
            }
            else
            {
                quotedString.Append(ch);
            }
        }

        return quotedString.ToString();
    }

    /// <summary>Unsanitizes the sanitized input string, returning it to its original contents before it was quoted.</summary>
    public static string Unquote(string s)
    {
        var unquotedString = new StringBuilder();
        int cursor = 0;
        while (cursor < s.Length)
        {
            if (s[cursor] == LeftEscapeChar)
            {
                var quotedSubString = new StringBuilder();
                while (cursor < s.Length && s[cursor] != RightEscapeChar)
                {
                    quotedSubString.Append(s[cursor]);
                    ++cursor;
                }

                if (cursor >= s.Length)
                {
                    // Badly formatted
                    break;
                }

                quotedSubString.Append(s[cursor]);
                if (s_unquoteConversions.TryGetValue(quotedSubString.ToString(), out char unquoted))
                {
                    unquotedString.Append(unquoted);
                }
            }
            else
            {
                unquotedString.Append(s[cursor]);
            }

            ++cursor;
        }

        return unquotedString.ToString();
    }

    /// <summary>Handles inputs to the converter from the view-model, corresponding to a given button or keyboard press.</summary>
    public void SendCommand(UnitConversionCommand command)
    {
        if (!CheckLoad())
        {
            return;
        }

        bool clearFront;
        bool clearBack;
        if (command != UnitConversionCommand.Negate && _switchedActive)
        {
            ClearValues();
            _switchedActive = false;
            clearFront = true;
            clearBack = false;
        }
        else
        {
            clearFront = _currentDisplay == "0";
            clearBack = (_currentHasDecimal && _currentDisplay.Length - 1 >= MaximumDigitsAllowed)
                || (!_currentHasDecimal && _currentDisplay.Length >= MaximumDigitsAllowed);
        }

        switch (command)
        {
            case UnitConversionCommand.Zero:
            case UnitConversionCommand.One:
            case UnitConversionCommand.Two:
            case UnitConversionCommand.Three:
            case UnitConversionCommand.Four:
            case UnitConversionCommand.Five:
            case UnitConversionCommand.Six:
            case UnitConversionCommand.Seven:
            case UnitConversionCommand.Eight:
            case UnitConversionCommand.Nine:
                _currentDisplay += (char)('0' + (int)command);
                break;

            case UnitConversionCommand.Decimal:
                clearFront = false;
                clearBack = false;
                if (!_currentHasDecimal)
                {
                    _currentDisplay += '.';
                    _currentHasDecimal = true;
                }

                break;

            case UnitConversionCommand.Backspace:
                clearFront = false;
                clearBack = false;
                if ((_currentDisplay[0] != '-' && _currentDisplay.Length > 1) || _currentDisplay.Length > 2)
                {
                    if (_currentDisplay[_currentDisplay.Length - 1] == '.')
                    {
                        _currentHasDecimal = false;
                    }

                    _currentDisplay = _currentDisplay.Substring(0, _currentDisplay.Length - 1);
                }
                else
                {
                    _currentDisplay = "0";
                    _currentHasDecimal = false;
                }

                break;

            case UnitConversionCommand.Negate:
                clearFront = false;
                clearBack = false;
                if (_currentCategory.SupportsNegative)
                {
                    if (_currentDisplay[0] == '-')
                    {
                        _currentDisplay = _currentDisplay.Substring(1);
                    }
                    else
                    {
                        _currentDisplay = "-" + _currentDisplay;
                    }
                }

                break;

            case UnitConversionCommand.Clear:
                clearFront = false;
                clearBack = false;
                ClearValues();
                break;

            case UnitConversionCommand.Reset:
                clearFront = false;
                clearBack = false;
                ClearValues();
                ResetCategoriesAndRatios();
                break;

            default:
                clearFront = false;
                clearBack = false;
                break;
        }

        if (clearFront && _currentDisplay.Length > 1)
        {
            _currentDisplay = _currentDisplay.Substring(1);
        }

        if (clearBack && _currentDisplay.Length > 0)
        {
            _currentDisplay = _currentDisplay.Substring(0, _currentDisplay.Length - 1);
            _vmCallback?.MaxDigitsReached();
        }

        Calculate();
    }

    /// <summary>Sets the callback interface to send display update calls to.</summary>
    public void SetViewModelCallback(IUnitConverterVMCallback newCallback)
    {
        _vmCallback = newCallback;
        if (CheckLoad())
        {
            UpdateViewModel();
        }
    }

    /// <summary>Converts a double value into another unit type.</summary>
    private static double Convert(double value, ConversionData conversionData)
    {
        if (conversionData.OffsetFirst)
        {
            return (value + conversionData.Offset) * conversionData.Ratio;
        }

        return (value * conversionData.Ratio) + conversionData.Offset;
    }

    private sealed class SuggestedValueIntermediate
    {
        public double Magnitude;
        public double Value;
        public Unit Type;
    }

    private static string FormatSuggested(double value)
    {
        if (Math.Abs(value) < 100)
        {
            return RoundSignificantDigits(value, 2U);
        }

        if (Math.Abs(value) < 1000)
        {
            return RoundSignificantDigits(value, 1U);
        }

        return RoundSignificantDigits(value, 0U);
    }

    private static IEnumerable<SuggestedValueIntermediate> SortByMagnitude(List<SuggestedValueIntermediate> list)
    {
        // Sort the resulting list by absolute magnitude, breaking ties by choosing the positive value
        return list.OrderBy(e => Math.Abs(e.Magnitude)).ThenByDescending(e => e.Magnitude);
    }

    /// <summary>Calculates the suggested values for the current display value and returns them as a list.</summary>
    private List<(string Value, Unit Unit)> CalculateSuggested()
    {
        var returnVector = new List<(string Value, Unit Unit)>();
        var intermediateVector = new List<SuggestedValueIntermediate>();
        var intermediateWhimsicalVector = new List<SuggestedValueIntermediate>();

        if (!_ratioMap.TryGetValue(_fromType, out Dictionary<Unit, ConversionData>? ratios))
        {
            return returnVector;
        }

        // Calculate converted values for every other unit type in this category, along with their magnitude
        double current = Stod(_currentDisplay);
        foreach (KeyValuePair<Unit, ConversionData> cur in ratios)
        {
            if (cur.Key != _fromType && cur.Key != _toType)
            {
                double convertedValue = Convert(current, cur.Value);
                var newEntry = new SuggestedValueIntermediate
                {
                    Magnitude = Math.Log10(convertedValue),
                    Value = convertedValue,
                    Type = cur.Key,
                };

                if (newEntry.Type.IsWhimsical)
                {
                    intermediateWhimsicalVector.Add(newEntry);
                }
                else
                {
                    intermediateVector.Add(newEntry);
                }
            }
        }

        // Now that the list is sorted, iterate over it and populate the return vector with properly rounded and formatted return strings
        foreach (SuggestedValueIntermediate entry in SortByMagnitude(intermediateVector))
        {
            string roundedString = FormatSuggested(entry.Value);
            if (Stod(roundedString) != 0.0 || _currentCategory.SupportsNegative)
            {
                roundedString = TrimTrailingZeros(roundedString);
                returnVector.Add((roundedString, entry.Type));
            }
        }

        // The Whimsicals are determined differently
        var whimsicalReturnVector = new List<(string Value, Unit Unit)>();
        foreach (SuggestedValueIntermediate entry in SortByMagnitude(intermediateWhimsicalVector))
        {
            string roundedString = FormatSuggested(entry.Value);

            // How to work out which is the best whimsical value to add to the vector?
            if (Stod(roundedString) != 0.0)
            {
                roundedString = TrimTrailingZeros(roundedString);
                whimsicalReturnVector.Add((roundedString, entry.Type));
            }
        }

        // Pickup the 'best' whimsical value - currently the first one
        if (whimsicalReturnVector.Count != 0)
        {
            returnVector.Add(whimsicalReturnVector[0]);
        }

        return returnVector;
    }

    /// <summary>Resets categories and ratios.</summary>
    public void ResetCategoriesAndRatios()
    {
        _switchedActive = false;
        _categories = _dataLoader.GetOrderedCategories();
        if (_categories.Count == 0)
        {
            return;
        }

        _currentCategory = _categories[0];

        _categoryToUnits.Clear();
        _ratioMap.Clear();
        bool readyCategoryFound = false;
        foreach (Category category in _categories)
        {
            if (!_dataLoader.SupportsCategory(category))
            {
                continue;
            }

            List<Unit> units = _dataLoader.GetOrderedUnits(category);
            _categoryToUnits[category.Id] = units;

            // Just because the units are empty, doesn't mean the user can't select this category,
            // we just want to make sure we don't let an unready category be the default.
            if (units.Count != 0)
            {
                foreach (Unit u in units)
                {
                    _ratioMap[u] = _dataLoader.LoadOrderedRatios(u);
                }

                if (!readyCategoryFound)
                {
                    _currentCategory = category;
                    readyCategoryFound = true;
                }
            }
        }

        InitializeSelectedUnits();
    }

    /// <summary>
    /// Sets the initial values for the from and to types. This is an internal helper method as opposed to SetCurrentUnitTypes
    /// which is for external use by clients. If we fail to set units, we will fallback to the empty unit.
    /// </summary>
    private void InitializeSelectedUnits()
    {
        if (_categoryToUnits.Count == 0)
        {
            return;
        }

        if (!_categoryToUnits.TryGetValue(_currentCategory.Id, out List<Unit>? curUnits))
        {
            return;
        }

        if (curUnits.Count != 0)
        {
            // Units may already have been initialized through RestoreUserPreferences().
            // Check if they have been, and if so, do not override restored units.
            bool isFromUnitValid = _fromType != Unit.Empty && curUnits.Contains(_fromType);
            bool isToUnitValid = _toType != Unit.Empty && curUnits.Contains(_toType);

            if (isFromUnitValid && isToUnitValid)
            {
                return;
            }

            bool conversionSourceSet = false;
            bool conversionTargetSet = false;
            foreach (Unit cur in curUnits)
            {
                if (!conversionSourceSet && cur.IsConversionSource && !isFromUnitValid)
                {
                    _fromType = cur;
                    conversionSourceSet = true;
                }

                if (!conversionTargetSet && cur.IsConversionTarget && !isToUnitValid)
                {
                    _toType = cur;
                    conversionTargetSet = true;
                }

                if (conversionSourceSet && conversionTargetSet)
                {
                    return;
                }
            }
        }

        _fromType = Unit.Empty;
        _toType = Unit.Empty;
    }

    /// <summary>Resets the value fields to 0.</summary>
    private void ClearValues()
    {
        _currentHasDecimal = false;
        _returnHasDecimal = false;
        _currentDisplay = "0";
    }

    /// <summary>Checks if either unit is the empty unit.</summary>
    private bool AnyUnitIsEmpty() => _fromType == Unit.Empty || _toType == Unit.Empty;

    /// <summary>Calculates a new return value based on the current display value.</summary>
    public void Calculate()
    {
        if (AnyUnitIsEmpty())
        {
            _returnDisplay = TrimTrailingZeros(_currentDisplay);
            _returnHasDecimal = _currentHasDecimal;
            UpdateViewModel();
            return;
        }

        ConversionData toData = default;
        if (_ratioMap.TryGetValue(_fromType, out Dictionary<Unit, ConversionData>? conversionTable))
        {
            conversionTable.TryGetValue(_toType, out toData);
        }

        if (toData.Ratio == 1.0 && toData.Offset == 0.0)
        {
            _returnDisplay = TrimTrailingZeros(_currentDisplay);
            _returnHasDecimal = _currentHasDecimal;
        }
        else
        {
            double currentValue = Stod(_currentDisplay);
            double returnValue = Convert(currentValue, toData);

            uint numPreDecimal = GetNumberDigitsWholeNumberPart(returnValue);
            if (numPreDecimal > MaximumDigitsAllowed || (returnValue != 0 && Math.Abs(returnValue) < MinimumDecimalAllowed))
            {
                _returnDisplay = ToScientificNumber(returnValue);
            }
            else
            {
                uint currentNumberSignificantDigits = GetNumberDigits(_currentDisplay);
                uint precision;
                if (Math.Abs(returnValue) < OptimalDecimalAllowed)
                {
                    precision = MaximumDigitsAllowed;
                }
                else
                {
                    // Fewer digits are needed following the decimal if the number is large,
                    // we calculate the number of decimals necessary based on the number of digits in the integer part.
                    uint numberDigits = Math.Max(OptimalDigitsAllowed, Math.Min(MaximumDigitsAllowed, currentNumberSignificantDigits));
                    precision = numberDigits > numPreDecimal ? numberDigits - numPreDecimal : 0;
                }

                _returnDisplay = RoundSignificantDigits(returnValue, precision);
                _returnDisplay = TrimTrailingZeros(_returnDisplay);
            }

            _returnHasDecimal = _returnDisplay.IndexOf('.') >= 0;
        }

        UpdateViewModel();
    }

    private void UpdateViewModel()
    {
        if (_vmCallback != null)
        {
            _vmCallback.DisplayCallback(_currentDisplay, _returnDisplay);
            _vmCallback.SuggestedValueCallback(CalculateSuggested());
        }
    }
}
