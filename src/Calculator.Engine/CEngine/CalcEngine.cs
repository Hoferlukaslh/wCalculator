// Port of CalcManager CEngine calc.cpp.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

/// <summary>The calculation engine: a state machine that processes calculator commands (port of CCalcEngine).</summary>
internal sealed partial class CCalcEngine
{
    private const int DEFAULT_MAX_DIGITS = 32;
    private const int DEFAULT_PRECISION = 32;
    private const uint DEFAULT_RADIX = 10;

    private const char DEFAULT_DEC_SEPARATOR = '.';
    private const char DEFAULT_GRP_SEPARATOR = ',';
    private const string DEFAULT_GRP_STR = "3;0";
    private const string DEFAULT_NUMBER_STR = "0";

    private const int MAXPRECDEPTH = CHistoryCollector.MAXPRECDEPTH;
    private const int NUM_WIDTH_LENGTH = 4;

    // The string table shared across all instances.
    private static readonly Dictionary<string, string> s_engineStrings = new Dictionary<string, string>();

    private readonly bool _fPrecedence;

    // This is true if engine is explicitly called to be in integer mode. All bases are restricted to be in integers only.
    private readonly bool _fIntegerMode;
    private readonly ICalcDisplay? _calcDisplay;
    private readonly IResourceProvider _resourceProvider;
    private int _nOpCode;     // ID value of operation.
    private int _nPrevOpCode; // opcode which computed the number in _currentVal. 0 if it is already bracketed or plain number or if it hasn't yet been computed
    private bool _bChangeOp;     // Flag for changing operation
    private bool _bRecord;       // Global mode: recording or displaying
    private bool _bSetCalcState; // Flag for setting the engine result state
    private readonly CalcInput _input; // Global calc input object for decimal strings
    private NumberFormat _nFE;         // Scientific notation conversion flag
    private Rational _maxTrigonometricNum;
    private Rational _memoryValue; // Current memory value.

    private Rational _holdVal; // For holding the second operand in repetitive calculations ( pressing "=" continuously)

    private Rational _currentVal; // Currently displayed number used everywhere.
    private Rational _lastVal;    // Number before operation (left operand).
    private readonly Rational[] _parenVals = new Rational[MAXPRECDEPTH];      // Holding array for parenthesis values.
    private readonly Rational[] _precedenceVals = new Rational[MAXPRECDEPTH]; // Holding array for precedence values.
    private bool _bError; // Error flag.
    private bool _bInv;   // Inverse on/off flag.
    private bool _bNoPrevEqu; // Flag for previous equals.

    private uint _radix;
    private int _precision;
    private int _cIntDigitsSav;
    private List<uint> _decGrouping = new List<uint>(); // Holds the decimal digit grouping number

    private string _numberString;

    private int _nTempCom;         // Holding place for the last command.
    private int _openParenCount;   // Number of open parentheses.
    private readonly int[] _nOp = new int[MAXPRECDEPTH];     // Holding array for parenthesis operations.
    private readonly int[] _nPrecOp = new int[MAXPRECDEPTH]; // Holding array for precedence operations.
    private int _precedenceOpCount; // Current number of precedence ops in holding.
    private int _nLastCom;          // Last command entered.
    private AngleType _angletype;   // Current Angle type when in dec mode. one of deg, rad or grad
    private NumWidth _numwidth;     // one of qword, dword, word or byte mode.
    private int _dwWordBitWidth;    // # of bits in currently selected word size

    private Random? _random;

    private ulong _carryBit;

    private readonly CHistoryCollector _historyCollector; // Accumulator of each line of history as various commands are processed

    private readonly Rational[] _chopNumbers = new Rational[NUM_WIDTH_LENGTH];        // word size enforcement
    private readonly string[] _maxDecimalValueStrings = new string[NUM_WIDTH_LENGTH]; // maximum values represented by a given word width based off _chopNumbers
    private char _decimalSeparator;
    private char _groupSeparator;

    /// <summary>Strings keyed by engine string id: reads strings for keys, errors, trig types, etc. from the resources.</summary>
    private static void LoadEngineStrings(IResourceProvider resourceProvider)
    {
        foreach (string sid in EngineStrings.AllKeys)
        {
            string locString = resourceProvider.GetCEngineString(sid);
            if (!string.IsNullOrEmpty(locString))
            {
                s_engineStrings[sid] = locString;
            }
        }
    }

    /// <summary>Once per load time to call to initialize all shared global variables.</summary>
    public static void InitialOneTimeOnlySetup(IResourceProvider resourceProvider)
    {
        LoadEngineStrings(resourceProvider);

        // we must now set up all the ratpak constants and our arrayed pointers to these constants.
        ChangeBaseConstants(DEFAULT_RADIX, DEFAULT_MAX_DIGITS, DEFAULT_PRECISION);
    }

    public CCalcEngine(
        bool fPrecedence,
        bool fIntegerMode,
        IResourceProvider resourceProvider,
        ICalcDisplay? calcDisplay,
        IHistoryDisplay? historyDisplay)
    {
        _fPrecedence = fPrecedence;
        _fIntegerMode = fIntegerMode;
        _calcDisplay = calcDisplay;
        _resourceProvider = resourceProvider;
        _nOpCode = 0;
        _nPrevOpCode = 0;
        _bChangeOp = false;
        _bRecord = false;
        _bSetCalcState = false;
        _input = new CalcInput(DEFAULT_DEC_SEPARATOR);
        _nFE = NumberFormat.Float;
        _memoryValue = new Rational();
        _holdVal = new Rational();
        _currentVal = new Rational();
        _lastVal = new Rational();
        _maxTrigonometricNum = new Rational();
        for (int i = 0; i < MAXPRECDEPTH; i++)
        {
            _parenVals[i] = new Rational();
            _precedenceVals[i] = new Rational();
        }

        _bError = false;
        _bInv = false;
        _bNoPrevEqu = true;
        _radix = DEFAULT_RADIX;
        _precision = DEFAULT_PRECISION;
        _cIntDigitsSav = DEFAULT_MAX_DIGITS;
        _numberString = DEFAULT_NUMBER_STR;
        _nTempCom = 0;
        _openParenCount = 0;
        _precedenceOpCount = 0;
        _nLastCom = 0;
        _angletype = AngleType.Degrees;
        _numwidth = NumWidth.QWord;
        _historyCollector = new CHistoryCollector(calcDisplay, historyDisplay, DEFAULT_DEC_SEPARATOR);
        _groupSeparator = DEFAULT_GRP_SEPARATOR;

        InitChopNumbers();

        _dwWordBitWidth = DwWordBitWidthFromNumWidth(_numwidth);

        _maxTrigonometricNum = RationalMath.Pow(10, 100);

        SetRadixTypeAndNumWidth(RadixType.Decimal, _numwidth);
        SettingsChanged();
        DisplayNum();
    }

    private void InitChopNumbers()
    {
        // these rat numbers are set only once and then never change regardless of
        // base or precision changes
        _chopNumbers[0] = new Rational(RatPak.rat_qword);
        _chopNumbers[1] = new Rational(RatPak.rat_dword);
        _chopNumbers[2] = new Rational(RatPak.rat_word);
        _chopNumbers[3] = new Rational(RatPak.rat_byte);

        // initialize the max dec number you can support for each of the supported bit lengths
        // this is basically max num in that width / 2 in integer
        for (int i = 0; i < _chopNumbers.Length; i++)
        {
            Rational maxVal = _chopNumbers[i] / 2;
            maxVal = RationalMath.Integer(maxVal);

            _maxDecimalValueStrings[i] = maxVal.ToString(10, NumberFormat.Float, _precision);
        }
    }

    private Rational GetChopNumber() => _chopNumbers[(int)_numwidth];

    private string GetMaxDecimalValueString() => _maxDecimalValueStrings[(int)_numwidth];

    /// <summary>
    /// Gets the number in memory for UI to keep it persisted and set it again to a different instance
    /// of the engine. Otherwise it will get destructed with the engine.
    /// </summary>
    public Rational PersistedMemObject() => _memoryValue;

    public void PersistedMemObject(Rational memObject)
    {
        _memoryValue = memObject;
    }

    public bool FInErrorState() => _bError;

    public bool IsInputEmpty() => _input.IsEmpty() && (_numberString.Length == 0 || _numberString == "0");

    public bool FInRecordingState() => _bRecord;

    public void SettingsChanged()
    {
        char lastDec = _decimalSeparator;
        string decStr = _resourceProvider.GetCEngineString("sDecimal");
        _decimalSeparator = string.IsNullOrEmpty(decStr) ? DEFAULT_DEC_SEPARATOR : decStr[0];

        // Until it can be removed, continue to set ratpak decimal here
        RatPak.SetDecimalSeparator(_decimalSeparator);

        char lastSep = _groupSeparator;
        string sepStr = _resourceProvider.GetCEngineString("sThousand");
        _groupSeparator = string.IsNullOrEmpty(sepStr) ? DEFAULT_GRP_SEPARATOR : sepStr[0];

        List<uint> lastDecGrouping = _decGrouping;
        string grpStr = _resourceProvider.GetCEngineString("sGrouping");
        _decGrouping = DigitGroupingStringToGroupingVector(string.IsNullOrEmpty(grpStr) ? DEFAULT_GRP_STR : grpStr);

        bool numChanged = false;

        // if the grouping pattern or thousands symbol changed we need to refresh the display
        if (!SameGrouping(_decGrouping, lastDecGrouping) || _groupSeparator != lastSep)
        {
            numChanged = true;
        }

        // if the decimal symbol has changed we always do the following things
        if (_decimalSeparator != lastDec)
        {
            // Re-initialize member variables' decimal point.
            _input.SetDecimalSymbol(_decimalSeparator);
            _historyCollector.SetDecimalSymbol(_decimalSeparator);

            // put the new decimal symbol into the table used to draw the decimal key
            s_engineStrings[EngineStrings.SIDS_DECIMAL_SEPARATOR] = _decimalSeparator.ToString();

            // we need to redraw to update the decimal point button
            numChanged = true;
        }

        if (numChanged)
        {
            DisplayNum();
        }
    }

    private static bool SameGrouping(List<uint> a, List<uint> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    public char DecimalSeparator() => _decimalSeparator;

    public List<IExpressionCommand> GetHistoryCollectorCommandsSnapshot()
    {
        List<IExpressionCommand> commands = _historyCollector.GetCommands();
        if (!_historyCollector.FOpndAddedToHistory() && _bRecord)
        {
            commands.Add(_historyCollector.GetOperandCommandsFromString(_numberString, _currentVal));
        }

        return commands;
    }

    public void ChangePrecision(int precision)
    {
        _precision = precision;
        RatPak.ChangeConstants(_radix, precision);
    }

    public uint GetCurrentRadix() => _radix;

    // ---------- string table helpers ----------

    public static string GetString(int ids) => GetString(ids.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static string GetString(string ids) => s_engineStrings.TryGetValue(ids, out string? value) ? value : string.Empty;

    private static int IdStrFromCmdId(int id) => id - CCommand.IDC_FIRSTCONTROL + CCommand.IDS_ENGINESTR_FIRST;

    /// <summary>Returns the string representing the operator. Mostly same as the button, but few special cases for x^y etc.</summary>
    public static string OpCodeToString(int nOpCode) => GetString(IdStrFromCmdId(nOpCode));
}
