// Port of CalcManager CEngine scidisp.cpp (number display and digit grouping).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

internal sealed partial class CCalcEngine
{
    private const int MAX_EXPONENT = 4;
    private const uint MAX_GROUPING_SIZE = 16;

    // State of calc last time DisplayNum was called.
    private sealed class LastDisplayState
    {
        public Rational Value = new Rational();
        public int Precision = -1;
        public uint Radix;
        public int NFE = -1;
        public NumWidth NumWidth = (NumWidth)(-1);
        public bool IntMath;
        public bool Record;
        public bool UseSep;
    }

    private readonly LastDisplayState _previous = new LastDisplayState();
    private Regex? _numberValidationRegex;
    private char _numberValidationRegexSeparator;

    // Truncates if too big, makes it a non negative - the number in rat. Doesn't do anything if not in INT mode
    private Rational TruncateNumForIntMath(Rational rat)
    {
        if (!_fIntegerMode)
        {
            return rat;
        }

        // Truncate to an integer. Do not round here.
        Rational result = RationalMath.Integer(rat);

        // Can be converting a dec negative number to Hex/Oct/Bin rep. Use 2's complement form
        // Check the range.
        if (result < 0)
        {
            // if negative make positive by doing a twos complement
            result = -result - 1;
            result ^= GetChopNumber();
        }

        result &= GetChopNumber();

        return result;
    }

    // Convert _currentVal to a string in the current radix.
    // Updates the following variables: _currentVal, _numberString
    private void DisplayNum()
    {
        //
        // Only change the display if
        //  we are in record mode                               -OR-
        //  this is the first time DisplayNum has been called,  -OR-
        //  something important has changed since the last time DisplayNum was
        //  called.
        //
        if (_bRecord || _previous.Value != _currentVal || _previous.Precision != _precision || _previous.Radix != _radix || _previous.NFE != (int)_nFE
            || !_previous.UseSep || _previous.NumWidth != _numwidth || _previous.IntMath != _fIntegerMode || _previous.Record != _bRecord)
        {
            _previous.Precision = _precision;
            _previous.Radix = _radix;
            _previous.NFE = (int)_nFE;
            _previous.NumWidth = _numwidth;

            _previous.IntMath = _fIntegerMode;
            _previous.Record = _bRecord;
            _previous.UseSep = true;

            if (_bRecord)
            {
                // Display the string and return.
                _numberString = _input.ToString(_radix);
            }
            else
            {
                // If we're in Programmer mode, perform integer truncation so e.g. 5 / 2 * 2 results in 4, not 5.
                if (_fIntegerMode)
                {
                    _currentVal = TruncateNumForIntMath(_currentVal);
                }

                _numberString = GetStringForDisplay(_currentVal, _radix);
            }

            // Displayed number can go through transformation. So copy it after transformation
            _previous.Value = _currentVal;

            if ((_radix == 10) && IsNumberInvalid(_numberString, MAX_EXPONENT, _precision, _radix) != 0)
            {
                DisplayError(CalcErr.Overflow);
            }
            else
            {
                // Display the string and return.
                SetPrimaryDisplay(GroupDigitsPerRadix(_numberString, _radix));
            }
        }
    }

    private int IsNumberInvalid(string numberString, int iMaxExp, int iMaxMantissa, uint radix)
    {
        int iError = 0;

        if (radix == 10)
        {
            // start with an optional + or -
            // followed by zero or more digits
            // followed by an optional decimal point
            // followed by zero or more digits
            // followed by an optional exponent
            // in case there's an exponent:
            //      its optionally followed by a + or -
            //      which is followed by zero or more digits
            if (_numberValidationRegex == null || _numberValidationRegexSeparator != _decimalSeparator)
            {
                _numberValidationRegex = new Regex(
                    "^[+-]?([0-9]*)[" + EscapeForCharacterClass(_decimalSeparator) + "]?([0-9]*)(?:e[+-]?([0-9]*))?$",
                    RegexOptions.CultureInvariant);
                _numberValidationRegexSeparator = _decimalSeparator;
            }

            Match matches = _numberValidationRegex.Match(numberString);
            if (matches.Success)
            {
                // Check that exponent isn't too long
                if (matches.Groups[3].Length > iMaxExp)
                {
                    iError = EngineStrings.IDS_ERR_INPUT_OVERFLOW;
                }
                else
                {
                    string exp = matches.Groups[1].Value;
                    int idx = 0;
                    while (idx < exp.Length && exp[idx] == '0')
                    {
                        idx++;
                    }

                    int iMantissa = (exp.Length - idx) + matches.Groups[2].Length;
                    if (iMantissa > iMaxMantissa)
                    {
                        iError = EngineStrings.IDS_ERR_INPUT_OVERFLOW;
                    }
                }
            }
            else
            {
                iError = EngineStrings.IDS_ERR_UNK_CH;
            }
        }
        else
        {
            foreach (char c in numberString)
            {
                if (radix == 16)
                {
                    if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                    {
                        iError = EngineStrings.IDS_ERR_UNK_CH;
                    }
                }
                else if (c < '0' || c >= '0' + radix)
                {
                    iError = EngineStrings.IDS_ERR_UNK_CH;
                }
            }
        }

        return iError;
    }

    private static string EscapeForCharacterClass(char c)
    {
        return (c == '\\' || c == ']' || c == '^' || c == '-' || c == '[') ? "\\" + c : c.ToString();
    }

    // Mimics C's wcstoul(string, &end, 10): returns the parsed value and sets 'next' to the first unparsed position.
    private static uint WcsToUl(string s, int start, out int next)
    {
        int i = start;
        while (i < s.Length && char.IsWhiteSpace(s[i]))
        {
            i++;
        }

        bool negative = false;
        if (i < s.Length && (s[i] == '+' || s[i] == '-'))
        {
            negative = s[i] == '-';
            i++;
        }

        int digitsStart = i;
        ulong value = 0;
        while (i < s.Length && s[i] >= '0' && s[i] <= '9')
        {
            value = value * 10 + (ulong)(s[i] - '0');
            if (value > uint.MaxValue)
            {
                value = uint.MaxValue;
            }

            i++;
        }

        if (i == digitsStart)
        {
            next = start; // no conversion performed
            return 0;
        }

        next = i;
        uint result = (uint)value;
        return negative ? unchecked((uint)(-(long)result)) : result;
    }

    /// <summary>
    /// Takes the digit grouping string found in the regional settings and represents it as a vector.
    /// 0;0 - no grouping, 3;0 - group every 3 digits, 3 - group 1st 3 then no grouping, 3;2;0 - group 1st 3 and then every 2 digits...
    /// </summary>
    private static List<uint> DigitGroupingStringToGroupingVector(string groupingString)
    {
        var grouping = new List<uint>();

        for (int itr = 0; itr < groupingString.Length; ++itr)
        {
            // Try to parse a grouping number from the string
            uint currentGroup = WcsToUl(groupingString, itr, out int next);

            // If we successfully parsed a group, add it to the grouping.
            if (currentGroup < MAX_GROUPING_SIZE)
            {
                grouping.Add(currentGroup);
            }

            // If we found a grouping and aren't at the end of the string yet,
            // jump to the next position in the string (the ';').
            // The loop will then increment us to the next character, which should be a number.
            if (next < groupingString.Length)
            {
                itr = next;
            }
        }

        return grouping;
    }

    public string GroupDigitsPerRadix(string numberString, uint radix)
    {
        if (numberString.Length == 0)
        {
            return string.Empty;
        }

        switch (radix)
        {
            case 10:
                return GroupDigits(_groupSeparator.ToString(), _decGrouping, numberString, numberString[0] == '-');
            case 8:
                return GroupDigits(" ", new List<uint> { 3, 0 }, numberString);
            case 2:
            case 16:
                return GroupDigits(" ", new List<uint> { 4, 0 }, numberString);
            default:
                return numberString;
        }
    }

    /// <summary>
    /// Takes a grouping vector and the display string and adds the separator according to the pattern indicated by the grouping.
    /// </summary>
    private string GroupDigits(string delimiter, List<uint> grouping, string displayString, bool isNumNegative = false)
    {
        // if there's nothing to do, bail
        if (delimiter.Length == 0 || grouping.Count == 0)
        {
            return displayString;
        }

        // Find the position of exponential 'e' in the string
        int exp = displayString.IndexOf('e');
        bool hasExponent = exp >= 0;

        // Find the position of decimal point in the string
        int dec = displayString.IndexOf(_decimalSeparator);
        bool hasDecimal = dec >= 0;

        // 'pos' is the index of the first character (going right to left) of the portion of the number subject to grouping (i.e. left of the decimal)
        int pos;
        if (hasDecimal)
        {
            pos = dec - 1;
        }
        else if (hasExponent)
        {
            pos = exp - 1;
        }
        else
        {
            pos = displayString.Length - 1;
        }

        var result = new StringBuilder();
        uint groupingSize = 0;

        int groupIdx = 0;
        uint currGrouping = grouping[groupIdx];

        // Mark the 'end' of the string as index 0 or 1 if there is a negative sign.
        // We exclude the sign here because we don't want to end up with e.g. "-,123,456"
        // Then, iterate from back to front, adding group delimiters as needed.
        int lowest = isNumNegative ? 1 : 0;
        while (pos >= lowest)
        {
            result.Append(displayString[pos--]);
            groupingSize++;

            // If a group is complete, add a separator
            // Do not add a separator if:
            // - grouping size is 0
            // - we are at the end of the digit string
            if (currGrouping != 0 && (groupingSize % currGrouping) == 0 && pos >= lowest)
            {
                result.Append(delimiter);
                groupingSize = 0; // reset for a new group

                // Shift the grouping to next values if they exist
                if (groupIdx != grouping.Count)
                {
                    ++groupIdx;

                    // Loop through grouping vector until we find a non-zero value.
                    // "0" values may appear in a form of either e.g. "3;0" or "3;0;0".
                    // A 0 in the last position means repeat the previous grouping.
                    // A 0 in another position is a group. So, "3;0;0" means "group 3, then group 0 repeatedly"
                    // This could be expressed as just "3" but GetLocaleInfo is returning 3;0;0 in some cases instead.
                    for (currGrouping = 0; groupIdx != grouping.Count; ++groupIdx)
                    {
                        // If it's a non-zero value, that's our new group
                        if (grouping[groupIdx] != 0)
                        {
                            currGrouping = grouping[groupIdx];
                            break;
                        }

                        // Otherwise, save the previous grouping in case we need to repeat it
                        currGrouping = grouping[groupIdx - 1];
                    }
                }
            }
        }

        // now copy the negative sign if it is there
        if (isNumNegative)
        {
            result.Append(displayString[0]);
        }

        // reverse the accumulated characters
        char[] chars = result.ToString().ToCharArray();
        Array.Reverse(chars);
        result.Clear();
        result.Append(chars);

        // Add the right (fractional or exponential) part of the number to the final string.
        if (hasDecimal)
        {
            result.Append(displayString, dec, displayString.Length - dec);
        }
        else if (hasExponent)
        {
            result.Append(displayString, exp, displayString.Length - exp);
        }

        return result.ToString();
    }
}
