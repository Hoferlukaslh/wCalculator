// Port of CalcManager CalculatorManager.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

public enum CalculatorMode
{
    Standard = 0,
    Scientific,
}

public enum CalculatorPrecision
{
    StandardModePrecision = 16,
    ScientificModePrecision = 32,
    ProgrammerModePrecision = 64,
}

/// <summary>
/// Numbering continues from the Enum Command from Command.h with some gap to ensure there is no overlap of these ids.
/// </summary>
public enum MemoryCommand
{
    MemorizeNumber = 330,
    MemorizedNumberLoad = 331,
    MemorizedNumberAdd = 332,
    MemorizedNumberSubtract = 333,
    MemorizedNumberClearAll = 334,
    MemorizedNumberClear = 335,
}

/// <summary>
/// Entry point of the calculation engine: owns the standard, scientific and programmer engines,
/// the memory list and the history, and forwards display updates to the client.
/// </summary>
public sealed class CalculatorManager : ICalcDisplay
{
    private const int MaxHistoryItems = 20;
    private const int MaximumMemorySize = 100;

    private readonly ICalcDisplay _displayCallback;
    private CCalcEngine? _currentCalculatorEngine;
    private CCalcEngine? _scientificCalculatorEngine;
    private CCalcEngine? _standardCalculatorEngine;
    private CCalcEngine? _programmerCalculatorEngine;
    private readonly IResourceProvider _resourceProvider;
    private bool _inHistoryItemLoadMode;

    private readonly List<Rational> _memorizedNumbers = new List<Rational>();
    private readonly Rational _persistedPrimaryValue = new Rational();
    private bool _isExponentialFormat;
    private Command _currentDegreeMode;

    private readonly CalculatorHistory _stdHistory = new CalculatorHistory(MaxHistoryItems);
    private readonly CalculatorHistory _sciHistory = new CalculatorHistory(MaxHistoryItems);
    private CalculatorHistory? _history;

    public CalculatorManager(ICalcDisplay displayCallback, IResourceProvider resourceProvider)
    {
        _displayCallback = displayCallback;
        _resourceProvider = resourceProvider;
        _currentDegreeMode = Command.CommandNULL;

        CCalcEngine.InitialOneTimeOnlySetup(_resourceProvider);
    }

    private CCalcEngine Engine => _currentCalculatorEngine ?? throw new InvalidOperationException("No calculator mode has been selected yet.");

    // ---------- ICalcDisplay (callbacks from the engines, forwarded to the client) ----------

    /// <summary>Used to set the primary display value on the view model.</summary>
    public void SetPrimaryDisplay(string displayString, bool isError)
    {
        if (!_inHistoryItemLoadMode)
        {
            _displayCallback.SetPrimaryDisplay(displayString, isError);
        }
    }

    public void SetIsInError(bool isError) => _displayCallback.SetIsInError(isError);

    public void DisplayPasteError()
    {
        Engine.DisplayError(CalcErr.Domain /* code for "Invalid input" error */);
    }

    public void MaxDigitsReached() => _displayCallback.MaxDigitsReached();

    public void BinaryOperatorReceived() => _displayCallback.BinaryOperatorReceived();

    public void MemoryItemChanged(int indexOfMemory) => _displayCallback.MemoryItemChanged(indexOfMemory);

    public void InputChanged() => _displayCallback.InputChanged();

    /// <summary>Used to set the expression display value on the view model.</summary>
    public void SetExpressionDisplay(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands)
    {
        if (!_inHistoryItemLoadMode)
        {
            _displayCallback.SetExpressionDisplay(tokens, commands);
        }
    }

    /// <summary>String representations of memorized numbers get passed to the client.</summary>
    public void SetMemorizedNumbers(List<string> memorizedNumbers) => _displayCallback.SetMemorizedNumbers(memorizedNumbers);

    public void SetParenthesisNumber(int parenthesisCount) => _displayCallback.SetParenthesisNumber(parenthesisCount);

    public void OnNoRightParenAdded() => _displayCallback.OnNoRightParenAdded();

    public void OnHistoryItemAdded(int addedItemIndex) => _displayCallback.OnHistoryItemAdded(addedItemIndex);

    // ---------- modes ----------

    /// <summary>
    /// Reset the calculator manager: set the mode to the standard calculator, set the degree mode as regular degree
    /// (as opposed to Rad or Grad), clear all the entries and, if requested, the memory.
    /// </summary>
    public void Reset(bool clearMemory = true)
    {
        SetStandardMode();

        if (_scientificCalculatorEngine != null)
        {
            _scientificCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
            _scientificCalculatorEngine.ProcessCommand(CCommand.IDC_DEG);

            if (_isExponentialFormat)
            {
                _isExponentialFormat = false;
                _scientificCalculatorEngine.ProcessCommand(CCommand.IDC_FE);
            }
        }

        _currentDegreeMode = Command.CommandDEG;

        if (_programmerCalculatorEngine != null)
        {
            _programmerCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
            _programmerCalculatorEngine.ProcessCommand(CCommand.IDC_QWORD);
        }

        if (clearMemory)
        {
            MemorizedNumberClearAll();
        }
    }

    /// <summary>Change the current calculator engine to the standard calculator engine.</summary>
    public void SetStandardMode()
    {
        _standardCalculatorEngine ??= new CCalcEngine(false /* Respect Order of Operations */, false /* Set to Integer Mode */, _resourceProvider, this, _stdHistory);

        _currentCalculatorEngine = _standardCalculatorEngine;
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
        _currentCalculatorEngine.ChangePrecision((int)CalculatorPrecision.StandardModePrecision);
        UpdateMaxIntDigits();
        _history = _stdHistory;
    }

    /// <summary>Change the current calculator engine to the scientific calculator engine.</summary>
    public void SetScientificMode()
    {
        _scientificCalculatorEngine ??= new CCalcEngine(true /* Respect Order of Operations */, false /* Set to Integer Mode */, _resourceProvider, this, _sciHistory);

        _currentCalculatorEngine = _scientificCalculatorEngine;
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
        _currentCalculatorEngine.ChangePrecision((int)CalculatorPrecision.ScientificModePrecision);
        _history = _sciHistory;
    }

    /// <summary>Change the current calculator engine to the programmer calculator engine.</summary>
    public void SetProgrammerMode()
    {
        _programmerCalculatorEngine ??= new CCalcEngine(true /* Respect Order of Operations */, true /* Set to Integer Mode */, _resourceProvider, this, null);

        _currentCalculatorEngine = _programmerCalculatorEngine;
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_DEC);
        _currentCalculatorEngine.ProcessCommand(CCommand.IDC_CLEAR);
        _currentCalculatorEngine.ChangePrecision((int)CalculatorPrecision.ProgrammerModePrecision);
    }

    /// <summary>
    /// Send a command to the calculation engine: handles the special commands such as mode changes
    /// and the combination of two commands (inverse functions).
    /// </summary>
    public void SendCommand(Command command)
    {
        // When the expression line is cleared, we save the current state, which includes,
        // primary display, memory, and degree mode
        if (command == Command.CommandCLEAR || command == Command.CommandEQU || command == Command.ModeBasic || command == Command.ModeScientific
            || command == Command.ModeProgrammer)
        {
            switch (command)
            {
                case Command.ModeBasic:
                    SetStandardMode();
                    break;
                case Command.ModeScientific:
                    SetScientificMode();
                    break;
                case Command.ModeProgrammer:
                    SetProgrammerMode();
                    break;
                default:
                    Engine.ProcessCommand((int)command);
                    break;
            }

            InputChanged();
            return;
        }

        if (command == Command.CommandDEG || command == Command.CommandRAD || command == Command.CommandGRAD)
        {
            _currentDegreeMode = command;
        }

        switch (command)
        {
            case Command.CommandASIN:
                SendInverse(Command.CommandSIN);
                break;
            case Command.CommandACOS:
                SendInverse(Command.CommandCOS);
                break;
            case Command.CommandATAN:
                SendInverse(Command.CommandTAN);
                break;
            case Command.CommandPOWE:
                SendInverse(Command.CommandLN);
                break;
            case Command.CommandASINH:
                SendInverse(Command.CommandSINH);
                break;
            case Command.CommandACOSH:
                SendInverse(Command.CommandCOSH);
                break;
            case Command.CommandATANH:
                SendInverse(Command.CommandTANH);
                break;
            case Command.CommandASEC:
                SendInverse(Command.CommandSEC);
                break;
            case Command.CommandACSC:
                SendInverse(Command.CommandCSC);
                break;
            case Command.CommandACOT:
                SendInverse(Command.CommandCOT);
                break;
            case Command.CommandASECH:
                SendInverse(Command.CommandSECH);
                break;
            case Command.CommandACSCH:
                SendInverse(Command.CommandCSCH);
                break;
            case Command.CommandACOTH:
                SendInverse(Command.CommandCOTH);
                break;
            case Command.CommandFE:
                _isExponentialFormat = !_isExponentialFormat;
                Engine.ProcessCommand((int)command);
                break;
            default:
                Engine.ProcessCommand((int)command);
                break;
        }

        InputChanged();
    }

    private void SendInverse(Command function)
    {
        Engine.ProcessCommand((int)Command.CommandINV);
        Engine.ProcessCommand((int)function);
    }

    /// <summary>Load the persisted value that is saved in memory of the engine.</summary>
    private void LoadPersistedPrimaryValue()
    {
        Engine.PersistedMemObject(_persistedPrimaryValue);
        Engine.ProcessCommand(CCommand.IDC_RECALL);
        InputChanged();
    }

    // ---------- memory ----------

    /// <summary>Memorize the current displayed value and notify the client with the new memorized value list.</summary>
    public void MemorizeNumber()
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        Engine.ProcessCommand(CCommand.IDC_STORE);

        Rational memoryObject = Engine.PersistedMemObject();
        _memorizedNumbers.Insert(0, memoryObject);

        if (_memorizedNumbers.Count > MaximumMemorySize)
        {
            _memorizedNumbers.RemoveRange(MaximumMemorySize, _memorizedNumbers.Count - MaximumMemorySize);
        }

        SetMemorizedNumbersString();
    }

    /// <summary>Recall the memorized number. The memorized number gets loaded to the primary display.</summary>
    public void MemorizedNumberLoad(int indexOfMemory)
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        MemorizedNumberSelect(indexOfMemory);
        Engine.ProcessCommand(CCommand.IDC_RECALL);
        InputChanged();
    }

    /// <summary>Adds the primary display value to the selected memory and notifies the client.</summary>
    public void MemorizedNumberAdd(int indexOfMemory)
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        if (_memorizedNumbers.Count == 0)
        {
            MemorizeNumber();
        }
        else
        {
            MemorizedNumberSelect(indexOfMemory);
            Engine.ProcessCommand(CCommand.IDC_MPLUS);

            MemorizedNumberChanged(indexOfMemory);

            SetMemorizedNumbersString();
        }

        _displayCallback.MemoryItemChanged(indexOfMemory);
    }

    public void MemorizedNumberClear(int indexOfMemory)
    {
        if (indexOfMemory >= 0 && indexOfMemory < _memorizedNumbers.Count)
        {
            _memorizedNumbers.RemoveAt(indexOfMemory);
        }
    }

    /// <summary>Subtracts the primary display value from the selected memory and notifies the client.</summary>
    public void MemorizedNumberSubtract(int indexOfMemory)
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        // To add negative of the number on display to the memory -x = x - 2x
        if (_memorizedNumbers.Count == 0)
        {
            MemorizeNumber();
            MemorizedNumberSubtract(0);
            MemorizedNumberSubtract(0);
        }
        else
        {
            MemorizedNumberSelect(indexOfMemory);
            Engine.ProcessCommand(CCommand.IDC_MMINUS);

            MemorizedNumberChanged(indexOfMemory);

            SetMemorizedNumbersString();
        }

        _displayCallback.MemoryItemChanged(indexOfMemory);
    }

    /// <summary>Clear all the memorized values and notify the client.</summary>
    public void MemorizedNumberClearAll()
    {
        _memorizedNumbers.Clear();

        Engine.ProcessCommand(CCommand.IDC_MCLEAR);
        SetMemorizedNumbersString();
    }

    /// <summary>Selects a memory from the list and sets it into the engine.</summary>
    private void MemorizedNumberSelect(int indexOfMemory)
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        Rational memoryObject = _memorizedNumbers[indexOfMemory];
        Engine.PersistedMemObject(memoryObject);
    }

    /// <summary>Needs to be executed when the engine memory is modified: stores the new value in the list.</summary>
    private void MemorizedNumberChanged(int indexOfMemory)
    {
        if (Engine.FInErrorState())
        {
            return;
        }

        Rational memoryObject = Engine.PersistedMemObject();
        _memorizedNumbers[indexOfMemory] = memoryObject;
    }

    public void SetMemorizedNumbersString()
    {
        var resultVector = new List<string>();
        foreach (Rational memoryItem in _memorizedNumbers)
        {
            uint radix = Engine.GetCurrentRadix();
            string stringValue = Engine.GetStringForDisplay(memoryItem, radix);

            if (stringValue.Length != 0)
            {
                resultVector.Add(Engine.GroupDigitsPerRadix(stringValue, radix));
            }
        }

        _displayCallback.SetMemorizedNumbers(resultVector);
    }

    // ---------- history ----------

    public IReadOnlyList<HistoryItem> GetHistoryItems() => (_history ?? _stdHistory).GetHistory();

    public IReadOnlyList<HistoryItem> GetHistoryItems(CalculatorMode mode) =>
        mode == CalculatorMode.Standard ? _stdHistory.GetHistory() : _sciHistory.GetHistory();

    public void SetHistoryItems(IEnumerable<HistoryItem> historyItems)
    {
        CalculatorHistory history = _history ?? _stdHistory;
        foreach (HistoryItem historyItem in historyItems)
        {
            int index = history.AddItem(historyItem);
            OnHistoryItemAdded(index);
        }
    }

    public HistoryItem GetHistoryItem(int index) => (_history ?? _stdHistory).GetHistoryItem(index);

    public bool RemoveHistoryItem(int index) => (_history ?? _stdHistory).RemoveItem(index);

    public void ClearHistory() => (_history ?? _stdHistory).ClearHistory();

    public int MaxHistorySize => (_history ?? _stdHistory).MaxHistorySize;

    // ---------- misc ----------

    public void SetRadix(RadixType radixType)
    {
        switch (radixType)
        {
            case RadixType.Hex:
                Engine.ProcessCommand(CCommand.IDC_HEX);
                break;
            case RadixType.Decimal:
                Engine.ProcessCommand(CCommand.IDC_DEC);
                break;
            case RadixType.Octal:
                Engine.ProcessCommand(CCommand.IDC_OCT);
                break;
            case RadixType.Binary:
                Engine.ProcessCommand(CCommand.IDC_BIN);
                break;
        }

        SetMemorizedNumbersString();
    }

    public Command GetCurrentDegreeMode()
    {
        if (_currentDegreeMode == Command.CommandNULL)
        {
            _currentDegreeMode = Command.CommandDEG;
        }

        return _currentDegreeMode;
    }

    public string GetResultForRadix(uint radix, int precision, bool groupDigitsPerRadix) =>
        _currentCalculatorEngine != null ? _currentCalculatorEngine.GetCurrentResultForRadix(radix, precision, groupDigitsPerRadix) : string.Empty;

    public void SetPrecision(int precision) => Engine.ChangePrecision(precision);

    public void UpdateMaxIntDigits() => Engine.UpdateMaxIntDigits();

    public char DecimalSeparator()
    {
        if (_currentCalculatorEngine != null)
        {
            return _currentCalculatorEngine.DecimalSeparator();
        }

        string sep = _resourceProvider.GetCEngineString("sDecimal");
        return sep.Length > 0 ? sep[0] : '.';
    }

    public bool IsEngineRecording() => Engine.FInRecordingState();

    public bool IsInputEmpty() => Engine.IsInputEmpty();

    public void SetInHistoryItemLoadMode(bool isHistoryItemLoadMode) => _inHistoryItemLoadMode = isHistoryItemLoadMode;

    public List<IExpressionCommand> GetDisplayCommandsSnapshot() => Engine.GetHistoryCollectorCommandsSnapshot();

    /// <summary>Call after the user changed the regional settings (decimal / grouping separators).</summary>
    public void SettingsChanged()
    {
        _standardCalculatorEngine?.SettingsChanged();
        _scientificCalculatorEngine?.SettingsChanged();
        _programmerCalculatorEngine?.SettingsChanged();
    }
}
