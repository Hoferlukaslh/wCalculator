// Port of CalcManager ExpressionCommand.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

public sealed class CParentheses : IParenthesisCommand
{
    private readonly int _command;

    public CParentheses(int command)
    {
        _command = command;
    }

    public int GetCommand() => _command;

    public CommandType GetCommandType() => CommandType.Parentheses;

    public void Accept(ISerializeCommandVisitor commandVisitor) => commandVisitor.Visit(this);
}

public sealed class CUnaryCommand : IUnaryCommand
{
    private readonly List<int> _command = new List<int>();

    public CUnaryCommand(int command)
    {
        _command.Add(command);
    }

    public CUnaryCommand(int command1, int command2)
    {
        _command.Add(command1);
        _command.Add(command2);
    }

    public List<int> GetCommands() => _command;

    public CommandType GetCommandType() => CommandType.UnaryCommand;

    public void SetCommand(int command)
    {
        _command.Clear();
        _command.Add(command);
    }

    public void SetCommands(int command1, int command2)
    {
        _command.Clear();
        _command.Add(command1);
        _command.Add(command2);
    }

    public void Accept(ISerializeCommandVisitor commandVisitor) => commandVisitor.Visit(this);
}

public sealed class CBinaryCommand : IBinaryCommand
{
    private int _command;

    public CBinaryCommand(int command)
    {
        _command = command;
    }

    public void SetCommand(int command) => _command = command;

    public int GetCommand() => _command;

    public CommandType GetCommandType() => CommandType.BinaryCommand;

    public void Accept(ISerializeCommandVisitor commandVisitor) => commandVisitor.Visit(this);
}

public sealed class COpndCommand : IOpndCommand
{
    private const char ChNegate = '-';
    private const char ChExp = 'e';
    private const char ChPlus = '+';

    private List<int> _commands;
    private bool _fNegative;
    private bool _fSciFmt;
    private bool _fDecimal;
    private bool _fInitialized;
    private string _token = string.Empty;
    private Rational _value = new Rational();

    public COpndCommand(List<int> commands, bool fNegative, bool fDecimal, bool fSciFmt)
    {
        _commands = commands;
        _fNegative = fNegative;
        _fSciFmt = fSciFmt;
        _fDecimal = fDecimal;
        _fInitialized = false;
    }

    public void Initialize(Rational rat)
    {
        _value = rat;
        _fInitialized = true;
    }

    public List<int> GetCommands() => _commands;

    public void SetCommands(List<int> commands) => _commands = commands;

    public void AppendCommand(int command)
    {
        if (_fSciFmt)
        {
            ClearAllAndAppendCommand((Command)command);
        }
        else
        {
            _commands.Add(command);
        }

        if (command == CCommand.IDC_PNT)
        {
            _fDecimal = true;
        }
    }

    public void ToggleSign()
    {
        foreach (int nOpCode in _commands)
        {
            if (nOpCode != CCommand.IDC_0)
            {
                _fNegative = !_fNegative;
                break;
            }
        }
    }

    public void RemoveFromEnd()
    {
        if (_fSciFmt)
        {
            ClearAllAndAppendCommand(Command.Command0);
        }
        else
        {
            int nCommands = _commands.Count;

            if (nCommands == 1)
            {
                ClearAllAndAppendCommand(Command.Command0);
            }
            else
            {
                int nOpCode = _commands[nCommands - 1];

                if (nOpCode == CCommand.IDC_PNT)
                {
                    _fDecimal = false;
                }

                _commands.RemoveAt(nCommands - 1);
            }
        }
    }

    public bool IsNegative() => _fNegative;

    public bool IsSciFmt() => _fSciFmt;

    public bool IsDecimalPresent() => _fDecimal;

    public CommandType GetCommandType() => CommandType.OperandCommand;

    private void ClearAllAndAppendCommand(Command command)
    {
        _commands.Clear();
        _commands.Add((int)command);
        _fSciFmt = false;
        _fNegative = false;
        _fDecimal = false;
    }

    public string GetToken(char decimalSymbol)
    {
        var token = new StringBuilder();
        int nCommands = _commands.Count;

        for (int i = 0; i < nCommands; i++)
        {
            int nOpCode = _commands[i];

            if (nOpCode == CCommand.IDC_PNT)
            {
                token.Append(decimalSymbol);
            }
            else if (nOpCode == CCommand.IDC_EXP)
            {
                token.Append(ChExp);
                int nextOpCode = _commands[i + 1];
                if (nextOpCode != CCommand.IDC_SIGN)
                {
                    token.Append(ChPlus);
                }
            }
            else if (nOpCode == CCommand.IDC_SIGN)
            {
                token.Append(ChNegate);
            }
            else
            {
                token.Append((nOpCode - CCommand.IDC_0).ToString(CultureInfo.InvariantCulture));
            }
        }

        // Remove zeros
        string s = token.ToString();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] != '0')
            {
                if (s[i] == decimalSymbol)
                {
                    // C++ erase(0, i - 1) with i == 0 wraps to npos and erases everything.
                    s = i == 0 ? string.Empty : s.Substring(i - 1);
                }
                else
                {
                    s = s.Substring(i);
                }

                if (_fNegative)
                {
                    s = ChNegate + s;
                }

                _token = s;
                return _token;
            }
        }

        _token = "0";
        return _token;
    }

    public string GetString(uint radix, int precision)
    {
        if (_fInitialized)
        {
            return _value.ToString(radix, NumberFormat.Float, precision);
        }

        return string.Empty;
    }

    public void Accept(ISerializeCommandVisitor commandVisitor) => commandVisitor.Visit(this);
}
