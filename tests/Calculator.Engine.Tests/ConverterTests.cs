using System.Collections.Generic;
using System.Linq;
using Calculator.Engine;
using Xunit;
using Calculator.Engine.UnitConversion;

namespace Calculator.Engine.Tests;

public class ConverterTests
{
    private sealed class Callback : IUnitConverterVMCallback
    {
        public string From { get; private set; } = string.Empty;

        public string To { get; private set; } = string.Empty;

        public void DisplayCallback(string from, string to)
        {
            From = from;
            To = to;
        }

        public void SuggestedValueCallback(List<(string Value, Unit Unit)> suggestedValues)
        {
        }

        public void MaxDigitsReached()
        {
        }
    }

    private static (UnitConverter Converter, Callback Callback) Create(ConverterCategory category, string from, string to)
    {
        var loader = new UnitConverterDataLoader(key => key, "FR");
        var converter = new UnitConverter(loader);
        converter.Initialize();
        var callback = new Callback();
        converter.SetViewModelCallback(callback);

        Category cat = converter.GetCategories().First(c => c.Id == (int)category);
        (List<Unit> units, Unit _, Unit _) = converter.SetCurrentCategory(cat);
        Unit fromUnit = units.First(u => u.Name == from);
        Unit toUnit = units.First(u => u.Name == to);
        converter.SetCurrentUnitTypes(fromUnit, toUnit);
        return (converter, callback);
    }

    private static void TypeNumber(UnitConverter converter, string digits)
    {
        foreach (char ch in digits)
        {
            converter.SendCommand(ch == '.' ? UnitConversionCommand.Decimal : (UnitConversionCommand)(ch - '0'));
        }
    }

    [Fact]
    public void Twelve_categories_without_currency_and_graphing()
    {
        var converter = new UnitConverter(new UnitConverterDataLoader(key => key, "FR"));
        converter.Initialize();
        Assert.Equal(12, converter.GetCategories().Count);
    }

    [Fact]
    public void Celsius_to_fahrenheit()
    {
        var (c, cb) = Create(ConverterCategory.Temperature, "UnitName_DegreesCelsius", "UnitName_DegreesFahrenheit");
        TypeNumber(c, "100");
        Assert.Equal("100", cb.From);
        Assert.Equal("212", cb.To);
    }

    [Fact]
    public void Negative_celsius_to_fahrenheit()
    {
        var (c, cb) = Create(ConverterCategory.Temperature, "UnitName_DegreesCelsius", "UnitName_DegreesFahrenheit");
        TypeNumber(c, "40");
        c.SendCommand(UnitConversionCommand.Negate);
        Assert.Equal("-40", cb.To);
    }

    [Fact]
    public void Kilometers_to_meters()
    {
        var (c, cb) = Create(ConverterCategory.Length, "UnitName_Kilometer", "UnitName_Meter");
        TypeNumber(c, "2.5");
        Assert.Equal("2500", cb.To);
    }

    [Fact]
    public void Miles_to_kilometers()
    {
        var (c, cb) = Create(ConverterCategory.Length, "UnitName_Mile", "UnitName_Kilometer");
        TypeNumber(c, "1");
        Assert.StartsWith("1.609", cb.To);
    }

    [Fact]
    public void Backspace_and_clear()
    {
        var (c, cb) = Create(ConverterCategory.Length, "UnitName_Kilometer", "UnitName_Meter");
        TypeNumber(c, "12");
        c.SendCommand(UnitConversionCommand.Backspace);
        Assert.Equal("1", cb.From);
        c.SendCommand(UnitConversionCommand.Clear);
        Assert.Equal("0", cb.From);
        Assert.Equal("0", cb.To);
    }
}
