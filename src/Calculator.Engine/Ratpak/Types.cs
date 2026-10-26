// Port of the Microsoft Windows Calculator "ratpak" arbitrary precision math package to C#.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Calculator.Engine.Ratpak;

/// <summary>Error raised by the calculator engine. Mirrors the numeric codes thrown by the C++ ratpak (CalcErr.h).</summary>
public sealed class CalcException : Exception
{
    public uint ErrorCode { get; }

    public CalcException(uint code) : base($"Calculator error 0x{code:X8}")
    {
        ErrorCode = code;
    }
}

/// <summary>Error codes (CalcErr.h).</summary>
public static class CalcErr
{
    public const uint DivideByZero = 0x80000000;
    public const uint Domain = 0x80000001;
    public const uint Indefinite = 0x80000002;
    public const uint PosInfinity = 0x80000003;
    public const uint NegInfinity = 0x80000004;
    public const uint InvalidRange = 0x80000006;
    public const uint OutOfMemory = 0x80000007;
    public const uint Overflow = 0x80000008;
    public const uint NoResult = 0x80000009;
}

public enum NumberFormat
{
    /// <summary>Returns floating point, or exponential if number is too big.</summary>
    Float,
    /// <summary>Always returns scientific notation.</summary>
    Scientific,
    /// <summary>Always returns engineering notation such that exponent is a multiple of 3.</summary>
    Engineering,
}

public enum AngleType
{
    Degrees,
    Radians,
    Gradians,
}

/// <summary>
/// A generic sized, generic radix number (NUMBER in ratpak.h).
/// Digits are stored least significant first. The value is sign * mant * radix^exp.
/// </summary>
public sealed class Number
{
    public int Sign;
    public int Cdigit;
    public int Exp;
    public uint[] Mant;

    /// <summary>Allocates a zeroed number able to hold <paramref name="size"/> digits (plus one spare, like _createnum).</summary>
    public Number(int size)
    {
        if (size < 0 || size > 0x1FFFFFF0)
        {
            throw new CalcException(CalcErr.InvalidRange);
        }

        Mant = new uint[size + 1];
    }
}

/// <summary>A rational number pp / pq where both are integral Numbers (RAT in ratpak.h).</summary>
public sealed class Rat
{
    public Number Pp = null!;
    public Number Pq = null!;
}
