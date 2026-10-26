using System;
using Calculator.Engine;
using Xunit;
using Calculator.Engine.DateCalculation;

namespace Calculator.Engine.Tests;

public class DateTests
{
    private readonly DateCalculationEngine _engine = new DateCalculationEngine();
    private const DateUnit All = DateUnit.Year | DateUnit.Month | DateUnit.Week | DateUnit.Day;

    [Fact]
    public void Difference_in_days()
    {
        DateDifference? d = _engine.TryGetDateDifference(new DateTime(2024, 1, 1), new DateTime(2024, 3, 1), DateUnit.Day);
        Assert.NotNull(d);
        Assert.Equal(60, d!.Value.Day);
    }

    [Fact]
    public void Difference_in_all_units()
    {
        DateDifference? d = _engine.TryGetDateDifference(new DateTime(2020, 1, 15), new DateTime(2022, 3, 22), All);
        Assert.NotNull(d);
        Assert.Equal(2, d!.Value.Year);
        Assert.Equal(2, d.Value.Month);
        Assert.Equal(1, d.Value.Week);
        Assert.Equal(0, d.Value.Day);
    }

    [Fact]
    public void Difference_is_symmetric()
    {
        DateDifference? a = _engine.TryGetDateDifference(new DateTime(2020, 1, 15), new DateTime(2022, 3, 22), DateUnit.Day);
        DateDifference? b = _engine.TryGetDateDifference(new DateTime(2022, 3, 22), new DateTime(2020, 1, 15), DateUnit.Day);
        Assert.Equal(a!.Value.Day, b!.Value.Day);
    }

    [Fact]
    public void Add_duration()
    {
        DateTime? r = _engine.AddDuration(new DateTime(2024, 1, 31), new DateDifference(0, 1, 0, 0));
        Assert.Equal(new DateTime(2024, 2, 29), r);
    }

    [Fact]
    public void Add_days_across_year()
    {
        DateTime? r = _engine.AddDuration(new DateTime(2023, 12, 25), new DateDifference(0, 0, 0, 10));
        Assert.Equal(new DateTime(2024, 1, 4), r);
    }

    [Fact]
    public void Subtract_duration()
    {
        // As in the original app, days are subtracted first, then months, then years:
        // 2024-03-31 -1d = 03-30, -1m = 2024-02-29, -1y = 2023-02-28 (clamped).
        DateTime? r = _engine.SubtractDuration(new DateTime(2024, 3, 31), new DateDifference(1, 1, 0, 1));
        Assert.Equal(new DateTime(2023, 2, 28), r);
    }

    [Fact]
    public void Out_of_range_returns_null()
    {
        Assert.Null(_engine.SubtractDuration(new DateTime(1601, 1, 2), new DateDifference(0, 0, 0, 5)));
    }
}
