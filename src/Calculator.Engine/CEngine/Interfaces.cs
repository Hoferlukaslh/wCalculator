// Port of CalcManager ICalcDisplay / IHistoryDisplay / IResourceProvider / ExpressionCommandInterface.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System.Collections.Generic;

namespace Calculator.Engine;

/// <summary>Visitor used to serialize the expression commands.</summary>
public interface ISerializeCommandVisitor
{
    void Visit(COpndCommand opndCmd);

    void Visit(CUnaryCommand unaryCmd);

    void Visit(CBinaryCommand binaryCmd);

    void Visit(CParentheses paraCmd);
}

public interface IExpressionCommand
{
    CommandType GetCommandType();

    void Accept(ISerializeCommandVisitor commandVisitor);
}

public interface IOperatorCommand : IExpressionCommand
{
    void SetCommand(int command);
}

public interface IUnaryCommand : IOperatorCommand
{
    List<int> GetCommands();

    void SetCommands(int command1, int command2);
}

public interface IBinaryCommand : IOperatorCommand
{
    int GetCommand();
}

public interface IOpndCommand : IExpressionCommand
{
    List<int> GetCommands();

    void AppendCommand(int command);

    void ToggleSign();

    void RemoveFromEnd();

    bool IsNegative();

    bool IsSciFmt();

    bool IsDecimalPresent();

    string GetToken(char decimalSymbol);

    void SetCommands(List<int> commands);
}

public interface IParenthesisCommand : IExpressionCommand
{
    int GetCommand();
}

/// <summary>Provides localized strings to the calculation engine.</summary>
public interface IResourceProvider
{
    /// <summary>Returns the engine string for the given key (e.g. "0".."120", "SecDeg", "sDecimal", "sThousand", "sGrouping").</summary>
    string GetCEngineString(string id);
}

/// <summary>Callback interface to be implemented by the clients of the calculation engine.</summary>
public interface ICalcDisplay
{
    void SetPrimaryDisplay(string text, bool isError);

    void SetIsInError(bool isInError);

    void SetExpressionDisplay(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands);

    void SetParenthesisNumber(int count);

    void OnNoRightParenAdded();

    /// <summary>Not an error but still need to inform the UI layer.</summary>
    void MaxDigitsReached();

    void BinaryOperatorReceived();

    void OnHistoryItemAdded(int addedItemIndex);

    void SetMemorizedNumbers(List<string> memorizedNumbers);

    void MemoryItemChanged(int indexOfMemory);

    void InputChanged();
}

/// <summary>Callback interface to be implemented by the clients of the engine if they require equation history.</summary>
public interface IHistoryDisplay
{
    int AddToHistory(List<(string Text, int CommandIndex)> tokens, List<IExpressionCommand> commands, string result);
}
