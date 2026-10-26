// Port of the Windows Calculator date calculation engine (Gregorian calendar only).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Calculator.Engine.DateCalculation;

[Flags]
public enum DateUnit
{
    Year = 0x01,
    Month = 0x02,
    Week = 0x04,
    Day = 0x08,
}

public readonly struct DateDifference : IEquatable<DateDifference>
{
    public DateDifference(int year, int month, int week, int day)
    {
        Year = year;
        Month = month;
        Week = week;
        Day = day;
    }

    public int Year { get; }

    public int Month { get; }

    public int Week { get; }

    public int Day { get; }

    public bool Equals(DateDifference other) => Year == other.Year && Month == other.Month && Week == other.Week && Day == other.Day;

    public override bool Equals(object? obj) => obj is DateDifference other && Equals(other);

    public override int GetHashCode() => Year ^ (Month << 8) ^ (Week << 16) ^ (Day << 24);

    public static bool operator ==(DateDifference left, DateDifference right) => left.Equals(right);

    public static bool operator !=(DateDifference left, DateDifference right) => !left.Equals(right);
}

/// <summary>Computes differences between dates and adds / subtracts durations to dates.</summary>
public sealed class DateCalculationEngine
{
    private const int UnitsOfDate = 4;
    private const int UnitsGreaterThanDays = 3;
    private const int DaysInWeek = 7;

    // The original application stores dates as Windows FILETIME, so anything earlier than 1601 cannot be represented.
    private static readonly DateTime s_minSupportedDate = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    public static readonly DateDifference DateDifferenceUnknown = new DateDifference(int.MinValue, int.MinValue, int.MinValue, int.MinValue);

    /// <summary>Adds a duration to the date; returns null if the result is out of the supported range.</summary>
    public DateTime? AddDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            DateTime d = startDate.Date;
            if (duration.Year != 0)
            {
                d = d.AddYears(duration.Year);
            }

            if (duration.Month != 0)
            {
                d = d.AddMonths(duration.Month);
            }

            if (duration.Day != 0)
            {
                d = d.AddDays(duration.Day);
            }

            return d;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Subtracts a duration from the date; returns null if the result is out of the supported range.</summary>
    public DateTime? SubtractDuration(DateTime startDate, DateDifference duration)
    {
        try
        {
            DateTime d = startDate.Date;

            // For Subtract the algorithm is different than Add: smaller units first, then larger.
            if (duration.Day != 0)
            {
                d = d.AddDays(-duration.Day);
            }

            if (duration.Month != 0)
            {
                d = d.AddMonths(-duration.Month);
            }

            if (duration.Year != 0)
            {
                d = d.AddYears(-duration.Year);
            }

            return d >= s_minSupportedDate ? d : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>Gets the difference between two dates expressed in the requested units; null if it can't be computed.</summary>
    public DateDifference? TryGetDateDifference(DateTime date1, DateTime date2, DateUnit outputFormat)
    {
        DateTime startDate, endDate;

        if (date1 < date2)
        {
            startDate = date1.Date;
            endDate = date2.Date;
        }
        else
        {
            startDate = date2.Date;
            endDate = date1.Date;
        }

        DateTime pivotDate = startDate;
        uint daysDiff = (uint)GetDifferenceInDays(startDate, endDate);
        var differenceInDates = new uint[UnitsOfDate];

        // If output has units other than days (bits 0-2: Year, Month, Week)
        if (((int)outputFormat & 7) != 0)
        {
            uint daysInMonth = (uint)DateTime.DaysInMonth(startDate.Year, startDate.Month);
            uint approximateDaysInYear = DateTime.IsLeapYear(endDate.Year) ? 366u : 365u;
            uint[] daysIn = { approximateDaysInYear, daysInMonth, DaysInWeek, 1 };

            for (int unitIndex = 0; unitIndex < UnitsGreaterThanDays; unitIndex++)
            {
                DateTime tempPivotDate = pivotDate;
                var dateUnit = (DateUnit)(1 << unitIndex);

                if (((int)outputFormat & (int)dateUnit) != 0)
                {
                    bool isEndDateHit = false;
                    differenceInDates[unitIndex] = daysDiff / daysIn[unitIndex];

                    if (differenceInDates[unitIndex] != 0)
                    {
                        while (differenceInDates[unitIndex] != 0)
                        {
                            try
                            {
                                pivotDate = AdjustCalendarDate(tempPivotDate, dateUnit, (int)differenceInDates[unitIndex]);
                                break;
                            }
                            catch (ArgumentException)
                            {
                                // The day-based estimate can overshoot the calendar's upper bound.
                                differenceInDates[unitIndex] -= 1;
                            }
                        }
                    }

                    int tempDaysDiff;
                    do
                    {
                        tempDaysDiff = GetDifferenceInDays(pivotDate, endDate);

                        if (tempDaysDiff < 0)
                        {
                            if (differenceInDates[unitIndex] == 0)
                            {
                                return null;
                            }

                            differenceInDates[unitIndex] -= 1;
                            pivotDate = tempPivotDate;
                            pivotDate = AdjustCalendarDate(pivotDate, dateUnit, (int)differenceInDates[unitIndex]);
                            isEndDateHit = true;
                        }
                        else if (tempDaysDiff > 0)
                        {
                            if (isEndDateHit)
                            {
                                break;
                            }

                            try
                            {
                                pivotDate = AdjustCalendarDate(tempPivotDate, dateUnit, (int)(differenceInDates[unitIndex] + 1));
                                differenceInDates[unitIndex] += 1;
                            }
                            catch (ArgumentException)
                            {
                                // The current pivot is valid; finish with smaller units.
                                break;
                            }
                        }
                    }
                    while (tempDaysDiff != 0);

                    int signedDaysDiff = GetDifferenceInDays(pivotDate, endDate);
                    if (signedDaysDiff < 0)
                    {
                        return null;
                    }

                    daysDiff = (uint)signedDaysDiff;
                }
            }
        }

        differenceInDates[3] = daysDiff;

        return new DateDifference((int)differenceInDates[0], (int)differenceInDates[1], (int)differenceInDates[2], (int)differenceInDates[3]);
    }

    private static int GetDifferenceInDays(DateTime date1, DateTime date2) => (int)((date2.Date - date1.Date).Ticks / TimeSpan.TicksPerDay);

    private static DateTime AdjustCalendarDate(DateTime date, DateUnit dateUnit, int difference)
    {
        switch (dateUnit)
        {
            case DateUnit.Year:
                return date.AddYears(difference);
            case DateUnit.Month:
                return date.AddMonths(difference);
            case DateUnit.Week:
                return date.AddDays(7L * difference);
            default:
                return date;
        }
    }
}
