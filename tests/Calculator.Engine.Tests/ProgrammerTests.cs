using Calculator.Engine;
using Xunit;
using static Calculator.Engine.Tests.Calc.CalcKind;

namespace Calculator.Engine.Tests;

public class ProgrammerTests
{
    [Fact]
    public void Decimal_to_hex()
    {
        var c = new Calc(Programmer).Type("255");
        Assert.Equal("FF", c.Manager.GetResultForRadix(16, 64, false));
        Assert.Equal("11111111", c.Manager.GetResultForRadix(2, 64, false));
        Assert.Equal("377", c.Manager.GetResultForRadix(8, 64, false));
    }

    [Fact]
    public void Bitwise_and()
    {
        Assert.Equal("8", new Calc(Programmer).Type("12").Send(Command.CommandAnd).Type("10=").Primary);
    }

    [Fact]
    public void Bitwise_or_and_xor()
    {
        Assert.Equal("14", new Calc(Programmer).Type("12").Send(Command.CommandOR).Type("10=").Primary);
        Assert.Equal("6", new Calc(Programmer).Type("12").Send(Command.CommandXor).Type("10=").Primary);
    }

    [Fact]
    public void Left_shift()
    {
        Assert.Equal("40", new Calc(Programmer).Type("5").Send(Command.CommandLSHF).Type("3=").Primary);
    }

    [Fact]
    public void Integer_division_truncates()
    {
        Assert.Equal("3", new Calc(Programmer).Type("10/3=").Primary);
    }

    [Fact]
    public void Byte_word_size_wraps_around()
    {
        var c = new Calc(Programmer).Send(Command.CommandByte).Type("127+1=");
        Assert.Equal("-128", c.Primary);
    }

    [Fact]
    public void Hex_entry()
    {
        var c = new Calc(Programmer);
        c.Manager.SetRadix(RadixType.Hex);
        c.Type("FF");
        Assert.Equal("FF", c.Primary);
        Assert.Equal("255", c.Manager.GetResultForRadix(10, 64, false));
    }

    [Fact]
    public void Not_of_zero_is_minus_one()
    {
        Assert.Equal("-1", new Calc(Programmer).Type("0").Send(Command.CommandNot).Primary);
    }
}
