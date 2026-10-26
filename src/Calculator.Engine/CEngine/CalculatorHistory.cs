// Port of CalcManager CalculatorHistory.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text;

namespace Calculator.Engine;

/// <summary>One entry of the calculator history.</summary>
public sealed class HistoryItem
{
    public List<(string Text, int CommandIndex)> Tokens { get; set; } = new List<(string Text, int CommandIndex)>();

    public List<IExpressionCommand> Commands { get; set; } = new List<IExpressionCommand>();

    public string Expression { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;
}

public sealed class CalculatorHistory : IHistoryDisplay
{
    private readonly List<HistoryItem> _historyItems = new List<HistoryItem>();

    public CalculatorHistory(int maxSize)
    {
        MaxHistorySize = maxSize;
    }

    public int MaxHistorySize { get; }

    private static string GetGeneratedExpression(List<(string Text, int CommandIndex)> tokens)
    {
        var expression = new StringBuilder();
        bool isFirst = true;

        foreach (var token in tokens)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                expression.Append(' ');
            }

            expression.Append(token.Text);
        }

        return expression.ToString();
    }

    public int AddToHistory(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands, string result)
    {
        var historyItem = new HistoryItem
        {
            Tokens = tokens,
            Commands = commands,
            Expression = GetGeneratedExpression(tokens),
            Result = result,
        };

        return AddItem(historyItem);
    }

    public int AddItem(HistoryItem historyItem)
    {
        if (_historyItems.Count >= MaxHistorySize)
        {
            _historyItems.RemoveAt(0);
        }

        _historyItems.Add(historyItem);
        return _historyItems.Count - 1;
    }

    public bool RemoveItem(int index)
    {
        if (index >= 0 && index < _historyItems.Count)
        {
            _historyItems.RemoveAt(index);
            return true;
        }

        return false;
    }

    public IReadOnlyList<HistoryItem> GetHistory() => _historyItems;

    public HistoryItem GetHistoryItem(int index) => _historyItems[index];

    public void ClearHistory() => _historyItems.Clear();
}
