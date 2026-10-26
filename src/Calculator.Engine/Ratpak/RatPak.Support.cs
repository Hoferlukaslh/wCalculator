// Port of ratpak support.cpp (constants, comparisons, scaling, trimming).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    private const int RATIO_FOR_DECIMAL = 9;
    private const int DECIMAL = 10;
    private const int CALC_DECIMAL_DIGITS_DEFAULT = 32;

    private static int cbitsofprecision = RATIO_FOR_DECIMAL * DECIMAL * CALC_DECIMAL_DIGITS_DEFAULT;

    /// <summary>Ratio of internal 'digits' to output 'digits'. Calculated when the base is changed.</summary>
    public static int g_ratio;

    /// <summary>Set to true if you don't want chopping internally.</summary>
    public static bool g_ftrueinfinite = false;

    public static Number num_one = null!;
    public static Number num_two = null!;
    public static Number num_five = null!;
    public static Number num_six = null!;
    public static Number num_ten = null!;

    public static Rat ln_ten = null!;
    public static Rat ln_two = null!;
    public static Rat rat_zero = null!;
    public static Rat rat_one = null!;
    public static Rat rat_neg_one = null!;
    public static Rat rat_two = null!;
    public static Rat rat_six = null!;
    public static Rat rat_half = null!;
    public static Rat rat_ten = null!;
    public static Rat pt_eight_five = null!;
    public static Rat pi = null!;
    public static Rat pi_over_two = null!;
    public static Rat two_pi = null!;
    public static Rat one_pt_five_pi = null!;
    public static Rat e_to_one_half = null!;
    public static Rat rat_exp = null!;
    public static Rat rad_to_deg = null!;
    public static Rat rad_to_grad = null!;
    public static Rat rat_qword = null!;
    public static Rat rat_dword = null!; // unsigned max ui32
    public static Rat rat_word = null!;
    public static Rat rat_byte = null!;
    public static Rat rat_360 = null!;
    public static Rat rat_400 = null!;
    public static Rat rat_180 = null!;
    public static Rat rat_200 = null!;
    public static Rat rat_nRadix = null!;
    public static Rat rat_smallest = null!;
    public static Rat rat_negsmallest = null!;
    public static Rat rat_max_exp = null!;
    public static Rat rat_min_exp = null!;
    public static Rat rat_max_fact = null!;
    public static Rat rat_min_fact = null!;
    public static Rat rat_min_i32 = null!; // min signed i32
    public static Rat rat_max_i32 = null!; // max signed i32

    // ---------------------------------------------------------------------
    // Macro equivalents from ratpak.h
    // ---------------------------------------------------------------------

    public static int SIGN(Rat prat) => prat.Pp.Sign * prat.Pq.Sign;

    public static void ABSRAT(Rat x)
    {
        x.Pp.Sign = 1;
        x.Pq.Sign = 1;
    }

    public static int LOGNUMRADIX(Number pnum) => (pnum.Cdigit + pnum.Exp) * g_ratio;

    public static int LOGRATRADIX(Rat prat) => LOGNUMRADIX(prat.Pp) - LOGNUMRADIX(prat.Pq);

    public static int LOGNUM2(Number pnum) => pnum.Cdigit + pnum.Exp;

    public static int LOGRAT2(Rat prat) => LOGNUM2(prat.Pp) - LOGNUM2(prat.Pq);

    /// <summary>RENORMALIZE: gets the exponents non-negative.</summary>
    public static void RENORMALIZE(Rat x)
    {
        if (x.Pp.Exp < 0)
        {
            x.Pq.Exp -= x.Pp.Exp;
            x.Pp.Exp = 0;
        }

        if (x.Pq.Exp < 0)
        {
            x.Pp.Exp -= x.Pq.Exp;
            x.Pq.Exp = 0;
        }
    }

    /// <summary>TRIMNUM: ASSUMES the number is in radix form NOT INTERNAL BASEX!!!</summary>
    public static void TRIMNUM(Number x, int precision)
    {
        if (!g_ftrueinfinite)
        {
            int trim = x.Cdigit - precision - g_ratio;
            if (trim > 1)
            {
                Array.Copy(x.Mant, trim, x.Mant, 0, x.Cdigit - trim);
                x.Cdigit -= trim;
                x.Exp += trim;
            }
        }
    }

    /// <summary>TRIMTOP: ASSUMES the number is in INTERNAL BASEX!!!</summary>
    public static void TRIMTOP(Rat x, int precision)
    {
        if (!g_ftrueinfinite)
        {
            int trim = x.Pp.Cdigit - (precision / g_ratio) - 2;
            if (trim > 1)
            {
                Array.Copy(x.Pp.Mant, trim, x.Pp.Mant, 0, x.Pp.Cdigit - trim);
                x.Pp.Cdigit -= trim;
                x.Pp.Exp += trim;
            }

            trim = Math.Min(x.Pp.Exp, x.Pq.Exp);
            x.Pp.Exp -= trim;
            x.Pq.Exp -= trim;
        }
    }

    public static bool SMALL_ENOUGH_RAT(Rat a, int precision) =>
        zernum(a.Pp) || ((((a.Pq.Cdigit + a.Pq.Exp) - (a.Pp.Cdigit + a.Pp.Exp) - 1) * g_ratio) > precision);

    /// <summary>INC(a): the number equivalent of a++ (a is a BASEX number).</summary>
    public static void INC(ref Number a)
    {
        if (a.Mant[0] < BASEX - 1)
        {
            a.Mant[0]++;
        }
        else
        {
            addnum(ref a, num_one, BASEX);
        }
    }

    // ---------------------------------------------------------------------
    // ChangeConstants
    // ---------------------------------------------------------------------

    private static double Log2Exact(uint radix)
    {
        double l = Math.Log2(radix);
        double r = Math.Round(l);
        return Math.Abs(l - r) < 1e-12 ? r : l;
    }

    /// <summary>Call whenever either radix or precision changes, is smarter about recalculating constants.</summary>
    public static void ChangeConstants(uint radix, int precision)
    {
        // ratio is set to the number of digits in the current radix, you can get in the internal BASEX radix,
        // this is important for length calculations in translating from radix to BASEX and back.
        g_ratio = (int)Math.Ceiling(BASEXPWR / Log2Exact(radix)) - 1;

        rat_nRadix = i32torat((int)radix);

        // Check to see what we have to recalculate and what we don't
        if (cbitsofprecision < (g_ratio * (int)radix * precision))
        {
            g_ftrueinfinite = false;

            num_one ??= i32tonum(1, BASEX);
            num_two ??= i32tonum(2, BASEX);
            num_five ??= i32tonum(5, BASEX);
            num_six ??= i32tonum(6, BASEX);
            num_ten ??= i32tonum(10, BASEX);
            rat_six ??= i32torat(6);
            rat_two ??= i32torat(2);
            rat_zero ??= i32torat(0);
            rat_one ??= i32torat(1);
            rat_neg_one ??= i32torat(-1);
            rat_ten ??= i32torat(10);
            rat_word ??= i32torat(0xffff);
            rat_word ??= i32torat(0xff);
            rat_400 ??= i32torat(400);
            rat_360 ??= i32torat(360);
            rat_200 ??= i32torat(200);
            rat_180 ??= i32torat(180);
            rat_max_exp ??= i32torat(100000);

            // 3248, is the max number for which calc is able to compute factorial, after that it is unable to compute due to overflow.
            // Hence restricted factorial range as at most 3248.Beyond that calc will throw overflow error immediately.
            rat_max_fact ??= i32torat(3249);

            // -1000, is the min number for which calc is able to compute factorial, after that it takes too long to compute.
            rat_min_fact ??= i32torat(-1000);

            rat_smallest = DupRat(rat_nRadix);
            ratpowi32(ref rat_smallest, -precision, precision);
            rat_negsmallest = DupRat(rat_smallest);
            rat_negsmallest.Pp.Sign = -1;

            if (rat_half == null)
            {
                rat_half = new Rat { Pp = DupNum(num_one), Pq = DupNum(num_two) };
            }

            if (pt_eight_five == null)
            {
                pt_eight_five = new Rat { Pp = i32tonum(85, BASEX), Pq = i32tonum(100, BASEX) };
            }

            rat_qword = DupRat(rat_two);
            numpowi32(ref rat_qword.Pp, 64, BASEX, precision);
            _subrat(ref rat_qword, rat_one, precision);

            rat_dword = DupRat(rat_two);
            numpowi32(ref rat_dword.Pp, 32, BASEX, precision);
            _subrat(ref rat_dword, rat_one, precision);

            rat_max_i32 = DupRat(rat_two);
            numpowi32(ref rat_max_i32.Pp, 31, BASEX, precision);
            rat_min_i32 = DupRat(rat_max_i32);
            _subrat(ref rat_max_i32, rat_one, precision); // rat_max_i32 = 2^31 -1

            rat_min_i32.Pp.Sign *= -1; // rat_min_i32 = -2^31

            rat_min_exp = DupRat(rat_max_exp);
            rat_min_exp.Pp.Sign *= -1;

            cbitsofprecision = g_ratio * (int)radix * precision;

            // Apparently when dividing 180 by pi, another (internal) digit of precision is needed.
            int extraPrecision = precision + g_ratio;
            pi = DupRat(rat_half);
            asinrat(ref pi, radix, extraPrecision);
            mulrat(ref pi, rat_six, extraPrecision);

            two_pi = DupRat(pi);
            pi_over_two = DupRat(pi);
            one_pt_five_pi = DupRat(pi);
            _addrat(ref two_pi, pi, extraPrecision);

            divrat(ref pi_over_two, rat_two, extraPrecision);

            _addrat(ref one_pt_five_pi, pi_over_two, extraPrecision);

            e_to_one_half = DupRat(rat_half);
            _exprat(ref e_to_one_half, extraPrecision);

            rat_exp = DupRat(rat_one);
            _exprat(ref rat_exp, extraPrecision);

            // WARNING: remember _lograt uses exponent constants calculated above...

            ln_ten = DupRat(rat_ten);
            _lograt(ref ln_ten, extraPrecision);

            ln_two = DupRat(rat_two);
            _lograt(ref ln_two, extraPrecision);

            rad_to_deg = i32torat(180);
            divrat(ref rad_to_deg, pi, extraPrecision);

            rad_to_grad = i32torat(200);
            divrat(ref rad_to_grad, pi, extraPrecision);
        }
        else
        {
            _readconstants();

            rat_smallest = DupRat(rat_nRadix);
            ratpowi32(ref rat_smallest, -precision, precision);
            rat_negsmallest = DupRat(rat_smallest);
            rat_negsmallest.Pp.Sign = -1;
        }
    }

    // ---------------------------------------------------------------------
    // intrat
    // ---------------------------------------------------------------------

    public static void intrat(ref Rat px, uint radix, int precision)
    {
        // Only do the intrat operation if number is nonzero and only if the bottom part is not one.
        if (!zernum(px.Pp) && !equnum(px.Pq, num_one))
        {
            flatrat(ref px, radix, precision);

            // Subtract the fractional part of the rational
            Rat pret = DupRat(px);
            remrat(ref pret, rat_one);

            // Flatten pret in case it's not aligned with px after remrat operation
            if (!equnum(px.Pq, pret.Pq))
            {
                flatrat(ref pret, radix, precision);
            }

            _subrat(ref px, pret, precision);

            // Simplify the value if possible to resolve rounding errors
            flatrat(ref px, radix, precision);
        }
    }

    // ---------------------------------------------------------------------
    // rational comparisons
    // ---------------------------------------------------------------------

    public static bool rat_equ(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        rattmp.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        return zernum(rattmp.Pp);
    }

    public static bool rat_ge(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        b.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        b.Pp.Sign *= -1;
        return zernum(rattmp.Pp) || SIGN(rattmp) == 1;
    }

    public static bool rat_gt(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        b.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        b.Pp.Sign *= -1;
        return !zernum(rattmp.Pp) && SIGN(rattmp) == 1;
    }

    public static bool rat_le(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        b.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        b.Pp.Sign *= -1;
        return zernum(rattmp.Pp) || SIGN(rattmp) == -1;
    }

    public static bool rat_lt(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        b.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        b.Pp.Sign *= -1;
        return !zernum(rattmp.Pp) && SIGN(rattmp) == -1;
    }

    public static bool rat_neq(Rat a, Rat b, int precision)
    {
        Rat rattmp = DupRat(a);
        rattmp.Pp.Sign *= -1;
        _addrat(ref rattmp, b, precision);
        return !zernum(rattmp.Pp);
    }

    // ---------------------------------------------------------------------
    // scale, scale2pi, inbetween
    // ---------------------------------------------------------------------

    public static void scale(ref Rat px, Rat scalefact, uint radix, int precision)
    {
        Rat pret = DupRat(px);

        // Logscale is a quick way to tell how much extra precision is needed for scaling by scalefact.
        int logscale = g_ratio * ((pret.Pp.Cdigit + pret.Pp.Exp) - (pret.Pq.Cdigit + pret.Pq.Exp));
        if (logscale > 0)
        {
            precision += logscale;
        }

        divrat(ref pret, scalefact, precision);
        intrat(ref pret, radix, precision);
        mulrat(ref pret, scalefact, precision);
        pret.Pp.Sign *= -1;
        _addrat(ref px, pret, precision);
    }

    public static void scale2pi(ref Rat px, uint radix, int precision)
    {
        Rat pret = DupRat(px);
        Rat my_two_pi;

        // Logscale is a quick way to tell how much extra precision is needed for scaling by 2 pi.
        int logscale = g_ratio * ((pret.Pp.Cdigit + pret.Pp.Exp) - (pret.Pq.Cdigit + pret.Pq.Exp));
        if (logscale > 0)
        {
            precision += logscale;
            my_two_pi = DupRat(rat_half);
            asinrat(ref my_two_pi, radix, precision);
            mulrat(ref my_two_pi, rat_six, precision);
            mulrat(ref my_two_pi, rat_two, precision);
        }
        else
        {
            my_two_pi = DupRat(two_pi);
        }

        divrat(ref pret, my_two_pi, precision);
        intrat(ref pret, radix, precision);
        mulrat(ref pret, my_two_pi, precision);
        pret.Pp.Sign *= -1;
        _addrat(ref px, pret, precision);
    }

    /// <summary>Changes px to -/+range, if px is outside -range..+range.</summary>
    public static void inbetween(ref Rat px, Rat range, int precision)
    {
        if (rat_gt(px, range, precision))
        {
            px = DupRat(range);
        }
        else
        {
            range.Pp.Sign *= -1;
            if (rat_lt(px, range, precision))
            {
                px = DupRat(range);
            }

            range.Pp.Sign *= -1;
        }
    }

    // ---------------------------------------------------------------------
    // trimit
    // ---------------------------------------------------------------------

    /// <summary>
    /// Chops off digits from rational numbers to avoid time explosions in calculations of functions using series.
    /// </summary>
    public static void trimit(ref Rat px, int precision)
    {
        if (!g_ftrueinfinite)
        {
            Number pp = px.Pp;
            Number pq = px.Pq;
            int trim = g_ratio * (Math.Min(pp.Cdigit + pp.Exp, pq.Cdigit + pq.Exp) - 1) - precision;
            if (trim > g_ratio)
            {
                trim /= g_ratio;

                if (trim <= pp.Exp)
                {
                    pp.Exp -= trim;
                }
                else
                {
                    Array.Copy(pp.Mant, trim - pp.Exp, pp.Mant, 0, pp.Cdigit - trim + pp.Exp);
                    pp.Cdigit -= trim - pp.Exp;
                    pp.Exp = 0;
                }

                if (trim <= pq.Exp)
                {
                    pq.Exp -= trim;
                }
                else
                {
                    Array.Copy(pq.Mant, trim - pq.Exp, pq.Mant, 0, pq.Cdigit - trim + pq.Exp);
                    pq.Cdigit -= trim - pq.Exp;
                    pq.Exp = 0;
                }
            }

            trim = Math.Min(pp.Exp, pq.Exp);
            pp.Exp -= trim;
            pq.Exp -= trim;
        }
    }
}
