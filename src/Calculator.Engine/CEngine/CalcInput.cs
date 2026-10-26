// Port of CalcManager CEngine CalcInput.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Text;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

internal sealed class CalcNumSec
{
    public StringBuilder Value { get; } = new StringBuilder();

    public bool IsNegative { get; set; }

    public bool IsEmpty => Value.Length == 0;

    public void Clear()
    {
        Value.Clear();
        IsNegative = false;
    }
}

internal sealed class CalcInput
{
    /// <summary>Space to hold enough digits for a quadword binary number (64) plus digit separator strings for that number (20).</summary>
    public const int MAX_STRLEN = 84;

    private const int C_NUM_MAX_DIGITS = MAX_STRLEN;
    private const int C_EXP_MAX_DIGITS = 4;

    private bool _hasExponent;
    private bool _hasDecimal;
    private int _decPtIndex;
    private char _decSymbol;
    private readonly CalcNumSec _base = new CalcNumSec();
    private readonly CalcNumSec _exponent = new CalcNumSec();

    public CalcInput()
        : this('.')
    {
    }

    public CalcInput(char decSymbol)
    {
        _decSymbol = decSymbol;
    }

    public void Clear()
    {
        _base.Clear();
        _exponent.Clear();
        _hasExponent = false;
        _hasDecimal = false;
        _decPtIndex = 0;
    }

    public bool TryToggleSign(bool isIntegerMode, string maxNumStr)
    {
        // Zero is always positive
        if (_base.IsEmpty)
        {
            _base.IsNegative = false;
            _exponent.IsNegative = false;
        }
        else if (_hasExponent)
        {
            _exponent.IsNegative = !_exponent.IsNegative;
        }
        else
        {
            // When in integer only mode, it isn't always allowed to toggle, as toggling can cause the num to be out of
            // bounds. For eg. in byte -128 is valid, but when it toggled it becomes 128, which is more than 127.
            if (isIntegerMode && _base.IsNegative)
            {
                // Decide if this additional digit will fit for the given bit width
                if (_base.Value.Length >= maxNumStr.Length && _base.Value[_base.Value.Length - 1] > maxNumStr[maxNumStr.Length - 1])
                {
                    // Last digit is more than the allowed positive number. Fail
                    return false;
                }
            }

            _base.IsNegative = !_base.IsNegative;
        }

        return true;
    }

    public bool TryAddDigit(uint value, uint radix, bool isIntegerMode, string maxNumStr, int wordBitWidth, int maxDigits)
    {
        // Convert from an integer into a character
        // This includes both normal digits and alpha 'digits' for radixes > 10
        char chDigit = (char)((value < 10) ? ('0' + value) : ('A' + value - 10));

        CalcNumSec pNumSec;
        int maxCount;
        if (_hasExponent)
        {
            pNumSec = _exponent;
            maxCount = C_EXP_MAX_DIGITS;
        }
        else
        {
            pNumSec = _base;
            maxCount = maxDigits;
            // Don't include the decimal point in the count. In that way you can enter the maximum allowed precision.
            // Precision doesn't include decimal point.
            if (HasDecimalPt())
            {
                maxCount++;
            }

            // First leading 0 is not counted in input restriction as the output can be of that form
            // See NumberToString algorithm. REVIEW: We don't have such input restriction mimicking based on output of NumberToString for exponent
            // NumberToString can give 10 digit exponent, but we still restrict the exponent here to be only 4 digits.
            if (!pNumSec.IsEmpty && pNumSec.Value[0] == '0')
            {
                maxCount++;
            }
        }

        // Ignore leading zeros
        if (pNumSec.IsEmpty && value == 0)
        {
            return true;
        }

        if (pNumSec.Value.Length < maxCount)
        {
            pNumSec.Value.Append(chDigit);
            return true;
        }

        // if we are in integer mode, within the base, and we're on the last digit then
        // there are special cases where we can actually add one more digit.
        if (isIntegerMode && pNumSec.Value.Length == maxCount && !_hasExponent)
        {
            bool allowExtraDigit = false;

            if (radix == 8)
            {
                switch (wordBitWidth % 3)
                {
                    case 1:
                        // in 16 or 64bit word size, if the first digit is a 1 we can enter 6 (16bit) or 22 (64bit) digits
                        allowExtraDigit = pNumSec.Value[0] == '1';
                        break;

                    case 2:
                        // in 8 or 32bit word size, if the first digit is a 3 or less we can enter 3 (8bit) or 11 (32bit) digits
                        allowExtraDigit = pNumSec.Value[0] <= '3';
                        break;
                }
            }
            else if (radix == 10)
            {
                // If value length is at least the max, we know we can't add another digit.
                if (pNumSec.Value.Length < maxNumStr.Length)
                {
                    // Compare value to substring of maxNumStr of value.Length length.
                    int cmpResult = string.CompareOrdinal(pNumSec.Value.ToString(), 0, maxNumStr, 0, pNumSec.Value.Length);
                    if (cmpResult < 0)
                    {
                        allowExtraDigit = true;
                    }
                    else if (cmpResult == 0)
                    {
                        char lastChar = maxNumStr[pNumSec.Value.Length];
                        if (chDigit <= lastChar)
                        {
                            allowExtraDigit = true;
                        }
                        else if (pNumSec.IsNegative && chDigit <= lastChar + 1)
                        {
                            // Negative value case, eg. max is "127", and current number is "-12". Then 8 is also valid, as the range
                            // is always from -(max+1)...max in signed mode
                            allowExtraDigit = true;
                        }
                    }
                }
            }

            if (allowExtraDigit)
            {
                pNumSec.Value.Append(chDigit);
                return true;
            }
        }

        return false;
    }

    public bool TryAddDecimalPt()
    {
        // Already have a decimal pt or we're in the exponent
        if (_hasDecimal || _hasExponent)
        {
            return false;
        }

        if (_base.IsEmpty)
        {
            _base.Value.Append('0'); // Add a leading zero
        }

        _decPtIndex = _base.Value.Length;
        _base.Value.Append(_decSymbol);
        _hasDecimal = true;

        return true;
    }

    public bool HasDecimalPt() => _hasDecimal;

    public bool TryBeginExponent()
    {
        // For compatibility, add a trailing dec point to base num if it doesn't have one
        TryAddDecimalPt();

        if (_hasExponent)
        {
            // Already entering exponent
            return false;
        }

        _hasExponent = true; // Entering exponent
        return true;
    }

    public void Backspace()
    {
        if (_hasExponent)
        {
            if (!_exponent.IsEmpty)
            {
                _exponent.Value.Length--;

                if (_exponent.IsEmpty)
                {
                    _exponent.Clear();
                }
            }
            else
            {
                _hasExponent = false;
            }
        }
        else
        {
            if (!_base.IsEmpty)
            {
                _base.Value.Length--;
                if (_base.Value.Length == 1 && _base.Value[0] == '0')
                {
                    _base.Value.Length--;
                }
            }

            if (_base.Value.Length <= _decPtIndex)
            {
                // Backed up over decimal point
                _hasDecimal = false;
                _decPtIndex = 0;
            }

            if (_base.IsEmpty)
            {
                _base.Clear();
            }
        }
    }

    public void SetDecimalSymbol(char decSymbol)
    {
        if (_decSymbol != decSymbol)
        {
            _decSymbol = decSymbol;

            if (_hasDecimal)
            {
                // Change to new decimal pt
                _base.Value[_decPtIndex] = _decSymbol;
            }
        }
    }

    public bool IsEmpty() => _base.IsEmpty && !_hasExponent && _exponent.IsEmpty && !_hasDecimal;

    public string ToString(uint radix)
    {
        // In theory both the base and exponent could be C_NUM_MAX_DIGITS long.
        if (_base.Value.Length > MAX_STRLEN || (_hasExponent && _exponent.Value.Length > MAX_STRLEN))
        {
            return string.Empty;
        }

        var result = new StringBuilder();

        if (_base.IsNegative)
        {
            result.Append('-');
        }

        if (_base.IsEmpty)
        {
            result.Append('0');
        }
        else
        {
            result.Append(_base.Value);
        }

        if (_hasExponent)
        {
            // Add a decimal point if it is not already there
            if (!_hasDecimal)
            {
                result.Append(_decSymbol);
            }

            result.Append(radix == 10 ? 'e' : '^');
            result.Append(_exponent.IsNegative ? '-' : '+');

            if (_exponent.IsEmpty)
            {
                result.Append('0');
            }
            else
            {
                result.Append(_exponent.Value);
            }
        }

        // Base and Exp can each be up to C_NUM_MAX_DIGITS in length, plus 4 characters for sign, dec, exp, and expSign.
        if (result.Length > C_NUM_MAX_DIGITS * 2 + 4)
        {
            return string.Empty;
        }

        return result.ToString();
    }

    public Rational ToRational(uint radix, int precision)
    {
        Rat? rat = RatPak.StringToRat(_base.IsNegative, _base.Value.ToString(), _exponent.IsNegative, _exponent.Value.ToString(), radix, precision);
        if (rat == null)
        {
            return new Rational(0);
        }

        return new Rational(rat);
    }
}
