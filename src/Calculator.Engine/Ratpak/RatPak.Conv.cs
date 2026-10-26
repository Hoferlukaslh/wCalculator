// Port of ratpak conv.cpp (conversion, input and output routines).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Text;

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    private const int MAX_ZEROS_AFTER_DECIMAL = 2;

    // digits 0..64 used by bases 2 .. 64
    private const string DIGITS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_@";

    /// <summary>Default decimal separator.</summary>
    public static char g_decimalSeparator = '.';

    public static void SetDecimalSeparator(char decimalSeparator)
    {
        g_decimalSeparator = decimalSeparator;
    }

    // ---------------------------------------------------------------------
    // integer <-> number / rat
    // ---------------------------------------------------------------------

    public static Number i32tonum(int ini32, uint radix)
    {
        Number pnumret = createnum(MAX_LONG_SIZE);
        int pmant = 0;
        pnumret.Cdigit = 0;
        pnumret.Exp = 0;
        if (ini32 < 0)
        {
            pnumret.Sign = -1;
            ini32 = unchecked(ini32 * -1);
        }
        else
        {
            pnumret.Sign = 1;
        }

        uint u = unchecked((uint)ini32);
        do
        {
            pnumret.Mant[pmant++] = u % radix;
            u /= radix;
            pnumret.Cdigit++;
        }
        while (u != 0);

        return pnumret;
    }

    public static Number Ui32tonum(uint ini32, uint radix)
    {
        Number pnumret = createnum(MAX_LONG_SIZE);
        int pmant = 0;
        pnumret.Cdigit = 0;
        pnumret.Exp = 0;
        pnumret.Sign = 1;

        do
        {
            pnumret.Mant[pmant++] = ini32 % radix;
            ini32 /= radix;
            pnumret.Cdigit++;
        }
        while (ini32 != 0);

        return pnumret;
    }

    public static Rat i32torat(int ini32)
    {
        return new Rat { Pp = i32tonum(ini32, BASEX), Pq = i32tonum(1, BASEX) };
    }

    public static Rat Ui32torat(uint inui32)
    {
        return new Rat { Pp = Ui32tonum(inui32, BASEX), Pq = i32tonum(1, BASEX) };
    }

    public static int numtoi32(Number pnum, uint radix)
    {
        int lret = 0;

        int pmant = pnum.Cdigit - 1;

        int expt = pnum.Exp;
        for (int length = pnum.Cdigit; length > 0 && length + expt > 0; length--)
        {
            lret = unchecked((int)((uint)lret * radix));
            lret = unchecked((int)((uint)lret + pnum.Mant[pmant--]));
        }

        while (expt-- > 0)
        {
            lret = unchecked((int)((uint)lret * radix));
        }

        lret = unchecked(lret * pnum.Sign);

        return lret;
    }

    public static int rattoi32(Rat prat, uint radix, int precision)
    {
        if (rat_gt(prat, rat_max_i32, precision) || rat_lt(prat, rat_min_i32, precision))
        {
            // Don't attempt rattoi32 of anything too big or small
            throw new CalcException(CalcErr.Domain);
        }

        Rat pint = DupRat(prat);

        intrat(ref pint, radix, precision);
        divnumx(ref pint.Pp, pint.Pq, precision);
        pint.Pq = DupNum(num_one);

        return numtoi32(pint.Pp, BASEX);
    }

    public static uint rattoUi32(Rat prat, uint radix, int precision)
    {
        if (rat_gt(prat, rat_dword, precision) || rat_lt(prat, rat_zero, precision))
        {
            // Don't attempt rattoui32 of anything too big or small
            throw new CalcException(CalcErr.Domain);
        }

        Rat pint = DupRat(prat);

        intrat(ref pint, radix, precision);
        divnumx(ref pint.Pp, pint.Pq, precision);
        pint.Pq = DupNum(num_one);

        return unchecked((uint)numtoi32(pint.Pp, BASEX)); // This happens to work even if it is only signed
    }

    public static ulong rattoUi64(Rat prat, uint radix, int precision)
    {
        // first get the LO 32 bit word
        Rat pint = DupRat(prat);
        andrat(ref pint, rat_dword, radix, precision);      // & 0xFFFFFFFF   (2 ^ 32 -1)
        uint lo = rattoUi32(pint, radix, precision);        // wont throw exception because already hi-dword chopped off

        pint = DupRat(prat);
        Rat prat32 = i32torat(32);
        rshrat(ref pint, prat32, radix, precision);
        intrat(ref pint, radix, precision);
        andrat(ref pint, rat_dword, radix, precision);      // & 0xFFFFFFFF   (2 ^ 32 -1)
        uint hi = rattoUi32(pint, radix, precision);

        return ((ulong)hi << 32) | lo;
    }

    // ---------------------------------------------------------------------
    // radix conversions
    // ---------------------------------------------------------------------

    public static Rat numtorat(Number pin, uint radix)
    {
        Number pnRadixn = DupNum(pin);

        Number qnRadixn = i32tonum(1, radix);

        // Ensure p and q start out as integers.
        if (pnRadixn.Exp < 0)
        {
            qnRadixn.Exp -= pnRadixn.Exp;
            pnRadixn.Exp = 0;
        }

        var pout = new Rat();

        pout.Pp = numtonRadixx(pnRadixn, radix);
        pout.Pq = numtonRadixx(qnRadixn, radix);

        return pout;
    }

    /// <summary>Does a base conversion on a number from internal to requested base.</summary>
    public static Number nRadixxtonum(Number a, uint radix, int precision)
    {
        Number sum = i32tonum(0, radix);
        Number powofnRadix = i32tonum(unchecked((int)BASEX), radix);

        // A large penalty is paid for conversion of digits no one will see anyway.
        // limit the digits to the minimum of the existing precision or the requested precision.
        uint cdigits = (uint)(precision + 1);
        if (cdigits > (uint)a.Cdigit)
        {
            cdigits = (uint)a.Cdigit;
        }

        // scale by the internal base to the internal exponent offset of the LSD
        numpowi32(ref powofnRadix, a.Exp + (a.Cdigit - (int)cdigits), radix, precision);

        // Loop over all the relative digits from MSD to LSD
        for (int ptr = a.Cdigit - 1; cdigits > 0; ptr--, cdigits--)
        {
            // Loop over all the bits from MSB to LSB
            for (uint bitmask = BASEX / 2; bitmask > 0; bitmask /= 2)
            {
                addnum(ref sum, sum, radix);
                if ((a.Mant[ptr] & bitmask) != 0)
                {
                    sum.Mant[0] |= 1;
                }
            }
        }

        // Scale answer by power of internal exponent.
        mulnum(ref sum, powofnRadix, radix);

        sum.Sign = a.Sign;
        return sum;
    }

    /// <summary>Does a radix conversion on a number from specified radix to the internal radix.</summary>
    public static Number numtonRadixx(Number a, uint radix)
    {
        Number pnumret = i32tonum(0, BASEX); // pnumret is the number in internal form.
        Number num_radix = i32tonum((int)radix, BASEX);

        // Digits are in reverse order, back over them LSD first.
        int ptrdigit = a.Cdigit - 1;

        for (int idigit = 0; idigit < a.Cdigit; idigit++)
        {
            mulnumx(ref pnumret, num_radix);
            // WARNING:
            // This should just smack in each digit into a 'special' thisdigit.
            // and not do the overhead of recreating the number type each time.
            Number thisdigit = i32tonum(unchecked((int)a.Mant[ptrdigit--]), BASEX);
            addnum(ref pnumret, thisdigit, BASEX);
        }

        // Calculate the exponent of the external base for scaling.
        numpowi32x(ref num_radix, a.Exp);

        // ... and scale the result.
        mulnumx(ref pnumret, num_radix);

        // And propagate the sign.
        pnumret.Sign = a.Sign;

        return pnumret;
    }

    // ---------------------------------------------------------------------
    // StringToNumber / StringToRat
    // ---------------------------------------------------------------------

    private const byte DP = 0;
    private const byte ZR = 1;
    private const byte NZ = 2;
    private const byte SG = 3;
    private const byte EX = 4;

    private const byte START = 0;
    private const byte MANTS = 1;
    private const byte LZ = 2;
    private const byte LZDP = 3;
    private const byte LD = 4;
    private const byte DZ = 5;
    private const byte DD = 6;
    private const byte DDP = 7;
    private const byte EXPB = 8;
    private const byte EXPS = 9;
    private const byte EXPD = 10;
    private const byte EXPBZ = 11;
    private const byte EXPSZ = 12;
    private const byte EXPDZ = 13;
    private const byte ERR = 14;

    // New state is machine[state, terminal]
    private static readonly byte[,] machine =
    {
        //        DP,     ZR,      NZ,      SG,     EX
        /* START */ { LZDP, LZ, LD, MANTS, ERR },
        /* MANTS */ { LZDP, LZ, LD, ERR, ERR },
        /* LZ    */ { LZDP, LZ, LD, ERR, EXPBZ },
        /* LZDP  */ { ERR, DZ, DD, ERR, EXPB },
        /* LD    */ { DDP, LD, LD, ERR, EXPB },
        /* DZ    */ { ERR, DZ, DD, ERR, EXPBZ },
        /* DD    */ { ERR, DD, DD, ERR, EXPB },
        /* DDP   */ { ERR, DD, DD, ERR, EXPB },
        /* EXPB  */ { ERR, EXPD, EXPD, EXPS, ERR },
        /* EXPS  */ { ERR, EXPD, EXPD, ERR, ERR },
        /* EXPD  */ { ERR, EXPD, EXPD, ERR, ERR },
        /* EXPBZ */ { ERR, EXPDZ, EXPDZ, EXPSZ, ERR },
        /* EXPSZ */ { ERR, EXPDZ, EXPDZ, ERR, ERR },
        /* EXPDZ */ { ERR, EXPDZ, EXPDZ, ERR, ERR },
        /* ERR   */ { ERR, ERR, ERR, ERR, ERR },
    };

    private static char NormalizeCharDigit(char c, uint radix)
    {
        // Allow upper and lower case letters as equivalent, base is in the range where this is not ambiguous.
        if (radix >= (uint)DIGITS.IndexOf('A') && radix <= (uint)DIGITS.IndexOf('Z'))
        {
            return char.ToUpperInvariant(c);
        }

        return c;
    }

    /// <summary>Returns null if no number scanned.</summary>
    public static Number? StringToNumber(string numberString, uint radix, int precision)
    {
        int expSign = 1;  // expSign is exponent sign ( +/- 1 )
        int expValue = 0; // expValue is exponent mantissa, should be unsigned

        Number pnumret = createnum(numberString.Length);
        pnumret.Sign = 1;
        pnumret.Cdigit = 0;
        pnumret.Exp = 0;
        int pmant = numberString.Length - 1;

        byte state = START; // state is the state of the input state machine.
        foreach (char c in numberString)
        {
            // If the character is the decimal separator, use '.' for the purposes of the state machine.
            char curChar = c == g_decimalSeparator ? '.' : c;

            // Switch states based on the character we encountered
            switch (curChar)
            {
                case '-':
                case '+':
                    state = machine[state, SG];
                    break;
                case '.':
                    state = machine[state, DP];
                    break;
                case '0':
                    state = machine[state, ZR];
                    break;
                case '^':
                case 'e':
                    if (curChar == '^' || radix == 10)
                    {
                        state = machine[state, EX];
                    }
                    else
                    {
                        // the 'e'-as-a-digit case
                        state = machine[state, NZ];
                    }

                    break;
                default:
                    state = machine[state, NZ];
                    break;
            }

            // Now update our result value based on the state we are in
            switch (state)
            {
                case MANTS:
                    pnumret.Sign = curChar == '-' ? -1 : 1;
                    break;
                case EXPSZ:
                case EXPS:
                    expSign = curChar == '-' ? -1 : 1;
                    break;
                case EXPDZ:
                case EXPD:
                {
                    curChar = NormalizeCharDigit(curChar, radix);

                    int pos = DIGITS.IndexOf(curChar);
                    if (pos >= 0)
                    {
                        expValue = unchecked(expValue * (int)radix);
                        expValue = unchecked(expValue + pos);
                    }
                    else
                    {
                        state = ERR;
                    }

                    break;
                }

                case LD:
                case DD:
                {
                    if (state == LD)
                    {
                        pnumret.Exp++;
                    }

                    curChar = NormalizeCharDigit(curChar, radix);

                    int pos = DIGITS.IndexOf(curChar);
                    if (pos >= 0 && pos < (int)radix)
                    {
                        pnumret.Mant[pmant--] = (uint)pos;
                        pnumret.Exp--;
                        pnumret.Cdigit++;
                    }
                    else
                    {
                        state = ERR;
                    }

                    break;
                }

                case DZ:
                    pnumret.Exp--;
                    break;
                case LZ:
                case LZDP:
                case DDP:
                    break;
            }
        }

        if (state == DZ || state == EXPDZ)
        {
            pnumret.Cdigit = 1;
            pnumret.Exp = 0;
            pnumret.Sign = 1;
        }
        else
        {
            while (pnumret.Cdigit < numberString.Length)
            {
                pnumret.Cdigit++;
                pnumret.Exp--;
            }

            pnumret.Exp += expSign * expValue;
        }

        // If we don't have a number, clear our result.
        if (pnumret.Cdigit == 0)
        {
            return null;
        }

        stripzeroesnum(pnumret, precision);
        return pnumret;
    }

    /// <summary>Returns null if no number scanned.</summary>
    public static Rat? StringToRat(bool mantissaIsNegative, string mantissa, bool exponentIsNegative, string exponent, uint radix, int precision)
    {
        Rat resultRat; // holds exponent in rational form.

        // Deal with mantissa
        if (mantissa.Length == 0)
        {
            // Preset value if no mantissa
            if (exponent.Length == 0)
            {
                // Exponent not specified, preset value to zero
                resultRat = DupRat(rat_zero);
            }
            else
            {
                // Exponent specified, preset value to one
                resultRat = DupRat(rat_one);
            }
        }
        else
        {
            // Mantissa specified, convert to number form.
            Number? pnummant = StringToNumber(mantissa, radix, precision);
            if (pnummant == null)
            {
                return null;
            }

            // convert to rational form, and cleanup.
            resultRat = numtorat(pnummant, radix);
        }

        // Deal with exponent
        int expt = 0;
        if (exponent.Length != 0)
        {
            // Exponent specified, convert to number form.
            // Don't use native stuff, as it is restricted in the bases it can handle.
            Number? numExp = StringToNumber(exponent, radix, precision);
            if (numExp == null)
            {
                return null;
            }

            // Convert exponent number form to native integral form, and cleanup.
            expt = numtoi32(numExp, radix);
        }

        // Convert native integral exponent form to rational multiplier form.
        Number pnumexp = i32tonum((int)radix, BASEX);
        numpowi32x(ref pnumexp, Math.Abs(expt));

        var pratexp = new Rat { Pp = DupNum(pnumexp), Pq = i32tonum(1, BASEX) };

        if (exponentIsNegative)
        {
            // multiplier is less than 1, this means divide.
            divrat(ref resultRat, pratexp, precision);
        }
        else if (expt > 0)
        {
            // multiplier is greater than 1, this means multiply.
            mulrat(ref resultRat, pratexp, precision);
        }

        // multiplier can be 1, in which case it'd be a waste of time to multiply.

        if (mantissaIsNegative)
        {
            // A negative number was used, adjust the sign.
            resultRat.Pp.Sign *= -1;
        }

        return resultRat;
    }

    // ---------------------------------------------------------------------
    // stripzeroesnum / NumberToString
    // ---------------------------------------------------------------------

    /// <summary>Strips off trailing zeros. Returns true if stripping was done; modifies the number in place.</summary>
    public static bool stripzeroesnum(Number pnum, int starting)
    {
        bool fstrip = false;
        // point pmant to the LeastCalculatedDigit
        int pmant = 0;
        int cdigits = pnum.Cdigit;
        // point pmant to the LSD
        if (cdigits > starting)
        {
            pmant += cdigits - starting;
            cdigits = starting;
        }

        // Check we haven't gone too far, and we are still looking at zeros.
        while (cdigits > 0 && pnum.Mant[pmant] == 0)
        {
            // move to next significant digit and keep track of digits we can ignore later.
            pmant++;
            cdigits--;
            fstrip = true;
        }

        // If there are zeros to remove.
        if (fstrip)
        {
            // Remove them.
            Array.Copy(pnum.Mant, pmant, pnum.Mant, 0, cdigits);
            // And adjust exponent and digit count accordingly.
            pnum.Exp += pnum.Cdigit - cdigits;
            pnum.Cdigit = cdigits;
        }

        return fstrip;
    }

    public static string NumberToString(ref Number pnum, NumberFormat format, uint radix, int precision)
    {
        stripzeroesnum(pnum, precision + 2);
        int length = pnum.Cdigit;
        int exponent = pnum.Exp + length; // Actual number of digits to the left of decimal

        NumberFormat oldFormat = format;
        if (exponent > precision && format == NumberFormat.Float)
        {
            // Force scientific mode to prevent user from assuming 33rd digit is exact.
            format = NumberFormat.Scientific;
        }

        // Make length small enough to fit in pret.
        if (length > precision)
        {
            length = precision;
        }

        // If there is a chance a round has to occur, round.
        // - if number is zero no rounding
        // - if number of digits is less than the maximum output no rounding
        Number? round = null;
        if (!zernum(pnum) && (pnum.Cdigit >= precision || (length - exponent > precision && exponent >= -MAX_ZEROS_AFTER_DECIMAL)))
        {
            // Otherwise round.
            round = i32tonum((int)radix, radix);
            divnum(ref round, num_two, radix, precision);

            // Make round number exponent one below the LSD for the number.
            if (exponent > 0 || format == NumberFormat.Float)
            {
                round.Exp = pnum.Exp + pnum.Cdigit - round.Cdigit - precision;
            }
            else
            {
                round.Exp = pnum.Exp + pnum.Cdigit - round.Cdigit - precision - exponent;
                length = precision + exponent;
            }

            round.Sign = pnum.Sign;
        }

        if (format == NumberFormat.Float)
        {
            // Figure out if the exponent will fill more space than the non-exponent field.
            if ((length - exponent > precision) || (exponent > precision + 3))
            {
                if (exponent >= -MAX_ZEROS_AFTER_DECIMAL)
                {
                    if (round != null)
                    {
                        round.Exp -= exponent;
                    }

                    length = precision + exponent;
                }
                else
                {
                    // Case where too many zeros are to the right or left of the decimal pt.
                    // And we are forced to switch to scientific form.
                    format = NumberFormat.Scientific;
                }
            }
            else if (length + Math.Abs(exponent) < precision && round != null)
            {
                // Minimum loss of precision occurs with listing leading zeros
                // if we need to make room for zeros sacrifice some digits.
                round.Exp -= exponent;
            }
        }

        if (round != null)
        {
            addnum(ref pnum, round, radix);
            int offset = (pnum.Cdigit + pnum.Exp) - (round.Cdigit + round.Exp);
            if (stripzeroesnum(pnum, offset))
            {
                // WARNING: nesting/recursion, too much has been changed, need to re-figure format.
                return NumberToString(ref pnum, oldFormat, radix, precision);
            }
        }
        else
        {
            stripzeroesnum(pnum, precision);
        }

        // Set up all the post rounding stuff.
        bool useSciForm = false;
        int eout = exponent - 1; // Displayed exponent.
        int pmant = pnum.Cdigit - 1;
        // Case where too many digits are to the left of the decimal or
        // NumberFormat.Scientific or NumberFormat.Engineering was specified.
        if (format == NumberFormat.Scientific || format == NumberFormat.Engineering)
        {
            useSciForm = true;
            if (eout != 0)
            {
                if (format == NumberFormat.Engineering)
                {
                    exponent = eout % 3;
                    eout -= exponent;
                    exponent++;

                    // Fix the case where 0.02e-3 should really be 2.e-6 etc.
                    if (exponent < 0)
                    {
                        exponent += 3;
                        eout -= 3;
                    }
                }
                else
                {
                    exponent = 1;
                }
            }
        }
        else
        {
            eout = 0;
        }

        // Begin building the result string
        var result = new StringBuilder();

        // Make sure negative zeros aren't allowed.
        if (pnum.Sign == -1 && length > 0)
        {
            result.Append('-');
        }

        if (exponent <= 0 && !useSciForm)
        {
            result.Append('0');
            result.Append(g_decimalSeparator);
            // Used up a digit unaccounted for.
        }

        while (exponent < 0)
        {
            result.Append('0');
            exponent++;
        }

        while (length > 0)
        {
            exponent--;
            result.Append(DIGITS[(int)pnum.Mant[pmant--]]);
            length--;

            // Be more regular in using a decimal point.
            if (exponent == 0)
            {
                result.Append(g_decimalSeparator);
            }
        }

        while (exponent > 0)
        {
            result.Append('0');
            exponent--;
            // Be more regular in using a decimal point.
            if (exponent == 0)
            {
                result.Append(g_decimalSeparator);
            }
        }

        if (useSciForm)
        {
            result.Append(radix == 10 ? 'e' : '^');
            result.Append(eout < 0 ? '-' : '+');
            eout = Math.Abs(eout);
            var expString = new StringBuilder();
            do
            {
                expString.Append(DIGITS[(int)(eout % radix)]);
                eout /= (int)radix;
            }
            while (eout > 0);

            for (int i = expString.Length - 1; i >= 0; i--)
            {
                result.Append(expString[i]);
            }
        }

        // Remove trailing decimal
        if (result.Length > 0 && result[result.Length - 1] == g_decimalSeparator)
        {
            result.Length--;
        }

        return result.ToString();
    }

    public static string RatToString(Rat prat, NumberFormat format, uint radix, int precision)
    {
        Number p = RatToNumber(prat, radix, precision);

        return NumberToString(ref p, format, radix, precision);
    }

    public static Number RatToNumber(Rat prat, uint radix, int precision)
    {
        Rat temprat = DupRat(prat);
        // Convert p and q of rational form from internal base to requested base.
        // Scale by largest power of BASEX possible.
        int scaleby = Math.Min(temprat.Pp.Exp, temprat.Pq.Exp);
        scaleby = Math.Max(scaleby, 0);

        temprat.Pp.Exp -= scaleby;
        temprat.Pq.Exp -= scaleby;

        Number p = nRadixxtonum(temprat.Pp, radix, precision);
        Number q = nRadixxtonum(temprat.Pq, radix, precision);

        // finally take the time hit to actually divide.
        divnum(ref p, q, radix, precision);

        return p;
    }

    /// <summary>Converts a PRAT to a PNUMBER and back to a PRAT, flattening/simplifying the rational in the process.</summary>
    public static void flatrat(ref Rat prat, uint radix, int precision)
    {
        Number pnum = RatToNumber(prat, radix, precision);

        prat = numtorat(pnum, radix);
    }

    // ---------------------------------------------------------------------
    // gcd, factorial helpers, powers
    // ---------------------------------------------------------------------

    public static Number gcd(Number a, Number b)
    {
        if (zernum(a))
        {
            return b;
        }
        else if (zernum(b))
        {
            return a;
        }

        Number larger;
        Number smaller;
        if (lessnum(a, b))
        {
            larger = DupNum(b);
            smaller = DupNum(a);
        }
        else
        {
            larger = DupNum(a);
            smaller = DupNum(b);
        }

        while (!zernum(smaller))
        {
            remnum(ref larger, smaller, BASEX);
            // swap larger and smaller
            (larger, smaller) = (smaller, larger);
        }

        return larger;
    }

    public static Number i32factnum(int ini32, uint radix)
    {
        Number lret = i32tonum(1, radix);

        while (ini32 > 0)
        {
            Number tmp = i32tonum(ini32--, radix);
            mulnum(ref lret, tmp, radix);
        }

        return lret;
    }

    public static Number i32prodnum(int start, int stop, uint radix)
    {
        Number lret = i32tonum(1, radix);

        while (start <= stop)
        {
            if (start != 0)
            {
                Number tmp = i32tonum(start, radix);
                mulnum(ref lret, tmp, radix);
            }

            start++;
        }

        return lret;
    }

    /// <summary>root = root ** power. Assumes radix is the radix of root.</summary>
    public static void numpowi32(ref Number proot, int power, uint radix, int precision)
    {
        Number lret = i32tonum(1, radix);

        while (power > 0)
        {
            if ((power & 1) != 0)
            {
                mulnum(ref lret, proot, radix);
            }

            mulnum(ref proot, proot, radix);
            TRIMNUM(proot, precision);
            power >>= 1;
        }

        proot = lret;
    }

    public static void ratpowi32(ref Rat proot, int power, int precision)
    {
        bool invertResult = power < 0;
        uint magnitude = invertResult ? (uint)(-(long)power) : (uint)power;

        Rat lret = i32torat(1);

        while (magnitude > 0)
        {
            if ((magnitude & 1) != 0)
            {
                mulnumx(ref lret.Pp, proot.Pp);
                mulnumx(ref lret.Pq, proot.Pq);
            }

            mulrat(ref proot, proot, precision);
            trimit(ref lret, precision);
            trimit(ref proot, precision);
            magnitude >>= 1;
        }

        proot = lret;

        if (invertResult)
        {
            // Invert after exponentiation so INT32_MIN never needs to be negated as an int32_t.
            (proot.Pp, proot.Pq) = (proot.Pq, proot.Pp);
        }
    }
}
