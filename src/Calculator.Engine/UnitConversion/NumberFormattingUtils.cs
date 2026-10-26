// Port of CalcManager NumberFormattingUtils.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Globalization;

namespace Calculator.Engine.UnitConversion;

internal static class NumberFormattingUtils
{
    /// <summary>Trims out any trailing zeros or decimals in the given input string.</summary>
    public static string TrimTrailingZeros(string number)
    {
        if (number.IndexOf('.') < 0)
        {
            return number;
        }

        int i = number.Length - 1;
        while (i >= 0 && number[i] == '0')
        {
            i--;
        }

        if (i >= 0)
        {
            number = number.Substring(0, i + 1);
        }

        if (number.Length > 0 && number[number.Length - 1] == '.')
        {
            number = number.Substring(0, number.Length - 1);
        }

        return number;
    }

    /// <summary>Get number of digits (whole number part + decimal part).</summary>
    public static uint GetNumberDigits(string value)
    {
        value = TrimTrailingZeros(value);
        uint numberSignificantDigits = (uint)value.Length;
        if (value.IndexOf('.') >= 0)
        {
            --numberSignificantDigits;
        }

        if (value.IndexOf('-') >= 0)
        {
            --numberSignificantDigits;
        }

        return numberSignificantDigits;
    }

    /// <summary>Get number of digits (whole number part only).</summary>
    public static uint GetNumberDigitsWholeNumberPart(double value)
    {
        return value == 0 ? 1u : (uint)(1 + Math.Max(0.0, Math.Log10(Math.Abs(value))));
    }

    /// <summary>Rounds the given double to the given number of digits after the decimal point (fixed notation).</summary>
    public static string RoundSignificantDigits(double value, uint numberSignificantDigits)
    {
        return value.ToString("F" + numberSignificantDigits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>Convert a number to scientific notation, in the same format as the C++ stream (d.dddddde+XX).</summary>
    public static string ToScientificNumber(double number)
    {
        if (double.IsNaN(number) || double.IsInfinity(number))
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }

        string s = number.ToString("E6", CultureInfo.InvariantCulture); // e.g. 1.234560E+004
        int e = s.IndexOf('E');
        string mantissa = s.Substring(0, e);
        char sign = s[e + 1];
        string digits = s.Substring(e + 2).TrimStart('0');
        if (digits.Length < 2)
        {
            digits = digits.PadLeft(2, '0');
        }

        return mantissa + "e" + sign + digits;
    }

    /// <summary>Parses a double like the C++ stod does for the strings produced by the converter.</summary>
    public static double Stod(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
    }
}
