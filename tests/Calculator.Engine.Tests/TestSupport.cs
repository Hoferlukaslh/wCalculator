using System.Collections.Generic;
using Calculator.Engine;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Calculator.Engine.Tests;

internal sealed class TestResources : IResourceProvider
{
    public string GetCEngineString(string id) => id switch
    {
        "sDecimal" => ".",
        "sThousand" => ",",
        "sGrouping" => "3;0",
        "11" => "÷",
        "12" => "×",
        "13" => "+",
        "14" => "-",
        "101" => "Result is undefined",
        "100" => "Invalid input",
        "99" => "Cannot divide by zero",
        "107" => "Overflow",
        _ => string.Empty,
    };
}

internal sealed class TestDisplay : ICalcDisplay
{
    public string Primary { get; private set; } = "0";

    public bool IsError { get; private set; }

    public string Expression { get; private set; } = string.Empty;

    public int HistoryAdded { get; private set; }

    public List<string> Memory { get; private set; } = new List<string>();

    public int ParenCount { get; private set; }

    public void SetPrimaryDisplay(string text, bool isError)
    {
        Primary = text;
        IsError = isError;
    }

    public void SetIsInError(bool isInError) => IsError = isInError;

    public void SetExpressionDisplay(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands)
    {
        var parts = new List<string>();
        foreach ((string text, int _) in tokens)
        {
            parts.Add(text);
        }

        Expression = string.Join(" ", parts);
    }

    public void SetParenthesisNumber(int count) => ParenCount = count;

    public void OnNoRightParenAdded()
    {
    }

    public void MaxDigitsReached()
    {
    }

    public void BinaryOperatorReceived()
    {
    }

    public void OnHistoryItemAdded(int addedItemIndex) => HistoryAdded++;

    public void SetMemorizedNumbers(List<string> memorizedNumbers) => Memory = memorizedNumbers;

    public void MemoryItemChanged(int indexOfMemory)
    {
    }

    public void InputChanged()
    {
    }
}

internal sealed class Calc
{
    public Calc(CalcKind kind)
    {
        Display = new TestDisplay();
        Manager = new CalculatorManager(Display, new TestResources());
        Manager.Reset();
        switch (kind)
        {
            case CalcKind.Scientific:
                Manager.SendCommand(Command.ModeScientific);
                break;
            case CalcKind.Programmer:
                Manager.SendCommand(Command.ModeProgrammer);
                break;
            default:
                Manager.SendCommand(Command.ModeBasic);
                break;
        }
    }

    public enum CalcKind
    {
        Standard,
        Scientific,
        Programmer,
    }

    public TestDisplay Display { get; }

    public CalculatorManager Manager { get; }

    public string Primary => Display.Primary;

    public Calc Send(params Command[] commands)
    {
        foreach (Command c in commands)
        {
            Manager.SendCommand(c);
        }

        return this;
    }

    /// <summary>Types a string such as "12+3*4=" (digits, + - * / = . and hex digits A-F).</summary>
    public Calc Type(string keys)
    {
        foreach (char ch in keys)
        {
            Command c = ch switch
            {
                >= '0' and <= '9' => (Command)((int)Command.Command0 + (ch - '0')),
                >= 'A' and <= 'F' => (Command)((int)Command.CommandA + (ch - 'A')),
                '+' => Command.CommandADD,
                '-' => Command.CommandSUB,
                '*' => Command.CommandMUL,
                '/' => Command.CommandDIV,
                '=' => Command.CommandEQU,
                '.' => Command.CommandPNT,
                '(' => Command.CommandOPENP,
                ')' => Command.CommandCLOSEP,
                'n' => Command.CommandSIGN,
                _ => throw new System.ArgumentException("Unsupported key " + ch),
            };
            Manager.SendCommand(c);
        }

        return this;
    }
}
