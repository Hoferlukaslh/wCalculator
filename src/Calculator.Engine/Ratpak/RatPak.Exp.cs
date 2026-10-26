// Port of ratpak exp.cpp (exp, log and power functions for rationals).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    /// <summary>exp of x using the Taylor series. This should not be called explicitly.</summary>
    public static void _exprat(ref Rat px, int precision)
    {
        // CREATETAYLOR
        Rat xx = DupRat(px);
        mulrat(ref xx, px, precision);
        Rat pret = new Rat { Pp = i32tonum(0, BASEX), Pq = i32tonum(0, BASEX) };

        addnum(ref pret.Pp, num_one, BASEX);
        addnum(ref pret.Pq, num_one, BASEX);
        Rat thisterm = DupRat(pret);

        Number n2 = i32tonum(0, BASEX);

        do
        {
            // NEXTTERM(*px, INC(n2) DIVNUM(n2), precision)
            mulrat(ref thisterm, px, precision);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        // DESTROYTAYLOR
        trimit(ref pret, precision);
        px = pret;
    }

    public static void exprat(ref Rat px, uint radix, int precision)
    {
        if (rat_gt(px, rat_max_exp, precision) || rat_lt(px, rat_min_exp, precision))
        {
            // Don't attempt exp of anything large.
            throw new CalcException(CalcErr.Domain);
        }

        Rat pwr = DupRat(rat_exp);
        Rat pint = DupRat(px);

        intrat(ref pint, radix, precision);

        int intpwr = rattoi32(pint, radix, precision);
        ratpowi32(ref pwr, intpwr, precision);

        _subrat(ref px, pint, precision);

        // It just so happens to be an integral power of e.
        if (rat_gt(px, rat_negsmallest, precision) && rat_lt(px, rat_smallest, precision))
        {
            px = DupRat(pwr);
        }
        else
        {
            _exprat(ref px, precision);
            mulrat(ref px, pwr, precision);
        }
    }

    /// <summary>log of x using the Taylor series. Private implementation of _lograt, should not be used.</summary>
    private static void __lograt(ref Rat px, int precision)
    {
        // CREATETAYLOR
        Rat xx = DupRat(px);
        mulrat(ref xx, px, precision);
        Rat pret = new Rat { Pp = i32tonum(0, BASEX), Pq = i32tonum(0, BASEX) };

        // sub one from x
        px.Pq.Sign *= -1;
        addnum(ref px.Pp, px.Pq, BASEX);
        px.Pq.Sign *= -1;

        pret = DupRat(px);
        Rat thisterm = DupRat(px);

        Number n2 = i32tonum(1, BASEX);
        px.Pp.Sign *= -1;

        do
        {
            // NEXTTERM(*px, MULNUM(n2) INC(n2) DIVNUM(n2), precision)
            mulrat(ref thisterm, px, precision);
            mulnumx(ref thisterm.Pp, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);

            TRIMTOP(px, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        // DESTROYTAYLOR
        trimit(ref pret, precision);
        px = pret;
    }

    public static void _lograt(ref Rat px, int precision)
    {
        Rat pwr;    // pwr is the large scaling factor.
        Rat offset; // offset is the incremental scaling factor.

        // Check for someone taking the log of zero or a negative number.
        if (rat_le(px, rat_zero, precision))
        {
            throw new CalcException(CalcErr.Domain);
        }

        // Get number > 1, for scaling
        bool fneglog = rat_lt(px, rat_one, precision);
        if (fneglog)
        {
            (px.Pp, px.Pq) = (px.Pq, px.Pp);
        }

        // Scale the number within BASEX factor of 1, for the large scale.
        // log(x*2^(BASEXPWR*k)) = BASEXPWR*k*log(2)+log(x)
        if (LOGRAT2(px) > 1)
        {
            int intpwr = LOGRAT2(px) - 1;
            px.Pq.Exp += intpwr;
            pwr = i32torat(intpwr * (int)BASEXPWR);
            mulrat(ref pwr, ln_two, precision);
            // ln(x+e)-ln(x) looks close to e when x is close to one using some
            // expansions.  This means we can trim past precision digits+1.
            TRIMTOP(px, precision);
        }
        else
        {
            pwr = DupRat(rat_zero);
        }

        offset = DupRat(rat_zero);
        // Scale the number between 1 and e_to_one_half, for the small scale.
        while (rat_gt(px, e_to_one_half, precision))
        {
            divrat(ref px, e_to_one_half, precision);
            _addrat(ref offset, rat_one, precision);
        }

        __lograt(ref px, precision);

        // Add the large and small scaling factors, take into account
        // small scaling was done in e_to_one_half chunks.
        divrat(ref offset, rat_two, precision);
        _addrat(ref pwr, offset, precision);

        // And add the resulting scaling factor to the answer.
        _addrat(ref px, pwr, precision);

        trimit(ref px, precision);

        // If number started out < 1 rescale answer to negative.
        if (fneglog)
        {
            px.Pp.Sign *= -1;
        }
    }

    /// <summary>Natural log of x. Tries to snap to zero. Use _lograt inside ratpak by default.</summary>
    public static void lograt(ref Rat px, int precision)
    {
        Rat a = DupRat(px);

        _lograt(ref px, precision);

        _snaprat(ref px, a, null, precision);
    }

    public static void log10rat(ref Rat px, int precision)
    {
        lograt(ref px, precision);
        divrat(ref px, ln_ten, precision);
    }

    /// <summary>
    /// Returns if the given x is even number. The assumption here is its denominator is 1 and we are testing the
    /// numerator is even or not.
    /// </summary>
    private static bool IsEven(Rat x, uint radix, int precision)
    {
        Rat tmp = DupRat(x);
        divrat(ref tmp, rat_two, precision);
        fracrat(ref tmp, radix, precision);
        _addrat(ref tmp, tmp, precision);
        _subrat(ref tmp, rat_one, precision);
        return rat_lt(tmp, rat_zero, precision);
    }

    /// <summary>Sets px to px to the y. Handles special cases where px is a perfect root.</summary>
    public static void powrat(ref Rat px, Rat y, uint radix, int precision)
    {
        // Handle cases where px or y is 0 by calling powratcomp directly
        if (zerrat(px) || zerrat(y))
        {
            powratcomp(ref px, y, radix, precision);
            return;
        }

        // When y is 1, return px
        if (rat_equ(y, rat_one, precision))
        {
            return;
        }

        try
        {
            powratNumeratorDenominator(ref px, y, radix, precision);
        }
        catch (CalcException)
        {
            // If calculating the power using numerator/denominator failed, fall back to the less
            // accurate method of passing in the original y
            powratcomp(ref px, y, radix, precision);
        }
    }

    public static void powratNumeratorDenominator(ref Rat px, Rat y, uint radix, int precision)
    {
        // Prepare rationals
        Rat yNumerator = DupRat(rat_zero);   // yNumerator->pq is 1 one
        Rat yDenominator = DupRat(rat_zero); // yDenominator->pq is 1 one
        yNumerator.Pp = DupNum(y.Pp);
        yDenominator.Pp = DupNum(y.Pq);

        // Calculate the following use the Powers of Powers rule:
        // px ^ (yNum/yDenom) == px ^ yNum ^ (1/yDenom)
        // 1. For px ^ yNum, we call powratcomp directly which will call ratpowi32
        //    and store the result in pxPowNum
        // 2. For pxPowNum ^ (1/yDenom), we call powratcomp
        // 3. Validate the result of 2 by adding/subtracting 0.5, flooring and call powratcomp with yDenom
        //    on the floored result.

        // 1. Initialize result.
        Rat pxPow = DupRat(px);

        // 2. Calculate pxPow = px ^ yNumerator
        // if yNumerator is not 1
        if (!rat_equ(yNumerator, rat_one, precision))
        {
            powratcomp(ref pxPow, yNumerator, radix, precision);
        }

        // 2. Calculate pxPowNumDenom = pxPowNum ^ (1/yDenominator),
        // if yDenominator is not 1
        if (!rat_equ(yDenominator, rat_one, precision))
        {
            // Calculate 1 over y
            Rat oneoveryDenom = DupRat(rat_one);
            divrat(ref oneoveryDenom, yDenominator, precision);

            // Take the oneoveryDenom power
            Rat originalResult = DupRat(pxPow);
            powratcomp(ref originalResult, oneoveryDenom, radix, precision);

            // Round the originalResult to roundedResult
            Rat roundedResult = DupRat(originalResult);
            if (roundedResult.Pp.Sign == -1)
            {
                _subrat(ref roundedResult, rat_half, precision);
            }
            else
            {
                _addrat(ref roundedResult, rat_half, precision);
            }

            intrat(ref roundedResult, radix, precision);

            // Take the yDenom power of the roundedResult.
            Rat roundedPower = DupRat(roundedResult);
            powratcomp(ref roundedPower, yDenominator, radix, precision);

            // if roundedPower == px, we found an exact power in roundedResult
            if (rat_equ(roundedPower, pxPow, precision))
            {
                px = DupRat(roundedResult);
            }
            else
            {
                px = DupRat(originalResult);
            }
        }
        else
        {
            px = DupRat(pxPow);
        }
    }

    /// <summary>Sets px to px to the y using x^y=e(y*ln(x)), or a more exact calculation where y is an integer.</summary>
    public static void powratcomp(ref Rat px, Rat y, uint radix, int precision)
    {
        int sign = SIGN(px);

        // Take the absolute value
        px.Pp.Sign = 1;
        px.Pq.Sign = 1;

        if (zerrat(px))
        {
            // px is zero.
            if (rat_lt(y, rat_zero, precision))
            {
                throw new CalcException(CalcErr.Domain);
            }
            else if (zerrat(y))
            {
                // px and y are both zero, special case a 1 return.
                px = DupRat(rat_one);
                // Ensure sign is positive.
                sign = 1;
            }
        }
        else
        {
            Rat pxint = DupRat(px);
            _subrat(ref pxint, rat_one, precision);
            if (rat_gt(pxint, rat_negsmallest, precision) && rat_lt(pxint, rat_smallest, precision) && sign == 1)
            {
                // px is one, special case a 1 return.
                px = DupRat(rat_one);
                // Ensure sign is positive.
                sign = 1;
            }
            else
            {
                // Only do the exp if the number isn't zero or one
                Rat podd = DupRat(y);
                fracrat(ref podd, radix, precision);
                if (rat_gt(podd, rat_negsmallest, precision) && rat_lt(podd, rat_smallest, precision))
                {
                    // If power is an integer let ratpowi32 deal with it.
                    Rat iy = DupRat(y);
                    _subrat(ref iy, podd, precision);
                    int inty = rattoi32(iy, radix, precision);

                    Rat plnx = DupRat(px);
                    _lograt(ref plnx, precision);
                    mulrat(ref plnx, iy, precision);
                    if (rat_gt(plnx, rat_max_exp, precision) || rat_lt(plnx, rat_min_exp, precision))
                    {
                        // Don't attempt exp of anything large or small.
                        throw new CalcException(CalcErr.Domain);
                    }

                    ratpowi32(ref px, inty, precision);
                    if ((inty & 1) == 0)
                    {
                        sign = 1;
                    }
                }
                else
                {
                    // power is a fraction
                    if (sign == -1)
                    {
                        // Need to throw an error if the exponent has an even denominator.
                        // As a first step, the numerator and denominator must be divided by 2 as many times as
                        //     possible, so that 2/6 is allowed.
                        // If the final numerator is still even, the end result should be positive.
                        bool fBadExponent = false;

                        // Get the numbers in arbitrary precision rational number format
                        Rat pNumerator = DupRat(rat_zero);   // pNumerator->pq is 1 one
                        Rat pDenominator = DupRat(rat_zero); // pDenominator->pq is 1 one

                        pNumerator.Pp = DupNum(y.Pp);
                        pNumerator.Pp.Sign = 1;
                        pDenominator.Pp = DupNum(y.Pq);
                        pDenominator.Pp.Sign = 1;

                        while (IsEven(pNumerator, radix, precision) && IsEven(pDenominator, radix, precision)) // both Numerator & denominator is even
                        {
                            divrat(ref pNumerator, rat_two, precision);
                            divrat(ref pDenominator, rat_two, precision);
                        }

                        if (IsEven(pDenominator, radix, precision)) // denominator is still even
                        {
                            fBadExponent = true;
                        }

                        if (IsEven(pNumerator, radix, precision)) // numerator is still even
                        {
                            sign = 1;
                        }

                        if (fBadExponent)
                        {
                            throw new CalcException(CalcErr.Domain);
                        }
                    }
                    else
                    {
                        // If the exponent is not odd disregard the sign.
                        sign = 1;
                    }

                    _lograt(ref px, precision);
                    mulrat(ref px, y, precision);
                    exprat(ref px, radix, precision);
                }
            }
        }

        px.Pp.Sign *= sign;
    }
}
