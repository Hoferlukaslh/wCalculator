using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Calculator.Engine;
using CalcApp.Localization;

namespace CalcApp.ViewModels;

public enum CalcKind
{
    Standard,
    Scientific,
    Programmer,
}

public sealed class MemoryItem : ViewModelBase
{
    private string _value = string.Empty;

    public int Index { get; set; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}

public sealed class HistoryEntry
{
    public HistoryEntry(int index, HistoryItem item)
    {
        Index = index;
        Item = item;
        Expression = item.Expression;
        Result = item.Result;
    }

    public int Index { get; }

    public HistoryItem Item { get; }

    public string Expression { get; }

    public string Result { get; }
}

public sealed class BitItem : ViewModelBase
{
    private bool _isSet;

    public BitItem(int index)
    {
        Index = index;
    }

    public int Index { get; }

    public bool IsSet
    {
        get => _isSet;
        set
        {
            if (SetProperty(ref _isSet, value))
            {
                OnPropertyChanged(nameof(Text));
            }
        }
    }

    public string Text => _isSet ? "1" : "0";
}

/// <summary>
/// View model of the Standard / Scientific / Programmer calculators. It drives a <see cref="CalculatorManager"/>
/// and receives the display updates through <see cref="ICalcDisplay"/>.
/// </summary>
public sealed class CalculatorViewModel : ViewModelBase, ICalcDisplay
{
    // Assigned at the end of the constructor, after the commands that use it (null! keeps the nullable analysis quiet).
    private readonly CalculatorManager _manager = null!;
    private readonly Loc _loc = Loc.Instance;

    private CalcKind _kind = CalcKind.Standard;
    private string _displayValue = "0";
    private string _expressionText = string.Empty;
    private bool _isError;
    private int _openParen;
    private bool _is2nd;
    private bool _isHyp;
    private bool _isFE;
    private Command _angle = Command.CommandDEG;
    private bool _isInputEmpty = true;

    private RadixType _radix = RadixType.Decimal;
    private Command _wordSize = Command.CommandQword;
    private string _hexText = "0";
    private string _decText = "0";
    private string _octText = "0";
    private string _binText = "0";
    private bool _isBitFlip;

    private bool _isHistoryPanelOpen;
    private bool _isMemoryPanelOpen;

    public CalculatorViewModel()
    {
        MemoryItems = new ObservableCollection<MemoryItem>();
        HistoryItems = new ObservableCollection<HistoryEntry>();
        Bits = new ObservableCollection<BitItem>();
        RebuildBits();

        PressCommand = new RelayCommand(p =>
        {
            if (p is Command c)
            {
                Press(c);
            }
        });
        MemoryStoreCommand = new RelayCommand(() => _manager.MemorizeNumber());
        MemoryRecallCommand = new RelayCommand(() => Press(Command.CommandNULL, MemoryAction.Recall));
        MemoryAddCommand = new RelayCommand(() => Press(Command.CommandNULL, MemoryAction.Add));
        MemorySubtractCommand = new RelayCommand(() => Press(Command.CommandNULL, MemoryAction.Subtract));
        MemoryClearAllCommand = new RelayCommand(() =>
        {
            _manager.MemorizedNumberClearAll();
        });
        MemoryItemLoadCommand = new RelayCommand(p =>
        {
            if (p is MemoryItem m)
            {
                _manager.MemorizedNumberLoad(m.Index);
                IsMemoryPanelOpen = false;
            }
        });
        MemoryItemAddCommand = new RelayCommand(p =>
        {
            if (p is MemoryItem m)
            {
                _manager.MemorizedNumberAdd(m.Index);
            }
        });
        MemoryItemSubtractCommand = new RelayCommand(p =>
        {
            if (p is MemoryItem m)
            {
                _manager.MemorizedNumberSubtract(m.Index);
            }
        });
        MemoryItemClearCommand = new RelayCommand(p =>
        {
            if (p is MemoryItem m)
            {
                _manager.MemorizedNumberClear(m.Index);
                _manager.SetMemorizedNumbersString();
            }
        });
        HistoryItemSelectCommand = new RelayCommand(p =>
        {
            if (p is HistoryEntry h)
            {
                LoadHistoryItem(h.Item);
                IsHistoryPanelOpen = false;
            }
        });
        HistoryItemDeleteCommand = new RelayCommand(p =>
        {
            if (p is HistoryEntry h)
            {
                _manager.RemoveHistoryItem(h.Index);
                RefreshHistory();
            }
        });
        HistoryClearCommand = new RelayCommand(() =>
        {
            _manager.ClearHistory();
            RefreshHistory();
        });
        ToggleSecondCommand = new RelayCommand(() => Is2nd = !Is2nd);
        ToggleHypCommand = new RelayCommand(() => IsHyp = !IsHyp);
        ToggleFECommand = new RelayCommand(() =>
        {
            IsFE = !IsFE;
            Press(Command.CommandFE, skipFlags: true);
        });
        CycleAngleCommand = new RelayCommand(CycleAngle);
        SetRadixCommand = new RelayCommand(p =>
        {
            if (p is RadixType r)
            {
                SetRadix(r);
            }
        });
        CycleWordSizeCommand = new RelayCommand(CycleWordSize);
        ToggleBitCommand = new RelayCommand(p =>
        {
            if (p is BitItem b)
            {
                Press((Command)((int)Command.CommandBINPOS0 + b.Index));
            }
        });
        ToggleBitFlipCommand = new RelayCommand(() => IsBitFlip = !IsBitFlip);
        ToggleHistoryCommand = new RelayCommand(() => IsHistoryPanelOpen = !IsHistoryPanelOpen);
        ToggleMemoryCommand = new RelayCommand(() => IsMemoryPanelOpen = !IsMemoryPanelOpen);

        _manager = new CalculatorManager(this, _loc);
        _manager.Reset();
        _angle = Command.CommandDEG;
        RefreshAll();
    }

    // ------------------------------------------------------------------ commands

    public ICommand PressCommand { get; }

    public ICommand MemoryStoreCommand { get; }

    public ICommand MemoryRecallCommand { get; }

    public ICommand MemoryAddCommand { get; }

    public ICommand MemorySubtractCommand { get; }

    public ICommand MemoryClearAllCommand { get; }

    public ICommand MemoryItemLoadCommand { get; }

    public ICommand MemoryItemAddCommand { get; }

    public ICommand MemoryItemSubtractCommand { get; }

    public ICommand MemoryItemClearCommand { get; }

    public ICommand HistoryItemSelectCommand { get; }

    public ICommand HistoryItemDeleteCommand { get; }

    public ICommand HistoryClearCommand { get; }

    public ICommand ToggleSecondCommand { get; }

    public ICommand ToggleHypCommand { get; }

    public ICommand ToggleFECommand { get; }

    public ICommand CycleAngleCommand { get; }

    public ICommand SetRadixCommand { get; }

    public ICommand CycleWordSizeCommand { get; }

    public ICommand ToggleBitCommand { get; }

    public ICommand ToggleBitFlipCommand { get; }

    public ICommand ToggleHistoryCommand { get; }

    public ICommand ToggleMemoryCommand { get; }

    // ------------------------------------------------------------------ state

    public ObservableCollection<MemoryItem> MemoryItems { get; }

    public ObservableCollection<HistoryEntry> HistoryItems { get; }

    public ObservableCollection<BitItem> Bits { get; }

    public int BitColumns => Math.Min(16, Bits.Count == 0 ? 16 : Bits.Count);

    public CalcKind Kind => _kind;

    public bool IsStandard => _kind == CalcKind.Standard;

    public bool IsScientific => _kind == CalcKind.Scientific;

    public bool IsProgrammer => _kind == CalcKind.Programmer;

    public bool IsStandardOrScientific => _kind != CalcKind.Programmer;

    public bool HasHistory => _kind != CalcKind.Programmer;

    public string Title => _kind switch
    {
        CalcKind.Standard => _loc.Get("Standard"),
        CalcKind.Scientific => _loc.Get("Scientific"),
        _ => _loc.Get("Programmer"),
    };

    public string DisplayValue
    {
        get => _displayValue;
        private set => SetProperty(ref _displayValue, value);
    }

    public string ExpressionText
    {
        get => _expressionText;
        private set => SetProperty(ref _expressionText, value);
    }

    public bool IsError
    {
        get => _isError;
        private set => SetProperty(ref _isError, value);
    }

    public int OpenParen
    {
        get => _openParen;
        private set
        {
            if (SetProperty(ref _openParen, value))
            {
                OnPropertyChanged(nameof(OpenParenText));
                OnPropertyChanged(nameof(HasOpenParen));
            }
        }
    }

    public bool HasOpenParen => _openParen > 0;

    public string OpenParenText => _openParen > 0 ? "(" + _openParen.ToString(CultureInfo.InvariantCulture) : "(";

    public bool IsInputEmpty
    {
        get => _isInputEmpty;
        private set => SetProperty(ref _isInputEmpty, value);
    }

    public bool HasMemory => MemoryItems.Count > 0;

    public bool IsMemoryEmpty => MemoryItems.Count == 0;

    public bool IsHistoryEmpty => HistoryItems.Count == 0;

    public bool IsHistoryPanelOpen
    {
        get => _isHistoryPanelOpen;
        set
        {
            if (SetProperty(ref _isHistoryPanelOpen, value) && value)
            {
                IsMemoryPanelOpen = false;
            }

            OnPropertyChanged(nameof(IsAnyPanelOpen));
        }
    }

    public bool IsMemoryPanelOpen
    {
        get => _isMemoryPanelOpen;
        set
        {
            if (SetProperty(ref _isMemoryPanelOpen, value) && value)
            {
                IsHistoryPanelOpen = false;
            }

            OnPropertyChanged(nameof(IsAnyPanelOpen));
        }
    }

    public bool IsAnyPanelOpen => _isHistoryPanelOpen || _isMemoryPanelOpen;

    // ---- scientific

    public bool Is2nd
    {
        get => _is2nd;
        set
        {
            if (SetProperty(ref _is2nd, value))
            {
                RaiseFunctionLabels();
            }
        }
    }

    public bool IsHyp
    {
        get => _isHyp;
        set
        {
            if (SetProperty(ref _isHyp, value))
            {
                RaiseFunctionLabels();
            }
        }
    }

    public bool IsFE
    {
        get => _isFE;
        set => SetProperty(ref _isFE, value);
    }

    public string AngleLabel => _angle switch
    {
        Command.CommandRAD => "RAD",
        Command.CommandGRAD => "GRAD",
        _ => "DEG",
    };

    public Command SquareCmd => _is2nd ? Command.CommandCUB : Command.CommandSQR;

    public string SquareLabel => _is2nd ? "x³" : "x²";

    public Command RootCmd => _is2nd ? Command.CommandCUBEROOT : Command.CommandSQRT;

    public string RootLabel => _is2nd ? "∛x" : "√x";

    public Command PowCmd => _is2nd ? Command.CommandROOT : Command.CommandPWR;

    public string PowLabel => _is2nd ? "y√x" : "x^y";

    public Command TenPowCmd => _is2nd ? Command.CommandPOW2 : Command.CommandPOW10;

    public string TenPowLabel => _is2nd ? "2^x" : "10^x";

    public Command LogCmd => _is2nd ? Command.CommandLogBaseY : Command.CommandLOG;

    public string LogLabel => _is2nd ? "log_y x" : "log";

    public Command LnCmd => _is2nd ? Command.CommandPOWE : Command.CommandLN;

    public string LnLabel => _is2nd ? "e^x" : "ln";

    public Command SinCmd => Trig(Command.CommandSIN, Command.CommandASIN, Command.CommandSINH, Command.CommandASINH);

    public Command CosCmd => Trig(Command.CommandCOS, Command.CommandACOS, Command.CommandCOSH, Command.CommandACOSH);

    public Command TanCmd => Trig(Command.CommandTAN, Command.CommandATAN, Command.CommandTANH, Command.CommandATANH);

    public Command SecCmd => Trig(Command.CommandSEC, Command.CommandASEC, Command.CommandSECH, Command.CommandASECH);

    public Command CscCmd => Trig(Command.CommandCSC, Command.CommandACSC, Command.CommandCSCH, Command.CommandACSCH);

    public Command CotCmd => Trig(Command.CommandCOT, Command.CommandACOT, Command.CommandCOTH, Command.CommandACOTH);

    public string SinLabel => TrigLabel("sin");

    public string CosLabel => TrigLabel("cos");

    public string TanLabel => TrigLabel("tan");

    public string SecLabel => TrigLabel("sec");

    public string CscLabel => TrigLabel("csc");

    public string CotLabel => TrigLabel("cot");

    private Command Trig(Command plain, Command inverse, Command hyp, Command inverseHyp) =>
        _isHyp ? (_is2nd ? inverseHyp : hyp) : (_is2nd ? inverse : plain);

    private string TrigLabel(string name) => name + (_isHyp ? "h" : string.Empty) + (_is2nd ? "⁻¹" : string.Empty);

    private void RaiseFunctionLabels()
    {
        foreach (string n in new[]
        {
            nameof(SquareCmd), nameof(SquareLabel), nameof(RootCmd), nameof(RootLabel), nameof(PowCmd), nameof(PowLabel),
            nameof(TenPowCmd), nameof(TenPowLabel), nameof(LogCmd), nameof(LogLabel), nameof(LnCmd), nameof(LnLabel),
            nameof(SinCmd), nameof(CosCmd), nameof(TanCmd), nameof(SecCmd), nameof(CscCmd), nameof(CotCmd),
            nameof(SinLabel), nameof(CosLabel), nameof(TanLabel), nameof(SecLabel), nameof(CscLabel), nameof(CotLabel),
        })
        {
            OnPropertyChanged(n);
        }
    }

    // ---- programmer

    public RadixType Radix => _radix;

    public bool IsHexRadix => _radix == RadixType.Hex;

    public bool IsDecRadix => _radix == RadixType.Decimal;

    public bool IsOctRadix => _radix == RadixType.Octal;

    public bool IsBinRadix => _radix == RadixType.Binary;

    public bool IsRadixAtLeastOctal => _radix != RadixType.Binary;

    public bool IsRadixAtLeastDecimal => _radix == RadixType.Decimal || _radix == RadixType.Hex;

    public bool IsDecimalPointEnabled => _kind != CalcKind.Programmer;

    public string HexText
    {
        get => _hexText;
        private set => SetProperty(ref _hexText, value);
    }

    public string DecText
    {
        get => _decText;
        private set => SetProperty(ref _decText, value);
    }

    public string OctText
    {
        get => _octText;
        private set => SetProperty(ref _octText, value);
    }

    public string BinText
    {
        get => _binText;
        private set => SetProperty(ref _binText, value);
    }

    public string WordSizeLabel => _wordSize switch
    {
        Command.CommandDword => "DWORD",
        Command.CommandWord => "WORD",
        Command.CommandByte => "BYTE",
        _ => "QWORD",
    };

    public bool IsBitFlip
    {
        get => _isBitFlip;
        set
        {
            if (SetProperty(ref _isBitFlip, value))
            {
                OnPropertyChanged(nameof(IsKeypadVisible));
                OnPropertyChanged(nameof(IsBitPanelVisible));
                OnPropertyChanged(nameof(IsProgrammerPanelVisible));
            }
        }
    }

    public bool IsKeypadVisible => !(_kind == CalcKind.Programmer && _isBitFlip);

    public bool IsBitPanelVisible => _kind == CalcKind.Programmer && _isBitFlip;

    public bool IsProgrammerPanelVisible => _kind == CalcKind.Programmer && !_isBitFlip;

    public bool HasSidePanel => _kind != CalcKind.Standard;

    // ------------------------------------------------------------------ mode switching

    public void SetKind(CalcKind kind)
    {
        _kind = kind;
        IsHistoryPanelOpen = false;
        IsMemoryPanelOpen = false;
        _is2nd = false;
        _isHyp = false;
        _isFE = false;
        _isBitFlip = false;
        _openParen = 0;
        _angle = Command.CommandDEG;
        _radix = RadixType.Decimal;
        _wordSize = Command.CommandQword;

        switch (kind)
        {
            case CalcKind.Standard:
                _manager.SendCommand(Command.ModeBasic);
                break;
            case CalcKind.Scientific:
                _manager.SendCommand(Command.ModeScientific);
                break;
            default:
                _manager.SendCommand(Command.ModeProgrammer);
                _manager.SetRadix(RadixType.Decimal);
                RebuildBits();
                break;
        }

        if (kind != CalcKind.Programmer)
        {
            // Make sure the (shared) scientific engine starts in degrees.
            if (kind == CalcKind.Scientific)
            {
                _manager.SendCommand(Command.CommandDEG);
            }
        }

        ExpressionText = string.Empty;
        _manager.SetMemorizedNumbersString();
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshHistory();
        foreach (string n in new[]
        {
            nameof(Kind), nameof(IsStandard), nameof(IsScientific), nameof(IsProgrammer), nameof(IsStandardOrScientific),
            nameof(HasHistory), nameof(Title), nameof(Is2nd), nameof(IsHyp), nameof(IsFE), nameof(AngleLabel),
            nameof(OpenParen), nameof(OpenParenText), nameof(HasOpenParen), nameof(Radix), nameof(IsHexRadix), nameof(IsDecRadix),
            nameof(IsOctRadix), nameof(IsBinRadix), nameof(IsRadixAtLeastOctal), nameof(IsRadixAtLeastDecimal),
            nameof(IsDecimalPointEnabled), nameof(WordSizeLabel), nameof(IsBitFlip), nameof(IsKeypadVisible),
            nameof(BitColumns), nameof(IsAnyPanelOpen), nameof(IsBitPanelVisible), nameof(IsProgrammerPanelVisible),
            nameof(HasSidePanel),
        })
        {
            OnPropertyChanged(n);
        }

        RaiseFunctionLabels();
        UpdateProgrammerPanel();
    }

    // ------------------------------------------------------------------ input

    private enum MemoryAction
    {
        None,
        Recall,
        Add,
        Subtract,
    }

    private static bool IsRecoverable(Command c)
    {
        int v = (int)c;
        return (v >= (int)Command.Command0 && v <= (int)Command.Command9)
            || c == Command.CommandPNT
            || (v >= (int)Command.CommandBINEDITSTART && v <= (int)Command.CommandBINEDITEND)
            || (v >= (int)Command.CommandA && v <= (int)Command.CommandF);
    }

    public void Press(Command command) => Press(command, MemoryAction.None);

    private void Press(Command command, MemoryAction memory = MemoryAction.None, bool skipFlags = false)
    {
        if (_isError)
        {
            _manager.SendCommand(Command.CommandCLEAR);
            if (memory == MemoryAction.None && !IsRecoverable(command))
            {
                return;
            }
        }

        switch (memory)
        {
            case MemoryAction.Recall:
                if (MemoryItems.Count > 0)
                {
                    _manager.MemorizedNumberLoad(0);
                }

                return;
            case MemoryAction.Add:
                _manager.MemorizedNumberAdd(0);
                return;
            case MemoryAction.Subtract:
                _manager.MemorizedNumberSubtract(0);
                return;
        }

        if (!skipFlags)
        {
            if (command == Command.CommandCLEAR && _isFE)
            {
                IsFE = false;
            }

            if (command == Command.CommandDEG || command == Command.CommandRAD || command == Command.CommandGRAD)
            {
                _angle = command;
                OnPropertyChanged(nameof(AngleLabel));
            }
        }

        _manager.SendCommand(command);
    }

    private void CycleAngle()
    {
        Command next = _angle switch
        {
            Command.CommandDEG => Command.CommandRAD,
            Command.CommandRAD => Command.CommandGRAD,
            _ => Command.CommandDEG,
        };
        Press(next);
    }

    private void SetRadix(RadixType r)
    {
        if (_isError)
        {
            _manager.SendCommand(Command.CommandCLEAR);
        }

        _radix = r;
        _manager.SetRadix(r);
        foreach (string n in new[]
        {
            nameof(Radix), nameof(IsHexRadix), nameof(IsDecRadix), nameof(IsOctRadix), nameof(IsBinRadix),
            nameof(IsRadixAtLeastOctal), nameof(IsRadixAtLeastDecimal),
        })
        {
            OnPropertyChanged(n);
        }

        UpdateProgrammerPanel();
    }

    private void CycleWordSize()
    {
        Command next = _wordSize switch
        {
            Command.CommandQword => Command.CommandDword,
            Command.CommandDword => Command.CommandWord,
            Command.CommandWord => Command.CommandByte,
            _ => Command.CommandQword,
        };
        _wordSize = next;
        Press(next);
        RebuildBits();
        OnPropertyChanged(nameof(WordSizeLabel));
        OnPropertyChanged(nameof(BitColumns));
        UpdateProgrammerPanel();
    }

    private int WordBits => _wordSize switch
    {
        Command.CommandDword => 32,
        Command.CommandWord => 16,
        Command.CommandByte => 8,
        _ => 64,
    };

    private void RebuildBits()
    {
        Bits.Clear();
        for (int i = WordBits - 1; i >= 0; i--)
        {
            Bits.Add(new BitItem(i));
        }
    }

    // ------------------------------------------------------------------ text input (keyboard / paste)

    /// <summary>Maps a typed character to a calculator command; returns false when the character is not a command.</summary>
    public bool TryMapCharacter(char ch, out Command command)
    {
        command = Command.CommandNULL;
        if (ch >= '0' && ch <= '9')
        {
            command = (Command)((int)Command.Command0 + (ch - '0'));
            return true;
        }

        if (_kind == CalcKind.Programmer && _radix == RadixType.Hex)
        {
            char u = char.ToUpperInvariant(ch);
            if (u >= 'A' && u <= 'F')
            {
                command = (Command)((int)Command.CommandA + (u - 'A'));
                return true;
            }
        }

        char dec = _manager.DecimalSeparator();
        if ((ch == '.' || ch == ',' || ch == dec) && _kind != CalcKind.Programmer)
        {
            command = Command.CommandPNT;
            return true;
        }

        switch (ch)
        {
            case '+': command = Command.CommandADD; return true;
            case '-': case '−': command = Command.CommandSUB; return true;
            case '*': case '×': command = Command.CommandMUL; return true;
            case '/': case '÷': command = Command.CommandDIV; return true;
            case '=': command = Command.CommandEQU; return true;
            case '%': command = _kind == CalcKind.Programmer ? Command.CommandMOD : Command.CommandPERCENT; return true;
            case '(' when _kind != CalcKind.Standard: command = Command.CommandOPENP; return true;
            case ')' when _kind != CalcKind.Standard: command = Command.CommandCLOSEP; return true;
            case '^' when _kind == CalcKind.Scientific: command = Command.CommandPWR; return true;
            case '!' when _kind == CalcKind.Scientific: command = Command.CommandFAC; return true;
            case '@': command = Command.CommandSQRT; return true;
            case '&' when _kind == CalcKind.Programmer: command = Command.CommandAnd; return true;
            case '|' when _kind == CalcKind.Programmer: command = Command.CommandOR; return true;
            case '~' when _kind == CalcKind.Programmer: command = Command.CommandNot; return true;
            case 'e' when _kind == CalcKind.Scientific: command = Command.CommandEXP; return true;
            case 'p' when _kind == CalcKind.Scientific: command = Command.CommandPI; return true;
            case 'r': command = Command.CommandREC; return true;
            case 'q': command = Command.CommandSQR; return true;
            case 'm' when _kind == CalcKind.Scientific: command = Command.CommandMOD; return true;
            case 'l' when _kind == CalcKind.Scientific: command = Command.CommandLOG; return true;
            case 'n' when _kind == CalcKind.Scientific: command = Command.CommandLN; return true;
            case 's' when _kind == CalcKind.Scientific: command = Command.CommandSIN; return true;
            case 'o' when _kind == CalcKind.Scientific: command = Command.CommandCOS; return true;
            case 't' when _kind == CalcKind.Scientific: command = Command.CommandTAN; return true;
        }

        return false;
    }

    /// <summary>Handles typed text (from the keyboard); returns true when something was processed.</summary>
    public bool HandleText(string text)
    {
        bool any = false;
        foreach (char ch in text)
        {
            if (TryMapCharacter(ch, out Command c))
            {
                Press(c);
                any = true;
            }
        }

        return any;
    }

    public void PressBackspace() => Press(Command.CommandBACK);

    public void PressClear() => Press(Command.CommandCLEAR);

    public void PressClearEntry() => Press(Command.CommandCENTR);

    public void PressEquals() => Press(Command.CommandEQU);

    /// <summary>Pastes a number (or a simple expression) into the calculator. Returns false if it could not be understood.</summary>
    public bool Paste(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string t = text.Trim();
        char groupSep = _loc.GetCEngineString("sThousand").FirstOrDefault();
        char decSep = _manager.DecimalSeparator();
        var commands = new List<Command>();
        bool negativePending = false;
        bool inExponent = false;
        bool expectOperand = true;

        for (int i = 0; i < t.Length; i++)
        {
            char ch = t[i];
            if (char.IsWhiteSpace(ch) || ch == ' ' || ch == ' ' || ch == '_' || (groupSep != '\0' && ch == groupSep && groupSep != decSep && ch != '.'))
            {
                continue;
            }

            if ((ch == '-' || ch == '−') && expectOperand && !inExponent)
            {
                negativePending = !negativePending;
                continue;
            }

            if (ch == '+' && expectOperand && !inExponent)
            {
                continue;
            }

            if ((ch == 'e' || ch == 'E') && _radix == RadixType.Decimal && _kind != CalcKind.Programmer && !expectOperand && !inExponent)
            {
                commands.Add(Command.CommandEXP);
                inExponent = true;
                if (i + 1 < t.Length && (t[i + 1] == '-' || t[i + 1] == '+'))
                {
                    if (t[i + 1] == '-')
                    {
                        commands.Add(Command.CommandSIGN);
                    }

                    i++;
                }

                continue;
            }

            if (ch == '.' || ch == ',' || ch == decSep)
            {
                if (_kind == CalcKind.Programmer || inExponent)
                {
                    return false;
                }

                commands.Add(Command.CommandPNT);
                expectOperand = false;
                continue;
            }

            if (ch >= '0' && ch <= '9')
            {
                commands.Add((Command)((int)Command.Command0 + (ch - '0')));
                if (negativePending)
                {
                    commands.Add(Command.CommandSIGN);
                    negativePending = false;
                }

                expectOperand = false;
                continue;
            }

            if (_kind == CalcKind.Programmer && _radix == RadixType.Hex && char.ToUpperInvariant(ch) >= 'A' && char.ToUpperInvariant(ch) <= 'F')
            {
                commands.Add((Command)((int)Command.CommandA + (char.ToUpperInvariant(ch) - 'A')));
                if (negativePending)
                {
                    commands.Add(Command.CommandSIGN);
                    negativePending = false;
                }

                expectOperand = false;
                continue;
            }

            if (TryMapCharacter(ch, out Command op) && (op == Command.CommandADD || op == Command.CommandSUB || op == Command.CommandMUL || op == Command.CommandDIV))
            {
                commands.Add(op);
                expectOperand = true;
                inExponent = false;
                continue;
            }

            return false;
        }

        foreach (Command c in commands)
        {
            Press(c);
        }

        return commands.Count > 0;
    }

    // ------------------------------------------------------------------ history

    private static List<int> Flatten(IEnumerable<IExpressionCommand> commands)
    {
        var result = new List<int>();
        foreach (IExpressionCommand c in commands)
        {
            switch (c)
            {
                case IUnaryCommand u:
                    result.AddRange(u.GetCommands());
                    break;
                case IBinaryCommand b:
                    result.Add(b.GetCommand());
                    break;
                case IParenthesisCommand p:
                    result.Add(p.GetCommand());
                    break;
                case IOpndCommand o:
                {
                    bool needSign = o.IsNegative();
                    foreach (int code in o.GetCommands())
                    {
                        result.Add(code);
                        if (needSign && code != CCommandZero)
                        {
                            result.Add((int)Command.CommandSIGN);
                            needSign = false;
                        }
                    }

                    break;
                }
            }
        }

        return result;
    }

    private const int CCommandZero = (int)Command.Command0;

    private void LoadHistoryItem(HistoryItem item)
    {
        // Replay the stored expression in the current engine, then show its result.
        int angle = (int)_manager.GetCurrentDegreeMode();
        List<int> commands = Flatten(item.Commands);

        _manager.SetInHistoryItemLoadMode(true);
        try
        {
            _manager.Reset(false);
            if (_kind == CalcKind.Scientific)
            {
                _manager.SendCommand(Command.ModeScientific);
            }

            if (_isFE)
            {
                _manager.SendCommand(Command.CommandFE);
            }

            _manager.SendCommand((Command)angle);
            foreach (int c in commands)
            {
                _manager.SendCommand((Command)c);
            }

            // The original application presses the FE key twice to force the result to be displayed.
            _manager.SendCommand(Command.CommandFE);
            _manager.SendCommand(Command.CommandFE);
        }
        finally
        {
            _manager.SetInHistoryItemLoadMode(false);
        }

        ExpressionText = JoinTokens(item.Tokens);
        DisplayValue = item.Result;
        IsError = false;
    }

    private void RefreshHistory()
    {
        HistoryItems.Clear();
        if (_kind != CalcKind.Programmer)
        {
            IReadOnlyList<HistoryItem> items = _manager.GetHistoryItems();
            // Most recent first.
            for (int i = items.Count - 1; i >= 0; i--)
            {
                HistoryItems.Add(new HistoryEntry(i, items[i]));
            }
        }

        OnPropertyChanged(nameof(IsHistoryEmpty));
    }

    private static string JoinTokens(List<(string Text, int CommandIndex)> tokens) =>
        string.Join(" ", tokens.Select(t => t.Text));

    // ------------------------------------------------------------------ programmer panel

    private void UpdateProgrammerPanel()
    {
        if (_kind != CalcKind.Programmer)
        {
            return;
        }

        const int precision = 64;
        string hex = string.Empty, dec = string.Empty, oct = string.Empty, bin = string.Empty;
        string rawBin = string.Empty;
        if (!_isError)
        {
            hex = _manager.GetResultForRadix(16, precision, true);
            if (string.IsNullOrEmpty(hex))
            {
                hex = dec = oct = bin = _displayValue;
            }
            else
            {
                dec = _manager.GetResultForRadix(10, precision, true);
                oct = _manager.GetResultForRadix(8, precision, true);
                bin = _manager.GetResultForRadix(2, precision, true);
                rawBin = _manager.GetResultForRadix(2, precision, false);
            }
        }

        HexText = hex;
        DecText = dec;
        OctText = oct;
        BinText = bin;

        int n = Bits.Count;
        for (int i = 0; i < n; i++)
        {
            BitItem b = Bits[i];
            int pos = rawBin.Length - 1 - b.Index;
            b.IsSet = pos >= 0 && pos < rawBin.Length && rawBin[pos] == '1';
        }
    }

    // ------------------------------------------------------------------ ICalcDisplay

    void ICalcDisplay.SetPrimaryDisplay(string text, bool isError)
    {
        DisplayValue = text;
        IsError = isError;
        UpdateProgrammerPanel();
    }

    void ICalcDisplay.SetIsInError(bool isInError) => IsError = isInError;

    void ICalcDisplay.SetExpressionDisplay(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands) =>
        ExpressionText = JoinTokens(tokens);

    void ICalcDisplay.SetParenthesisNumber(int count) => OpenParen = count;

    void ICalcDisplay.OnNoRightParenAdded()
    {
    }

    void ICalcDisplay.MaxDigitsReached()
    {
    }

    void ICalcDisplay.BinaryOperatorReceived()
    {
    }

    void ICalcDisplay.OnHistoryItemAdded(int addedItemIndex) => RefreshHistory();

    void ICalcDisplay.SetMemorizedNumbers(List<string> memorizedNumbers)
    {
        MemoryItems.Clear();
        for (int i = 0; i < memorizedNumbers.Count; i++)
        {
            MemoryItems.Add(new MemoryItem { Index = i, Value = memorizedNumbers[i] });
        }

        OnPropertyChanged(nameof(HasMemory));
        OnPropertyChanged(nameof(IsMemoryEmpty));
    }

    void ICalcDisplay.MemoryItemChanged(int indexOfMemory)
    {
        // The list is rebuilt by SetMemorizedNumbers; nothing else to do here.
    }

    void ICalcDisplay.InputChanged()
    {
        IsInputEmpty = _manager == null || _manager.IsInputEmpty();
    }
}
