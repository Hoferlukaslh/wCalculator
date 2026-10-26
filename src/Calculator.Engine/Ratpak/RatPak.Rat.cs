// Port of ratpak rat.cpp, logic.cpp and fact.cpp (rational arithmetic, boolean operations, factorial).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    // ---------------------------------------------------------------------
    // rat.cpp
    // ---------------------------------------------------------------------

    public static void gcdrat(ref Rat pa, int precision)
    {
        Rat a = pa;
        Number pgcd = gcd(a.Pp, a.Pq);

        if (!zernum(pgcd))
        {
            divnumx(ref a.Pp, pgcd, precision);
            divnumx(ref a.Pq, pgcd, precision);
        }

        pa = a;

        RENORMALIZE(pa);
    }

    public static void fracrat(ref Rat pa, uint radix, int precision)
    {
        // Only do the flatrat operation if number is nonzero and only if the bottom part is not one.
        if (!zernum(pa.Pp) && !equnum(pa.Pq, num_one))
        {
            flatrat(ref pa, radix, precision);
        }

        remnum(ref pa.Pp, pa.Pq, BASEX);

        // Get pa back in the integer over integer form.
        RENORMALIZE(pa);
    }

    public static void mulrat(ref Rat pa, Rat b, int precision)
    {
        // Only do the multiply if it isn't zero.
        if (!zernum(pa.Pp))
        {
            mulnumx(ref pa.Pp, b.Pp);
            mulnumx(ref pa.Pq, b.Pq);
            trimit(ref pa, precision);
        }
        else
        {
            // If it is zero, blast a one in the denominator.
            pa.Pq = DupNum(num_one);
        }
    }

    public static void divrat(ref Rat pa, Rat b, int precision)
    {
        if (!zernum(pa.Pp))
        {
            // Only do the divide if the top isn't zero.
            mulnumx(ref pa.Pp, b.Pq);
            mulnumx(ref pa.Pq, b.Pp);

            if (zernum(pa.Pq))
            {
                // raise an exception if the bottom is 0.
                throw new CalcException(CalcErr.DivideByZero);
            }

            trimit(ref pa, precision);
        }
        else
        {
            // Top is zero.
            if (zerrat(b))
            {
                // If bottom is zero
                // 0 / 0 is indefinite, raise an exception.
                throw new CalcException(CalcErr.Indefinite);
            }
            else
            {
                // 0/x make a unique 0.
                pa.Pq = DupNum(num_one);
            }
        }
    }

    /// <summary>pa -= b with snapping to zero. All ratpak internals should use _subrat by default.</summary>
    public static void subrat(ref Rat pa, Rat b, int precision)
    {
        Rat a = DupRat(pa);

        _subrat(ref pa, b, precision);

        _snaprat(ref pa, a, b, precision);
    }

    public static void _subrat(ref Rat pa, Rat b, int precision)
    {
        b.Pp.Sign *= -1;
        _addrat(ref pa, b, precision);
        b.Pp.Sign *= -1;
    }

    /// <summary>pa += b with snapping to zero. All ratpak internals should use _addrat by default.</summary>
    public static void addrat(ref Rat pa, Rat b, int precision)
    {
        Rat a = DupRat(pa);

        _addrat(ref pa, b, precision);

        _snaprat(ref pa, a, b, precision);
    }

    public static void _addrat(ref Rat pa, Rat b, int precision)
    {
        if (equnum(pa.Pq, b.Pq))
        {
            // Very special case, q's match.,
            // make sure signs are involved in the calculation
            // we have to do this since the optimization here is only
            // working with the top half of the rationals.
            pa.Pp.Sign *= pa.Pq.Sign;
            pa.Pq.Sign = 1;
            b.Pp.Sign *= b.Pq.Sign;
            b.Pq.Sign = 1;
            addnum(ref pa.Pp, b.Pp, BASEX);
        }
        else
        {
            // Usual case q's aren't the same.
            Number bot = DupNum(pa.Pq);
            mulnumx(ref bot, b.Pq);
            mulnumx(ref pa.Pp, b.Pq);
            mulnumx(ref pa.Pq, b.Pp);
            addnum(ref pa.Pp, pa.Pq, BASEX);
            pa.Pq = bot;
            trimit(ref pa, precision);

            // Get rid of negative zeros here.
            pa.Pp.Sign *= pa.Pq.Sign;
            pa.Pq.Sign = 1;
        }
    }

    /// <summary>bth root of a in rat form. This is now a stub function to powrat().</summary>
    public static void rootrat(ref Rat py, Rat n, uint radix, int precision)
    {
        // Initialize 1/n
        Rat oneovern = DupRat(rat_one);
        divrat(ref oneovern, n, precision);

        powrat(ref py, oneovern, radix, precision);
    }

    public static bool zerrat(Rat a) => zernum(a.Pp);

    /// <summary>
    /// If |pr| is magnitude smaller than |a| or |b| beyond precision, snap pr to 0.
    /// b is optional and can be null for unary operations.
    /// </summary>
    public static void _snaprat(ref Rat pr, Rat a, Rat? b, int precision)
    {
        Rat threshold;
        if (b == null)
        {
            threshold = DupRat(a);
            ABSRAT(threshold);
        }
        else
        {
            Rat absA = DupRat(a);
            Rat absB = DupRat(b);
            ABSRAT(absA);
            ABSRAT(absB);

            if (rat_lt(absA, absB, precision))
            {
                threshold = DupRat(absB);
            }
            else
            {
                threshold = DupRat(absA);
            }
        }

        mulrat(ref threshold, rat_smallest, precision);

        Rat absR = DupRat(pr);
        ABSRAT(absR);

        // if absResult < threshold => snap to zero
        if (rat_lt(absR, threshold, precision))
        {
            pr = DupRat(rat_zero);
        }
    }

    // ---------------------------------------------------------------------
    // logic.cpp
    // ---------------------------------------------------------------------

    public static void lshrat(ref Rat pa, Rat b, uint radix, int precision)
    {
        intrat(ref pa, radix, precision);
        if (!zernum(pa.Pp))
        {
            // If input is zero we're done.
            if (rat_gt(b, rat_max_exp, precision))
            {
                // Don't attempt lsh of anything big
                throw new CalcException(CalcErr.Domain);
            }

            int intb = rattoi32(b, radix, precision);
            Rat pwr = DupRat(rat_two);
            ratpowi32(ref pwr, intb, precision);
            mulrat(ref pa, pwr, precision);
        }
    }

    public static void rshrat(ref Rat pa, Rat b, uint radix, int precision)
    {
        intrat(ref pa, radix, precision);
        if (!zernum(pa.Pp))
        {
            // If input is zero we're done.
            if (rat_lt(b, rat_min_exp, precision))
            {
                // Don't attempt rsh of anything big and negative.
                throw new CalcException(CalcErr.Domain);
            }

            int intb = rattoi32(b, radix, precision);
            Rat pwr = DupRat(rat_two);
            ratpowi32(ref pwr, intb, precision);
            divrat(ref pa, pwr, precision);
        }
    }

    private const int FUNC_AND = 0;
    private const int FUNC_OR = 1;
    private const int FUNC_XOR = 2;

    public static void andrat(ref Rat pa, Rat b, uint radix, int precision) => boolrat(ref pa, b, FUNC_AND, radix, precision);

    public static void orrat(ref Rat pa, Rat b, uint radix, int precision) => boolrat(ref pa, b, FUNC_OR, radix, precision);

    public static void xorrat(ref Rat pa, Rat b, uint radix, int precision) => boolrat(ref pa, b, FUNC_XOR, radix, precision);

    private static void boolrat(ref Rat pa, Rat b, int func, uint radix, int precision)
    {
        intrat(ref pa, radix, precision);
        Rat tmp = DupRat(b);
        intrat(ref tmp, radix, precision);

        boolnum(ref pa.Pp, tmp.Pp, func);
    }

    /// <summary>radix doesn't matter for logicals. WARNING: Assumes numbers are unsigned.</summary>
    private static void boolnum(ref Number pa, Number b, int func)
    {
        Number a = pa;
        int cdigits = Math.Max(a.Cdigit + a.Exp, b.Cdigit + b.Exp) - Math.Min(a.Exp, b.Exp);
        Number c = createnum(cdigits);
        c.Exp = Math.Min(a.Exp, b.Exp);
        int mexp = c.Exp;
        c.Cdigit = cdigits;
        int pcha = 0, pchb = 0, pchc = 0;
        for (; cdigits > 0; cdigits--, mexp++)
        {
            uint da = ((mexp >= a.Exp) && (cdigits + a.Exp - c.Exp > (c.Cdigit - a.Cdigit))) ? a.Mant[pcha++] : 0u;
            uint db = ((mexp >= b.Exp) && (cdigits + b.Exp - c.Exp > (c.Cdigit - b.Cdigit))) ? b.Mant[pchb++] : 0u;
            switch (func)
            {
                case FUNC_AND:
                    c.Mant[pchc++] = da & db;
                    break;
                case FUNC_OR:
                    c.Mant[pchc++] = da | db;
                    break;
                case FUNC_XOR:
                    c.Mant[pchc++] = da ^ db;
                    break;
            }
        }

        c.Sign = a.Sign;
        while (c.Cdigit > 1 && c.Mant[--pchc] == 0)
        {
            c.Cdigit--;
        }

        pa = c;
    }

    /// <summary>Remainder of pa / b, equivalent of 'pa % b' in C/C++ (same sign as the dividend).</summary>
    public static void remrat(ref Rat pa, Rat b)
    {
        if (zerrat(b))
        {
            throw new CalcException(CalcErr.Indefinite);
        }

        Rat tmp = DupRat(b);

        mulnumx(ref pa.Pp, tmp.Pq);
        mulnumx(ref tmp.Pp, pa.Pq);
        remnum(ref pa.Pp, tmp.Pp, BASEX);
        mulnumx(ref pa.Pq, tmp.Pq);

        // Get pa back in the integer over integer form.
        RENORMALIZE(pa);
    }

    /// <summary>Remainder of pa / b, with the sign of the result either zero or the same as the divisor.</summary>
    public static void modrat(ref Rat pa, Rat b)
    {
        // contrary to remrat(X, 0) returning 0, modrat(X, 0) must return X
        if (zerrat(b))
        {
            return;
        }

        Rat tmp = DupRat(b);

        bool needAdjust = SIGN(pa) == -1 ? (SIGN(b) == 1) : (SIGN(b) == -1);

        mulnumx(ref pa.Pp, tmp.Pq);
        mulnumx(ref tmp.Pp, pa.Pq);
        remnum(ref pa.Pp, tmp.Pp, BASEX);
        mulnumx(ref pa.Pq, tmp.Pq);

        if (needAdjust && !zerrat(pa))
        {
            _addrat(ref pa, b, unchecked((int)BASEX));
        }

        // Get pa back in the integer over integer form.
        RENORMALIZE(pa);
    }

    // ---------------------------------------------------------------------
    // fact.cpp
    // ---------------------------------------------------------------------

    private static void NEGATE(Rat x) => x.Pp.Sign *= -1;

    private static void _gamma(ref Rat pn, uint radix, int precision)
    {
        Rat factorial;
        Number count;
        Rat tmp;
        Rat one_pt_five;
        Rat a;
        Rat a2;
        Rat term;
        Rat sum;
        Rat err;
        Rat mpy;

        // Set up constants and initial conditions
        Rat ratprec = i32torat(precision);

        // Find the best 'A' for convergence to the required precision.
        a = i32torat((int)radix);
        _lograt(ref a, precision);
        mulrat(ref a, ratprec, precision);

        // Really is -ln(n)+1, but -ln(n) will be < 1 if we scale n between 0.5 and 1.5
        _addrat(ref a, rat_two, precision);
        tmp = DupRat(a);
        _lograt(ref tmp, precision);
        mulrat(ref tmp, pn, precision);
        _addrat(ref a, tmp, precision);
        _addrat(ref a, rat_one, precision);

        // Calculate the necessary bump in precision and up the precision.
        // The following code is equivalent to
        // precision += ln(exp(a)*pow(a,n+1.5))-ln(radix));
        tmp = DupRat(pn);
        one_pt_five = i32torat(3);
        divrat(ref one_pt_five, rat_two, precision);
        _addrat(ref tmp, one_pt_five, precision);
        term = DupRat(a);
        powratcomp(ref term, tmp, radix, precision);
        tmp = DupRat(a);
        exprat(ref tmp, radix, precision);
        mulrat(ref term, tmp, precision);
        _lograt(ref term, precision);
        Rat ratRadix = i32torat((int)radix);
        tmp = DupRat(ratRadix);
        _lograt(ref tmp, precision);
        _subrat(ref term, tmp, precision);
        precision += rattoi32(term, radix, precision);

        // Set up initial terms for series, refer to series in above comment block.
        factorial = DupRat(rat_one); // Start factorial out with one
        count = i32tonum(0, BASEX);

        mpy = DupRat(a);
        powratcomp(ref mpy, pn, radix, precision);
        // a2=a^2
        a2 = DupRat(a);
        mulrat(ref a2, a, precision);

        // sum=(1/n)-(a/(n+1))
        sum = DupRat(rat_one);
        divrat(ref sum, pn, precision);
        tmp = DupRat(pn);
        _addrat(ref tmp, rat_one, precision);
        term = DupRat(a);
        divrat(ref term, tmp, precision);
        _subrat(ref sum, term, precision);

        err = DupRat(ratRadix);
        NEGATE(ratprec);
        powratcomp(ref err, ratprec, radix, precision);
        divrat(ref err, ratRadix, precision);

        // Just get something not tiny in term
        term = DupRat(rat_two);

        // Loop until precision is reached, or asked to halt.
        while (!zerrat(term) && rat_gt(term, err, precision))
        {
            _addrat(ref pn, rat_two, precision);

            // WARNING: mixing numbers and rationals here.
            // for speed and efficiency.
            INC(ref count);
            mulnumx(ref factorial.Pp, count);
            INC(ref count);
            mulnumx(ref factorial.Pp, count);

            divrat(ref factorial, a2, precision);

            tmp = DupRat(pn);
            _addrat(ref tmp, rat_one, precision);
            term = new Rat { Pp = DupNum(count), Pq = DupNum(num_one) };
            _addrat(ref term, rat_one, precision);
            mulrat(ref term, tmp, precision);
            tmp = DupRat(a);
            divrat(ref tmp, term, precision);

            term = DupRat(rat_one);
            divrat(ref term, pn, precision);
            _subrat(ref term, tmp, precision);

            divrat(ref term, factorial, precision);
            _addrat(ref sum, term, precision);
            ABSRAT(term);
        }

        // Multiply by factor.
        mulrat(ref sum, mpy, precision);

        pn = DupRat(sum);
    }

    public static void factrat(ref Rat px, uint radix, int precision)
    {
        if (rat_gt(px, rat_max_fact, precision) || rat_lt(px, rat_min_fact, precision))
        {
            // Don't attempt factorial of anything too large or small.
            throw new CalcException(CalcErr.Overflow);
        }

        Rat fact = DupRat(rat_one);

        Rat neg_rat_one = DupRat(rat_one);
        neg_rat_one.Pp.Sign *= -1;

        Rat frac = DupRat(px);
        fracrat(ref frac, radix, precision);

        // Check for negative integers and throw an error.
        if ((zerrat(frac) || (LOGRATRADIX(frac) <= -precision)) && (SIGN(px) == -1))
        {
            throw new CalcException(CalcErr.Domain);
        }

        while (rat_gt(px, rat_zero, precision) && (LOGRATRADIX(px) > -precision))
        {
            mulrat(ref fact, px, precision);
            _subrat(ref px, rat_one, precision);
        }

        // Added to make numbers 'close enough' to integers use integer factorial.
        if (LOGRATRADIX(px) <= -precision)
        {
            px = DupRat(rat_zero);
            intrat(ref fact, radix, precision);
        }

        while (rat_lt(px, neg_rat_one, precision))
        {
            _addrat(ref px, rat_one, precision);
            divrat(ref fact, px, precision);
        }

        if (rat_neq(px, rat_zero, precision))
        {
            _addrat(ref px, rat_one, precision);
            _gamma(ref px, radix, precision);
            mulrat(ref px, fact, precision);
        }
        else
        {
            px = DupRat(fact);
        }
    }
}
