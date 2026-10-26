// Port of CalcManager CEngine History (CHistoryCollector).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

/// <summary>
/// Helper class, really an internal class of the engine, to accumulate each history line of text by collecting the
/// operands, operator, unary operator etc. It relies on the engine calling it in appropriate order.
/// </summary>
internal sealed class CHistoryCollector
{
    /// <summary>Maximum depth you can get by precedence. It is just an array's size limit.</summary>
    public const int MAXPRECDEPTH = 25;

    private const uint E_BOUNDS = 0x8000000B;

    private IHistoryDisplay? _historyDisplay;
    private ICalcDisplay? _calcDisplay;

    // A sort of state, set to the index before 2 after 2 in the expression 2 + 3 say. Useful for auto correct portion of history and for
    // attaching the unary op around the last operand.
    private int _lastOpStartIndex;

    // Index of the beginning of the last binary operator added to the history.
    private int _lastBinOpStartIndex;

    // Stack of index of opnd's beginning for each '('.
    private readonly int[] _operandIndices = new int[MAXPRECDEPTH];
    private int _curOperandIndex;

    // True iff the last opnd in history is already braced so we can avoid putting another one for unary operator.
    private bool _lastOpndBrace;
    private char _decimalSymbol;
    private List<(string Text, int CommandIndex)>? _tokens;
    private List<IExpressionCommand>? _commands;

    public CHistoryCollector(ICalcDisplay? calcDisplay, IHistoryDisplay? historyDisplay, char decimalSymbol)
    {
        _historyDisplay = historyDisplay;
        _calcDisplay = calcDisplay;
        _decimalSymbol = decimalSymbol;
        ReinitHistory();
    }

    private static void Truncate<T>(List<T> v, uint index)
    {
        if (index >= (uint)v.Count)
        {
            throw new CalcException(E_BOUNDS);
        }

        v.RemoveRange((int)index, v.Count - (int)index);
    }

    private void ReinitHistory()
    {
        _lastOpStartIndex = -1;
        _lastBinOpStartIndex = -1;
        _curOperandIndex = 0;
        _lastOpndBrace = false;
        _tokens?.Clear();
        _commands?.Clear();
    }

    public void AddOpndToHistory(string numStr, Rational rat, bool fRepetition = false)
    {
        int iCommandEnd = AddCommand(GetOperandCommandsFromString(numStr, rat));
        _lastOpStartIndex = IchAddSzToEquationSz(numStr, iCommandEnd);

        if (fRepetition)
        {
            SetExpressionDisplay();
        }

        _lastOpndBrace = false;
        _lastBinOpStartIndex = -1;
    }

    public void RemoveLastOpndFromHistory()
    {
        TruncateEquationSzFromIch(_lastOpStartIndex);
        SetExpressionDisplay();
        _lastOpStartIndex = -1;

        // This will not restore the _lastBinOpStartIndex, as it isn't possible to remove that also later.
    }

    public void AddBinOpToHistory(int nOpCode, bool isIntegerMode, bool fNoRepetition = true)
    {
        int iCommandEnd = AddCommand(new CBinaryCommand(nOpCode));
        _lastBinOpStartIndex = IchAddSzToEquationSz(" ", -1);

        IchAddSzToEquationSz(CCalcEngine.OpCodeToBinaryString(nOpCode, isIntegerMode), iCommandEnd);
        IchAddSzToEquationSz(" ", -1);

        if (fNoRepetition)
        {
            SetExpressionDisplay();
        }

        _lastOpStartIndex = -1;
    }

    // This is expected to be called when a binary op in the last say 1+2+ is changing to another one say 1+2* (+ changed to *)
    // It needs to know by this change a Precedence inversion happened. i.e. previous op was lower or equal to its previous op, but the new
    // one isn't. (Eg. 1*2* to 1*2^). It can add explicit brackets to ensure the precedence is inverted. (Eg. (1*2) ^)
    public void ChangeLastBinOp(int nOpCode, bool fPrecInvToHigher, bool isIntegerMode)
    {
        TruncateEquationSzFromIch(_lastBinOpStartIndex);
        if (fPrecInvToHigher)
        {
            EnclosePrecInversionBrackets();
        }

        AddBinOpToHistory(nOpCode, isIntegerMode);
    }

    public void PushLastOpndStart(int ichOpndStart = -1)
    {
        int ich = (ichOpndStart == -1) ? _lastOpStartIndex : ichOpndStart;

        if (_curOperandIndex < _operandIndices.Length)
        {
            _operandIndices[_curOperandIndex++] = ich;
        }
    }

    public void PopLastOpndStart()
    {
        if (_curOperandIndex > 0)
        {
            _lastOpStartIndex = _operandIndices[--_curOperandIndex];
        }
    }

    public void AddOpenBraceToHistory()
    {
        AddCommand(new CParentheses(CCommand.IDC_OPENP));
        int ichOpndStart = IchAddSzToEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_OPENP), -1);
        PushLastOpndStart(ichOpndStart);

        SetExpressionDisplay();
        _lastBinOpStartIndex = -1;
    }

    public void AddCloseBraceToHistory()
    {
        AddCommand(new CParentheses(CCommand.IDC_CLOSEP));
        IchAddSzToEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
        SetExpressionDisplay();
        PopLastOpndStart();

        _lastBinOpStartIndex = -1;
        _lastOpndBrace = true;
    }

    public void EnclosePrecInversionBrackets()
    {
        // Top of the Opnd starts index or 0 is nothing is in top
        int ichStart = (_curOperandIndex > 0) ? _operandIndices[_curOperandIndex - 1] : 0;

        InsertSzInEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_OPENP), -1, ichStart);
        IchAddSzToEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
    }

    public bool FOpndAddedToHistory() => _lastOpStartIndex != -1;

    // This does the postfix to prefix translation of the input and adds the text to the history. Eg. doing 2 + 4 (sqrt),
    // this routine will ensure the last sqrt call unary operator, actually goes back in history and wraps 4 in sqrt(4)
    public void AddUnaryOpToHistory(int nOpCode, bool fInv, AngleType angletype)
    {
        int iCommandEnd;

        // When successfully applying a unary op, there should be an opnd already
        // A very special case of % which is a funny post op unary op.
        if (CCommand.IDC_PERCENT == nOpCode)
        {
            iCommandEnd = AddCommand(new CUnaryCommand(nOpCode));
            IchAddSzToEquationSz(CCalcEngine.OpCodeToString(nOpCode), iCommandEnd);
        }
        else
        {
            // all the other unary ops
            IOperatorCommand spExpressionCommand;
            if (CCommand.IDC_SIGN == nOpCode)
            {
                spExpressionCommand = new CUnaryCommand(nOpCode);
            }
            else
            {
                Command angleOpCode;
                if (angletype == AngleType.Degrees)
                {
                    angleOpCode = Command.CommandDEG;
                }
                else if (angletype == AngleType.Radians)
                {
                    angleOpCode = Command.CommandRAD;
                }
                else
                {
                    angleOpCode = Command.CommandGRAD;
                }

                int command = nOpCode;
                switch (nOpCode)
                {
                    case CCommand.IDC_SIN:
                        command = fInv ? (int)Command.CommandASIN : CCommand.IDC_SIN;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_COS:
                        command = fInv ? (int)Command.CommandACOS : CCommand.IDC_COS;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_TAN:
                        command = fInv ? (int)Command.CommandATAN : CCommand.IDC_TAN;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_SINH:
                        command = fInv ? (int)Command.CommandASINH : CCommand.IDC_SINH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_COSH:
                        command = fInv ? (int)Command.CommandACOSH : CCommand.IDC_COSH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_TANH:
                        command = fInv ? (int)Command.CommandATANH : CCommand.IDC_TANH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_SEC:
                        command = fInv ? (int)Command.CommandASEC : CCommand.IDC_SEC;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_CSC:
                        command = fInv ? (int)Command.CommandACSC : CCommand.IDC_CSC;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_COT:
                        command = fInv ? (int)Command.CommandACOT : CCommand.IDC_COT;
                        spExpressionCommand = new CUnaryCommand((int)angleOpCode, command);
                        break;
                    case CCommand.IDC_SECH:
                        command = fInv ? (int)Command.CommandASECH : CCommand.IDC_SECH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_CSCH:
                        command = fInv ? (int)Command.CommandACSCH : CCommand.IDC_CSCH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_COTH:
                        command = fInv ? (int)Command.CommandACOTH : CCommand.IDC_COTH;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    case CCommand.IDC_LN:
                        command = fInv ? (int)Command.CommandPOWE : CCommand.IDC_LN;
                        spExpressionCommand = new CUnaryCommand(command);
                        break;
                    default:
                        spExpressionCommand = new CUnaryCommand(nOpCode);
                        break;
                }
            }

            iCommandEnd = AddCommand(spExpressionCommand);

            string operandStr = CCalcEngine.OpCodeToUnaryString(nOpCode, fInv, angletype);
            if (!_lastOpndBrace)
            {
                // The opnd is already covered in braces. No need for additional braces around it
                operandStr += CCalcEngine.OpCodeToString(CCommand.IDC_OPENP);
            }

            InsertSzInEquationSz(operandStr, iCommandEnd, _lastOpStartIndex);

            if (!_lastOpndBrace)
            {
                IchAddSzToEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_CLOSEP), -1);
            }
        }

        SetExpressionDisplay();
        _lastOpndBrace = false;

        // _lastOpStartIndex remains the same as last opnd is just replaced by unaryop(lastopnd)
        _lastBinOpStartIndex = -1;
    }

    // Called after = with the result of the equation
    // Responsible for clearing the top line of current running history display, as well as adding yet another element to
    // history of equations
    public void CompleteHistoryLine(string numStr)
    {
        if (_historyDisplay != null)
        {
            int addedItemIndex = _historyDisplay.AddToHistory(
                _tokens ?? new List<(string Text, int CommandIndex)>(),
                _commands ?? new List<IExpressionCommand>(),
                numStr);
            _calcDisplay?.OnHistoryItemAdded(addedItemIndex);
        }

        _tokens = null;
        _commands = null;
        ReinitHistory();
    }

    public void CompleteEquation(string numStr)
    {
        // Add only '=' token and not add EQU command, because
        // EQU command breaks loading from history (it duplicate history entries).
        IchAddSzToEquationSz(CCalcEngine.OpCodeToString(CCommand.IDC_EQU), -1);

        SetExpressionDisplay();
        CompleteHistoryLine(numStr);
    }

    public void ClearHistoryLine(string errStr)
    {
        if (errStr.Length == 0) // in case of error let the display stay as it is
        {
            _calcDisplay?.SetExpressionDisplay(new List<(string Text, int CommandIndex)>(), new List<IExpressionCommand>());
            ReinitHistory();
        }
    }

    // Adds the given string to the globally maintained current equation at the end.
    // Also returns the 0 based index in the list just added.
    private int IchAddSzToEquationSz(string str, int icommandIndex)
    {
        _tokens ??= new List<(string Text, int CommandIndex)>();
        _tokens.Add((str, icommandIndex));
        return _tokens.Count - 1;
    }

    // Inserts a given string into the global equation at the given index ich.
    private void InsertSzInEquationSz(string str, int icommandIndex, int ich)
    {
        _tokens!.Insert(ich, (str, icommandIndex));
    }

    // Chops off the current equation from the given index
    private void TruncateEquationSzFromIch(int ich)
    {
        if (_tokens == null)
        {
            throw new CalcException(E_BOUNDS);
        }

        // Truncate commands
        int minIdx = -1;
        uint nTokens = (uint)_tokens.Count;

        for (uint i = unchecked((uint)ich); i < nTokens; i++)
        {
            int curTokenId = _tokens[(int)i].CommandIndex;
            if (curTokenId != -1)
            {
                // NOTE: faithful to the original logic, which never starts truncating the command list (minIdx stays -1).
                if ((minIdx != -1) || (curTokenId < minIdx))
                {
                    minIdx = curTokenId;
                    Truncate(_commands!, (uint)minIdx);
                }
            }
        }

        Truncate(_tokens, unchecked((uint)ich));
    }

    // Adds the equation into the running history text
    private void SetExpressionDisplay()
    {
        if (_calcDisplay != null)
        {
            _tokens ??= new List<(string Text, int CommandIndex)>();
            _commands ??= new List<IExpressionCommand>();
            _calcDisplay.SetExpressionDisplay(_tokens, _commands);
        }
    }

    public int AddCommand(IExpressionCommand spCommand)
    {
        _commands ??= new List<IExpressionCommand>();
        _commands.Add(spCommand);
        return _commands.Count - 1;
    }

    // To update the operands in the Expression according to the current Radix
    public void UpdateHistoryExpression(uint radix, int precision)
    {
        if (_tokens == null)
        {
            return;
        }

        for (int i = 0; i < _tokens.Count; i++)
        {
            int commandPosition = _tokens[i].CommandIndex;
            if (commandPosition != -1)
            {
                IExpressionCommand expCommand = _commands![commandPosition];

                if (expCommand != null && CommandType.OperandCommand == expCommand.GetCommandType())
                {
                    var opndCommand = (COpndCommand)expCommand;
                    string text = opndCommand.GetString(radix, precision);
                    _tokens[i] = (text, commandPosition);
                    opndCommand.SetCommands(GetOperandCommandsFromString(text));
                }
            }
        }

        SetExpressionDisplay();
    }

    public void SetDecimalSymbol(char decimalSymbol)
    {
        _decimalSymbol = decimalSymbol;
    }

    // Update the commands corresponding to the passed string Number
    private List<int> GetOperandCommandsFromString(string numStr)
    {
        var commands = new List<int>();

        // Check for negate
        bool fNegative = numStr.Length > 0 && numStr[0] == '-';

        for (int i = fNegative ? 1 : 0; i < numStr.Length; i++)
        {
            if (numStr[i] == _decimalSymbol)
            {
                commands.Add(CCommand.IDC_PNT);
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IDC_EXP);
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IDC_SIGN);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            else
            {
                // Number
                int num = numStr[i] - '0';
                num += CCommand.IDC_0;
                commands.Add(num);
            }
        }

        // If the number is negative, append a sign command at the end.
        if (fNegative)
        {
            commands.Add(CCommand.IDC_SIGN);
        }

        return commands;
    }

    public COpndCommand GetOperandCommandsFromString(string numStr, Rational rat)
    {
        var commands = new List<int>();

        // Check for negate
        bool fNegative = numStr.Length > 0 && numStr[0] == '-';
        bool fSciFmt = false;
        bool fDecimal = false;

        for (int i = fNegative ? 1 : 0; i < numStr.Length; i++)
        {
            if (numStr[i] == _decimalSymbol)
            {
                commands.Add(CCommand.IDC_PNT);
                if (!fSciFmt)
                {
                    fDecimal = true;
                }
            }
            else if (numStr[i] == 'e')
            {
                commands.Add(CCommand.IDC_EXP);
                fSciFmt = true;
            }
            else if (numStr[i] == '-')
            {
                commands.Add(CCommand.IDC_SIGN);
            }
            else if (numStr[i] == '+')
            {
                // Ignore.
            }
            else
            {
                // Number
                int num = numStr[i] - '0';
                num += CCommand.IDC_0;
                commands.Add(num);
            }
        }

        var operandCommand = new COpndCommand(commands, fNegative, fDecimal, fSciFmt);
        operandCommand.Initialize(rat);
        return operandCommand;
    }

    public List<IExpressionCommand> GetCommands()
    {
        var commands = new List<IExpressionCommand>();
        if (_commands != null)
        {
            commands.AddRange(_commands);
        }

        return commands;
    }
}
