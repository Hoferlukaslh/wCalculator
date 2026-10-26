using Calculator.Engine;
using Xunit;
using static Calculator.Engine.Tests.Calc.CalcKind;

namespace Calculator.Engine.Tests;

public class CalculatorTests
{
    [Fact]
    public void Addition()
    {
        Assert.Equal("5", new Calc(Standard).Type("2+3=").Primary);
    }

    [Fact]
    public void Standard_mode_has_no_operator_precedence()
    {
        Assert.Equal("9", new Calc(Standard).Type("1+2*3=").Primary);
    }

    [Fact]
    public void Scientific_mode_respects_operator_precedence()
    {
        Assert.Equal("7", new Calc(Scientific).Type("1+2*3=").Primary);
    }

    [Fact]
    public void Parentheses_in_scientific_mode()
    {
        Assert.Equal("9", new Calc(Scientific).Type("(1+2)*3=").Primary);
    }

    [Fact]
    public void Division_gives_decimals()
    {
        Assert.Equal("2.5", new Calc(Standard).Type("10/4=").Primary);
    }

    [Fact]
    public void One_third_times_three_is_exactly_one()
    {
        Assert.Equal("1", new Calc(Standard).Type("1/3*3=").Primary);
    }

    [Fact]
    public void Square_root_of_four()
    {
        Assert.Equal("2", new Calc(Standard).Type("4").Send(Command.CommandSQRT).Primary);
    }

    [Fact]
    public void Square_of_twelve()
    {
        Assert.Equal("144", new Calc(Standard).Type("12").Send(Command.CommandSQR).Primary);
    }

    [Fact]
    public void Reciprocal_of_eight()
    {
        Assert.Equal("0.125", new Calc(Standard).Type("8").Send(Command.CommandREC).Primary);
    }

    [Fact]
    public void Percent_of_a_number_in_addition()
    {
        // 200 + 5% = 210 (5% of 200 is added)
        var c = new Calc(Standard).Type("200+5").Send(Command.CommandPERCENT);
        Assert.Equal("10", c.Primary);
        Assert.Equal("210", c.Send(Command.CommandEQU).Primary);
    }

    [Fact]
    public void Negation()
    {
        Assert.Equal("-7", new Calc(Standard).Type("7").Send(Command.CommandSIGN).Primary);
    }

    [Fact]
    public void Digit_grouping_in_display()
    {
        Assert.Equal("1,234,567", new Calc(Standard).Type("1234567").Primary);
    }

    [Fact]
    public void Backspace_removes_last_digit()
    {
        Assert.Equal("12", new Calc(Standard).Type("123").Send(Command.CommandBACK).Primary);
    }

    [Fact]
    public void Divide_by_zero_is_an_error()
    {
        var c = new Calc(Standard).Type("1/0=");
        Assert.True(c.Display.IsError);
        Assert.False(string.IsNullOrEmpty(c.Primary));
    }

    [Fact]
    public void Clear_resets_the_display()
    {
        Assert.Equal("0", new Calc(Standard).Type("123+4").Send(Command.CommandCLEAR).Primary);
    }

    [Fact]
    public void Factorial()
    {
        Assert.Equal("120", new Calc(Scientific).Type("5").Send(Command.CommandFAC).Primary);
    }

    [Fact]
    public void Power()
    {
        Assert.Equal("1,024", new Calc(Scientific).Type("2").Send(Command.CommandPWR).Type("10=").Primary);
    }

    [Fact]
    public void Sine_of_30_degrees()
    {
        var c = new Calc(Scientific).Send(Command.CommandDEG).Type("30").Send(Command.CommandSIN);
        Assert.Equal("0.5", c.Primary);
    }

    [Fact]
    public void Cosine_of_pi_radians()
    {
        var c = new Calc(Scientific).Send(Command.CommandRAD).Send(Command.CommandPI).Send(Command.CommandCOS);
        Assert.Equal("-1", c.Primary);
    }

    [Fact]
    public void Arc_sine_of_one_half_in_degrees()
    {
        var c = new Calc(Scientific).Send(Command.CommandDEG).Type("0.5").Send(Command.CommandASIN);
        Assert.Equal("30", c.Primary);
    }

    [Fact]
    public void Natural_log_of_e_is_one()
    {
        var c = new Calc(Scientific).Send(Command.CommandEuler).Send(Command.CommandLN);
        Assert.Equal("1", c.Primary);
    }

    [Fact]
    public void Log10_of_1000()
    {
        Assert.Equal("3", new Calc(Scientific).Type("1000").Send(Command.CommandLOG).Primary);
    }

    [Fact]
    public void Memory_store_and_recall()
    {
        var d = new Calc(Standard);
        d.Type("42");
        d.Manager.MemorizeNumber();
        d.Send(Command.CommandCLEAR);
        Assert.Equal("0", d.Primary);
        Assert.Single(d.Display.Memory);
        d.Manager.MemorizedNumberLoad(0);
        Assert.Equal("42", d.Primary);
    }

    [Fact]
    public void Memory_add_and_subtract()
    {
        var d = new Calc(Standard);
        d.Type("10");
        d.Manager.MemorizeNumber();
        d.Type("5");
        d.Manager.MemorizedNumberAdd(0);
        Assert.Equal("15", d.Display.Memory[0]);
        d.Manager.MemorizedNumberSubtract(0);
        Assert.Equal("10", d.Display.Memory[0]);
    }

    [Fact]
    public void Equals_adds_an_item_to_the_history()
    {
        var c = new Calc(Standard).Type("2+2=");
        Assert.Single(c.Manager.GetHistoryItems());
        Assert.Equal("4", c.Manager.GetHistoryItems()[0].Result);
        Assert.Contains("2", c.Manager.GetHistoryItems()[0].Expression);
    }

    [Fact]
    public void Expression_line_shows_pending_operation()
    {
        var c = new Calc(Standard).Type("7+");
        Assert.Contains("7", c.Display.Expression);
        Assert.Contains("+", c.Display.Expression);
    }

    [Fact]
    public void Repeated_equals_repeats_last_operation()
    {
        Assert.Equal("14", new Calc(Standard).Type("5+3===").Primary);
    }

    [Fact]
    public void Very_large_numbers_use_exponent_notation()
    {
        var c = new Calc(Scientific).Type("9999999999").Send(Command.CommandPWR).Type("4=");
        Assert.Contains("e+", c.Primary);
    }
}
