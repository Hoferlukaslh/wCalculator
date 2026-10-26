// Port of CalcManager CEngine sciset.cpp (radix / word size settings).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

internal sealed partial class CCalcEngine
{
    // To be called when either the radix or num width changes. You can use -1 in either of these values to mean
    // dont change that.
    private void SetRadixTypeAndNumWidth(RadixType radixtype, NumWidth numwidth)
    {
        // When in integer mode, the number is represented in 2's complement form. When a bit width is changing, we can
        // change the number representation back to sign, abs num form in ratpak. Soon when display sees this, it will
        // convert to 2's complement form, but this time all high bits will be propagated. Eg. -127, in byte mode is
        // represented as 1000,0001. This puts it back as sign=-1, 01111111 . But DisplayNum will see this and convert it
        // back to 1111,1111,1000,0001 when in Word mode.
        if (_fIntegerMode)
        {
            ulong w64Bits = _currentVal.ToUInt64_t();
            bool fMsb = ((w64Bits >> (_dwWordBitWidth - 1)) & 1) != 0; // make sure you use the old width

            if (fMsb)
            {
                // If high bit is set, then get the decimal number in -ve 2'scompl form.
                Rational tempResult = _currentVal ^ GetChopNumber();

                _currentVal = -(tempResult + 1);
            }
        }

        if (radixtype >= RadixType.Hex && radixtype <= RadixType.Binary)
        {
            _radix = NRadixFromRadixType(radixtype);

            // radixtype is not even saved
        }

        if (numwidth >= NumWidth.QWord && numwidth <= NumWidth.Byte)
        {
            _numwidth = numwidth;
            _dwWordBitWidth = DwWordBitWidthFromNumWidth(numwidth);
        }

        // inform ratpak that a change in base or precision has occurred
        BaseOrPrecisionChanged();

        // display the correct number for the new state (ie convert displayed
        //  number to correct base)
        DisplayNum();
    }

    private static int DwWordBitWidthFromNumWidth(NumWidth numwidth)
    {
        switch (numwidth)
        {
            case NumWidth.DWord:
                return 32;
            case NumWidth.Word:
                return 16;
            case NumWidth.Byte:
                return 8;
            default:
                return 64;
        }
    }

    private static uint NRadixFromRadixType(RadixType radixtype)
    {
        switch (radixtype)
        {
            case RadixType.Hex:
                return 16;
            case RadixType.Octal:
                return 8;
            case RadixType.Binary:
                return 2;
            default:
                return 10;
        }
    }

    // Toggles a given bit into the number representation. returns true if it changed it actually.
    private bool TryToggleBit(ref Rational rat, uint wbitno)
    {
        uint wmax = (uint)DwWordBitWidthFromNumWidth(_numwidth);
        if (wbitno >= wmax)
        {
            return false; // ignore error cant happen
        }

        Rational result = RationalMath.Integer(rat);

        // Remove any variance in how 0 could be represented in rat e.g. -0, 0/n, etc.
        result = result != 0 ? result : new Rational(0);

        // XOR the result with 2^wbitno power
        rat = result ^ RationalMath.Pow(2, (int)wbitno);

        return true;
    }

    // Returns the nearest power of two
    private static int QuickLog2(int iNum)
    {
        int iRes = 0;

        // while first digit is a zero
        while ((iNum & 1) == 0)
        {
            iRes++;
            iNum >>= 1;
        }

        // if our number isn't a perfect square
        iNum >>= 1;
        if (iNum != 0)
        {
            // find the largest digit
            for (iNum >>= 1; iNum != 0; iNum >>= 1)
            {
                ++iRes;
            }

            // and then add two
            iRes += 2;
        }

        return iRes;
    }

    // UpdateMaxIntDigits
    //
    // determine the maximum number of digits needed for the current precision,
    // word size, and base.  This number is conservative towards the small side
    // such that there may be some extra bits left over. For example, base 8 requires 3 bits per digit.
    // A word size of 32 bits allows for 10 digits with a remainder of two bits.  Bases
    // that require variable number of bits (non-power-of-two bases) are approximated
    // by the next highest power-of-two base (again, to be conservative and guarantee
    // there will be no over flow verse the current word size for numbers entered).
    // Base 10 is a special case and always uses the base 10 precision (_precision).
    public void UpdateMaxIntDigits()
    {
        if (_radix == 10)
        {
            // if in integer mode you still have to honor the max digits you can enter based on bit width
            if (_fIntegerMode)
            {
                // This is the max digits you can enter a decimal in fixed width mode aka integer mode -1. The last digit
                // has to be checked separately
                _cIntDigitsSav = GetMaxDecimalValueString().Length - 1;
            }
            else
            {
                _cIntDigitsSav = _precision;
            }
        }
        else
        {
            _cIntDigitsSav = _dwWordBitWidth / QuickLog2((int)_radix);
        }
    }

    private static void ChangeBaseConstants(uint radix, int maxIntDigits, int precision)
    {
        if (10 == radix)
        {
            // Base 10 precision for internal computing still needs to be 32, to
            // take care of decimals precisely. For eg. to get the HI word of a qword, we do a rsh, which depends on getting
            // 18446744073709551615 / 4294967296 = 4294967295.9999917... This is important it works this and doesn't reduce
            // the precision to number of digits allowed to enter. In other words, precision and # of allowed digits to be
            // entered are different.
            RatPak.ChangeConstants(radix, precision);
        }
        else
        {
            RatPak.ChangeConstants(radix, maxIntDigits + 1);
        }
    }

    private void BaseOrPrecisionChanged()
    {
        UpdateMaxIntDigits();
        ChangeBaseConstants(_radix, _cIntDigitsSav, _precision);
    }
}
