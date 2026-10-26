// Port of ratpak num.cpp and basex.cpp (number level arithmetic).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    public const uint BASEXPWR = 31;           // Internal log2(BASEX)
    public const uint BASEX = 0x80000000;      // Internal radix used in calculations
    public const int MAX_LONG_SIZE = 33;       // Base 2 requires 32 'digits'

    private const uint MantMask = 0x7FFFFFFF;  // (uint)~BASEX

    // ---------------------------------------------------------------------
    // Allocation helpers (the C++ macros createnum / DUPNUM / DUPRAT)
    // ---------------------------------------------------------------------

    public static Number createnum(int size) => new Number(size);

    public static Number DupNum(Number b)
    {
        var a = new Number(b.Cdigit)
        {
            Sign = b.Sign,
            Cdigit = b.Cdigit,
            Exp = b.Exp,
        };
        Array.Copy(b.Mant, a.Mant, b.Cdigit);
        return a;
    }

    public static Rat DupRat(Rat b) => new Rat { Pp = DupNum(b.Pp), Pq = DupNum(b.Pq) };

    // ---------------------------------------------------------------------
    // addnum
    // ---------------------------------------------------------------------

    public static void addnum(ref Number pa, Number b, uint radix)
    {
        if (b.Cdigit > 1 || b.Mant[0] != 0)
        {
            // b is not zero
            if (pa.Cdigit > 1 || pa.Mant[0] != 0)
            {
                _addnum(ref pa, b, radix);
            }
            else
            {
                // if pa is zero and b isn't just copy b.
                pa = DupNum(b);
            }
        }
    }

    private static void _addnum(ref Number pa, Number b, uint radix)
    {
        Number a = pa;
        uint cy = 0;
        bool fcompla = false;
        bool fcomplb = false;

        // Calculate the overlap of the numbers after alignment, this includes necessary padding 0's
        int cdigits = Math.Max(a.Cdigit + a.Exp, b.Cdigit + b.Exp) - Math.Min(a.Exp, b.Exp);

        Number c = createnum(cdigits + 1);
        c.Exp = Math.Min(a.Exp, b.Exp);
        int mexp = c.Exp;
        c.Cdigit = cdigits;
        int ia = 0, ib = 0, ic = 0;

        // Figure out the sign of the numbers
        if (a.Sign != b.Sign)
        {
            cy = 1;
            fcompla = a.Sign == -1;
            fcomplb = b.Sign == -1;
        }

        // Loop over all the digits, real and 0 padded. Here we know a and b are aligned
        for (; cdigits > 0; cdigits--, mexp++)
        {
            uint da = ((mexp >= a.Exp) && (cdigits + a.Exp - c.Exp > (c.Cdigit - a.Cdigit))) ? a.Mant[ia++] : 0u;
            uint db = ((mexp >= b.Exp) && (cdigits + b.Exp - c.Exp > (c.Cdigit - b.Cdigit))) ? b.Mant[ib++] : 0u;

            if (fcompla)
            {
                da = radix - 1 - da;
            }

            if (fcomplb)
            {
                db = radix - 1 - db;
            }

            cy = unchecked(da + db + cy);
            c.Mant[ic++] = cy % radix;
            cy /= radix;
        }

        // Handle carry from last sum as extra digit
        if (cy != 0 && !(fcompla || fcomplb))
        {
            c.Mant[ic++] = cy;
            c.Cdigit++;
        }

        // Compute sign of result
        if (!(fcompla || fcomplb))
        {
            c.Sign = a.Sign;
        }
        else
        {
            if (cy != 0)
            {
                c.Sign = 1;
            }
            else
            {
                // An overflow or underflow has occurred and all the digits need to be complemented.
                c.Sign = -1;
                cy = 1;
                ic = 0;
                for (cdigits = c.Cdigit; cdigits > 0; cdigits--)
                {
                    cy = unchecked(radix - 1 - c.Mant[ic] + cy);
                    c.Mant[ic++] = cy % radix;
                    cy /= radix;
                }
            }
        }

        // Remove leading zeros, remember digits are in order of increasing significance. i.e. 100 would be 0,0,1
        while (c.Cdigit > 1 && c.Mant[--ic] == 0)
        {
            c.Cdigit--;
        }

        pa = c;
    }

    // ---------------------------------------------------------------------
    // mulnum
    // ---------------------------------------------------------------------

    public static void mulnum(ref Number pa, Number b, uint radix)
    {
        if (b.Cdigit > 1 || b.Mant[0] != 1 || b.Exp != 0)
        {
            // If b is one we don't multiply exactly.
            if (pa.Cdigit > 1 || pa.Mant[0] != 1 || pa.Exp != 0)
            {
                _mulnum(ref pa, b, radix);
            }
            else
            {
                // if pa is one and b isn't just copy b, and adjust the sign.
                int sign = pa.Sign;
                pa = DupNum(b);
                pa.Sign *= sign;
            }
        }
        else
        {
            // But we do have to set the sign.
            pa.Sign *= b.Sign;
        }
    }

    private static void _mulnum(ref Number pa, Number b, uint radix)
    {
        Number a = pa;
        int ibdigit = a.Cdigit + b.Cdigit - 1;
        Number c = createnum(ibdigit + 1);
        c.Cdigit = ibdigit;
        c.Sign = a.Sign * b.Sign;
        c.Exp = a.Exp + b.Exp;

        int pcha = 0;
        int pchcoffset = 0;
        int icdigit = 0;

        for (int iadigit = a.Cdigit; iadigit > 0; iadigit--)
        {
            uint da = a.Mant[pcha++];
            int pchb = 0;

            // Shift pchc, and pchcoffset, one for each digit
            int pchc = pchcoffset++;

            for (ibdigit = b.Cdigit; ibdigit > 0; ibdigit--)
            {
                ulong cy = 0;
                ulong mcy = (ulong)da * b.Mant[pchb];
                if (mcy != 0)
                {
                    icdigit = 0;
                    if (ibdigit == 1 && iadigit == 1)
                    {
                        c.Cdigit++;
                    }
                }

                // If result is nonzero, or while result of carry is nonzero...
                while (mcy != 0 || cy != 0)
                {
                    // update carry from addition(s) and multiply.
                    cy += (ulong)c.Mant[pchc + icdigit] + (mcy % radix);

                    // update result digit from
                    c.Mant[pchc + icdigit++] = (uint)(cy % radix);

                    // update carries from
                    mcy /= radix;
                    cy /= radix;
                }

                pchb++;
                pchc++;
            }
        }

        // prevent different kinds of zeros, by stripping leading duplicate zeros.
        while (c.Cdigit > 1 && c.Mant[c.Cdigit - 1] == 0)
        {
            c.Cdigit--;
        }

        pa = c;
    }

    // ---------------------------------------------------------------------
    // remnum
    // ---------------------------------------------------------------------

    private static uint MSD(Number x) => x.Mant[x.Cdigit - 1];

    public static void remnum(ref Number pa, Number b, uint radix)
    {
        // Once pa is less than b, pa is the remainder.
        while (!lessnum(pa, b))
        {
            Number tmp = DupNum(b);
            if (lessnum(tmp, pa))
            {
                // Start off close to the right answer for subtraction.
                tmp.Exp = pa.Cdigit + pa.Exp - tmp.Cdigit;
                if (MSD(pa) <= MSD(tmp))
                {
                    // Don't take the chance that the numbers are equal.
                    tmp.Exp--;
                }
            }

            Number lasttmp = i32tonum(0, radix);

            while (lessnum(tmp, pa))
            {
                lasttmp = DupNum(tmp);
                addnum(ref tmp, tmp, radix);
            }

            if (lessnum(pa, tmp))
            {
                // too far, back up...
                tmp = lasttmp;
            }

            // Subtract the working remainder from the remainder holder.
            tmp.Sign = -1 * pa.Sign;
            addnum(ref pa, tmp, radix);
        }
    }

    // ---------------------------------------------------------------------
    // divnum
    // ---------------------------------------------------------------------

    public static void divnum(ref Number pa, Number b, uint radix, int precision)
    {
        if (b.Cdigit > 1 || b.Mant[0] != 1 || b.Exp != 0)
        {
            // b is not one
            _divnum(ref pa, b, radix, precision);
        }
        else
        {
            // But we do have to set the sign.
            pa.Sign *= b.Sign;
        }
    }

    private static void _divnum(ref Number pa, Number b, uint radix, int precision)
    {
        Number a = pa;
        int thismax = precision + 2;
        if (thismax < a.Cdigit)
        {
            thismax = a.Cdigit;
        }

        if (thismax < b.Cdigit)
        {
            thismax = b.Cdigit;
        }

        Number c = createnum(thismax + 1);
        c.Exp = (a.Cdigit + a.Exp) - (b.Cdigit + b.Exp) + 1;
        c.Sign = a.Sign * b.Sign;

        int ptrc = thismax;
        Number rem = DupNum(a);
        Number tmp = DupNum(b);
        tmp.Sign = a.Sign;
        rem.Exp = b.Cdigit + b.Exp - rem.Cdigit;

        // Build a table of multiplications of the divisor, this is quicker for more than radix 'digits'
        var table = new Number[radix];
        table[0] = i32tonum(0, radix);
        for (uint i = 1; i < radix; i++)
        {
            Number newValue = DupNum(table[i - 1]);
            addnum(ref newValue, tmp, radix);
            table[i] = newValue;
        }

        int cdigits = 0;
        while (cdigits++ < thismax && !zernum(rem))
        {
            int digit = 0;
            for (int d = (int)radix - 1; d >= 1; d--)
            {
                if (!lessnum(rem, table[d]))
                {
                    digit = d;
                    break;
                }
            }

            if (digit != 0)
            {
                Number multiple = table[digit];
                multiple.Sign *= -1;
                addnum(ref rem, multiple, radix);
                multiple.Sign *= -1;
            }

            rem.Exp++;
            c.Mant[ptrc--] = (uint)digit;
        }

        cdigits--;

        ptrc++;
        if (ptrc != 0)
        {
            Array.Copy(c.Mant, ptrc, c.Mant, 0, cdigits);
        }

        if (cdigits == 0)
        {
            c.Cdigit = 1;
            c.Exp = 0;
        }
        else
        {
            c.Cdigit = cdigits;
            c.Exp -= cdigits;
            while (c.Cdigit > 1 && c.Mant[c.Cdigit - 1] == 0)
            {
                c.Cdigit--;
            }
        }

        pa = c;
    }

    // ---------------------------------------------------------------------
    // comparisons
    // ---------------------------------------------------------------------

    public static bool equnum(Number a, Number b)
    {
        int diff = (a.Cdigit + a.Exp) - (b.Cdigit + b.Exp);
        if (diff != 0)
        {
            // If the exponents are different, these are different numbers.
            return false;
        }

        // OK the exponents match.
        int pa = a.Cdigit - 1;
        int pb = b.Cdigit - 1;
        int cdigits = Math.Max(a.Cdigit, b.Cdigit);
        int ccdigits = cdigits;

        // Loop over all digits until we run out of digits or there is a difference in the digits.
        for (; cdigits > 0; cdigits--)
        {
            uint da = (cdigits > (ccdigits - a.Cdigit)) ? a.Mant[pa--] : 0u;
            uint db = (cdigits > (ccdigits - b.Cdigit)) ? b.Mant[pb--] : 0u;
            if (da != db)
            {
                return false;
            }
        }

        // In this case, they are equal.
        return true;
    }

    /// <summary>abs(a) &lt; abs(b). WARNING: unsigned compare.</summary>
    public static bool lessnum(Number a, Number b)
    {
        int diff = (a.Cdigit + a.Exp) - (b.Cdigit + b.Exp);
        if (diff < 0)
        {
            // The exponent of a is less than b
            return true;
        }

        if (diff > 0)
        {
            return false;
        }

        int pa = a.Cdigit - 1;
        int pb = b.Cdigit - 1;
        int cdigits = Math.Max(a.Cdigit, b.Cdigit);
        int ccdigits = cdigits;
        for (; cdigits > 0; cdigits--)
        {
            uint da = (cdigits > (ccdigits - a.Cdigit)) ? a.Mant[pa--] : 0u;
            uint db = (cdigits > (ccdigits - b.Cdigit)) ? b.Mant[pb--] : 0u;
            if (da != db)
            {
                return da < db;
            }
        }

        // In this case, they are equal.
        return false;
    }

    public static bool zernum(Number a)
    {
        for (int i = 0; i < a.Cdigit; i++)
        {
            if (a.Mant[i] != 0)
            {
                // One of the digits isn't zero, therefore the number isn't zero
                return false;
            }
        }

        // All of the digits are zero, therefore the number is zero
        return true;
    }

    // ---------------------------------------------------------------------
    // basex.cpp: arithmetic in the internal base BASEX
    // ---------------------------------------------------------------------

    public static void mulnumx(ref Number pa, Number b)
    {
        if (b.Cdigit > 1 || b.Mant[0] != 1 || b.Exp != 0)
        {
            // If b is not one we multiply
            if (pa.Cdigit > 1 || pa.Mant[0] != 1 || pa.Exp != 0)
            {
                // pa and b are both non-one.
                _mulnumx(ref pa, b);
            }
            else
            {
                // if pa is one and b isn't just copy b. and adjust the sign.
                int sign = pa.Sign;
                pa = DupNum(b);
                pa.Sign *= sign;
            }
        }
        else
        {
            // B is +/- 1, But we do have to set the sign.
            pa.Sign *= b.Sign;
        }
    }

    private static void _mulnumx(ref Number pa, Number b)
    {
        Number a = pa;
        int ibdigit = a.Cdigit + b.Cdigit - 1;
        Number c = createnum(ibdigit + 1);
        c.Cdigit = ibdigit;
        c.Sign = a.Sign * b.Sign;
        c.Exp = a.Exp + b.Exp;

        uint[] am = a.Mant;
        uint[] bm = b.Mant;
        uint[] cm = c.Mant;

        int ptra = 0;
        int ptrcoffset = 0;
        int icdigit = 0;

        for (int iadigit = a.Cdigit; iadigit > 0; iadigit--)
        {
            uint da = am[ptra++];
            int ptrb = 0;

            // Shift ptrc, and ptrcoffset, one for each digit
            int ptrc = ptrcoffset++;

            for (ibdigit = b.Cdigit; ibdigit > 0; ibdigit--)
            {
                ulong cy = 0;
                ulong mcy = (ulong)da * bm[ptrb];
                if (mcy != 0)
                {
                    icdigit = 0;
                    if (ibdigit == 1 && iadigit == 1)
                    {
                        c.Cdigit++;
                    }
                }

                // If result is nonzero, or while result of carry is nonzero...
                while (mcy != 0 || cy != 0)
                {
                    // update carry from addition(s) and multiply.
                    cy += (ulong)cm[ptrc + icdigit] + ((uint)mcy & MantMask);

                    // update result digit from
                    cm[ptrc + icdigit++] = (uint)cy & MantMask;

                    // update carries from
                    mcy >>= (int)BASEXPWR;
                    cy >>= (int)BASEXPWR;
                }

                ptrb++;
                ptrc++;
            }
        }

        // prevent different kinds of zeros, by stripping leading duplicate zeros.
        while (c.Cdigit > 1 && cm[c.Cdigit - 1] == 0)
        {
            c.Cdigit--;
        }

        pa = c;
    }

    /// <summary>root = root ** power, base BASEX.</summary>
    public static void numpowi32x(ref Number proot, int power)
    {
        Number lret = i32tonum(1, BASEX);

        // Once the power remaining is zero we are done.
        while (power > 0)
        {
            // If this bit in the power decomposition is on, multiply the result by the root number.
            if ((power & 1) != 0)
            {
                mulnumx(ref lret, proot);
            }

            // multiply the root number by itself to scale for the next bit (i.e. square it.
            mulnumx(ref proot, proot);

            // move the next bit of the power into place.
            power >>= 1;
        }

        proot = lret;
    }

    public static void divnumx(ref Number pa, Number b, int precision)
    {
        if (b.Cdigit > 1 || b.Mant[0] != 1 || b.Exp != 0)
        {
            // b is not one.
            if (pa.Cdigit > 1 || pa.Mant[0] != 1 || pa.Exp != 0)
            {
                // pa and b are both not one.
                _divnumx(ref pa, b, precision);
            }
            else
            {
                // if pa is one and b is not one, just copy b, and adjust the sign.
                int sign = pa.Sign;
                pa = DupNum(b);
                pa.Sign *= sign;
            }
        }
        else
        {
            // b is one so don't divide, but set the sign.
            pa.Sign *= b.Sign;
        }
    }

    private static void _divnumx(ref Number pa, Number b, int precision)
    {
        Number a = pa;

        int thismax = precision + g_ratio; // set a maximum number of internal digits to shoot for in the divide.

        if (thismax < a.Cdigit)
        {
            // a has more digits than precision specified, bump up digits to shoot for.
            thismax = a.Cdigit;
        }

        if (thismax < b.Cdigit)
        {
            // b has more digits than precision specified, bump up digits to shoot for.
            thismax = b.Cdigit;
        }

        // Create c (the divide answer) and set up exponent and sign.
        Number c = createnum(thismax + 1);
        c.Exp = (a.Cdigit + a.Exp) - (b.Cdigit + b.Exp) + 1;
        c.Sign = a.Sign * b.Sign;

        int ptrc = thismax;
        int cdigits = 0;

        Number rem = DupNum(a);
        rem.Sign = b.Sign;
        rem.Exp = b.Cdigit + b.Exp - rem.Cdigit;

        while (cdigits++ < thismax && !zernum(rem))
        {
            long digit = 0;
            c.Mant[ptrc] = 0;
            while (!lessnum(rem, b))
            {
                digit = 1;
                Number tmp = DupNum(b);
                Number lasttmp = i32tonum(0, BASEX);
                while (lessnum(tmp, rem))
                {
                    lasttmp = DupNum(tmp);
                    addnum(ref tmp, tmp, BASEX);
                    digit *= 2;
                }

                if (lessnum(rem, tmp))
                {
                    // too far, back up...
                    digit /= 2;
                    tmp = lasttmp;
                }

                tmp.Sign *= -1;
                addnum(ref rem, tmp, BASEX);
                c.Mant[ptrc] |= (uint)digit;
            }

            rem.Exp++;
            ptrc--;
        }

        cdigits--;
        ptrc++;
        if (ptrc != 0)
        {
            Array.Copy(c.Mant, ptrc, c.Mant, 0, cdigits);
        }

        if (cdigits == 0)
        {
            // A zero, make sure no weird exponents creep in
            c.Exp = 0;
            c.Cdigit = 1;
        }
        else
        {
            c.Cdigit = cdigits;
            c.Exp -= cdigits;
            // prevent different kinds of zeros, by stripping leading duplicate zeros.
            while (c.Cdigit > 1 && c.Mant[c.Cdigit - 1] == 0)
            {
                c.Cdigit--;
            }
        }

        pa = c;
    }
}
