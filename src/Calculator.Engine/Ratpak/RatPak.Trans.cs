// Port of ratpak trans.cpp, transh.cpp, itrans.cpp and itransh.cpp
// (trigonometric, hyperbolic and inverse functions for rationals).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Calculator.Engine.Ratpak;

internal static partial class RatPak
{
    // ---------------------------------------------------------------------
    // Taylor series helpers (CREATETAYLOR / NEXTTERM / DESTROYTAYLOR macros)
    // ---------------------------------------------------------------------

    private static Rat CreateTaylorXX(Rat px, int precision)
    {
        Rat xx = DupRat(px);
        mulrat(ref xx, px, precision);
        return xx;
    }

    private static Rat CreateTaylorRet() => new Rat { Pp = i32tonum(0, BASEX), Pq = i32tonum(0, BASEX) };

    private static void DestroyTaylor(ref Rat px, ref Rat pret, int precision)
    {
        trimit(ref pret, precision);
        px = pret;
    }

    // ---------------------------------------------------------------------
    // trans.cpp
    // ---------------------------------------------------------------------

    private static void scalerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        switch (angletype)
        {
            case AngleType.Radians:
                scale2pi(ref pa, radix, precision);
                break;
            case AngleType.Degrees:
                scale(ref pa, rat_360, radix, precision);
                break;
            case AngleType.Gradians:
                scale(ref pa, rat_400, radix, precision);
                break;
        }
    }

    public static void _sinrat(ref Rat px, int precision)
    {
        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();

        pret = DupRat(px);
        Rat thisterm = DupRat(px);

        Number n2 = DupNum(num_one);
        xx.Pp.Sign *= -1;

        do
        {
            mulrat(ref thisterm, xx, precision);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);

        // Since px might be epsilon above 1 or below -1, due to TRIMIT we need this trick here.
        inbetween(ref px, rat_one, precision);

        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(px, rat_smallest, precision) && rat_ge(px, rat_negsmallest, precision))
        {
            px = DupRat(rat_zero);
        }
    }

    public static void sinrat(ref Rat px, uint radix, int precision)
    {
        scale2pi(ref px, radix, precision);
        _sinrat(ref px, precision);
    }

    public static void sinanglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case AngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    _subrat(ref pa, rat_360, precision);
                }

                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case AngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    _subrat(ref pa, rat_400, precision);
                }

                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }

        _sinrat(ref pa, precision);
    }

    public static void _cosrat(ref Rat px, uint radix, int precision)
    {
        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();

        pret.Pp = i32tonum(1, radix);
        pret.Pq = i32tonum(1, radix);

        Rat thisterm = DupRat(pret);

        Number n2 = i32tonum(0, radix);
        xx.Pp.Sign *= -1;

        do
        {
            mulrat(ref thisterm, xx, precision);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);

        // Since px might be epsilon above 1 or below -1, due to TRIMIT we need this trick here.
        inbetween(ref px, rat_one, precision);
        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(px, rat_smallest, precision) && rat_ge(px, rat_negsmallest, precision))
        {
            px = DupRat(rat_zero);
        }
    }

    public static void cosrat(ref Rat px, uint radix, int precision)
    {
        scale2pi(ref px, radix, precision);
        _cosrat(ref px, radix, precision);
    }

    public static void cosanglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case AngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    Rat ptmp = DupRat(rat_360);
                    _subrat(ref ptmp, pa, precision);
                    pa = ptmp;
                }

                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case AngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    Rat ptmp = DupRat(rat_400);
                    _subrat(ref ptmp, pa, precision);
                    pa = ptmp;
                }

                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }

        _cosrat(ref pa, radix, precision);
    }

    public static void _tanrat(ref Rat px, uint radix, int precision)
    {
        Rat ptmp = DupRat(px);
        _sinrat(ref px, precision);
        _cosrat(ref ptmp, radix, precision);
        if (zerrat(ptmp))
        {
            throw new CalcException(CalcErr.Domain);
        }

        divrat(ref px, ptmp, precision);
    }

    public static void tanrat(ref Rat px, uint radix, int precision)
    {
        scale2pi(ref px, radix, precision);
        _tanrat(ref px, radix, precision);
    }

    public static void tananglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        scalerat(ref pa, angletype, radix, precision);
        switch (angletype)
        {
            case AngleType.Degrees:
                if (rat_gt(pa, rat_180, precision))
                {
                    _subrat(ref pa, rat_180, precision);
                }

                divrat(ref pa, rat_180, precision);
                mulrat(ref pa, pi, precision);
                break;
            case AngleType.Gradians:
                if (rat_gt(pa, rat_200, precision))
                {
                    _subrat(ref pa, rat_200, precision);
                }

                divrat(ref pa, rat_200, precision);
                mulrat(ref pa, pi, precision);
                break;
        }

        _tanrat(ref pa, radix, precision);
    }

    // ---------------------------------------------------------------------
    // transh.cpp: hyperbolic functions
    // ---------------------------------------------------------------------

    private static bool IsValidForHypFunc(Rat px, int precision)
    {
        Rat ptmp = DupRat(rat_min_exp);
        divrat(ref ptmp, rat_ten, precision);
        return !rat_lt(px, ptmp, precision);
    }

    private static void _sinhrat(ref Rat px, int precision)
    {
        if (!IsValidForHypFunc(px, precision))
        {
            // Don't attempt exp of anything large or small
            throw new CalcException(CalcErr.Domain);
        }

        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();

        pret = DupRat(px);
        Rat thisterm = DupRat(pret);

        Number n2 = DupNum(num_one);

        do
        {
            mulrat(ref thisterm, xx, precision);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);
    }

    public static void sinhrat(ref Rat px, uint radix, int precision)
    {
        if (rat_ge(px, rat_one, precision))
        {
            Rat tmpx = DupRat(px);
            exprat(ref px, radix, precision);
            tmpx.Pp.Sign *= -1;
            exprat(ref tmpx, radix, precision);
            _subrat(ref px, tmpx, precision);
            divrat(ref px, rat_two, precision);
        }
        else
        {
            _sinhrat(ref px, precision);
        }
    }

    private static void _coshrat(ref Rat px, uint radix, int precision)
    {
        if (!IsValidForHypFunc(px, precision))
        {
            // Don't attempt exp of anything large or small
            throw new CalcException(CalcErr.Domain);
        }

        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();

        pret.Pp = i32tonum(1, radix);
        pret.Pq = i32tonum(1, radix);

        Rat thisterm = DupRat(pret);

        Number n2 = i32tonum(0, radix);

        do
        {
            mulrat(ref thisterm, xx, precision);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);
    }

    public static void coshrat(ref Rat px, uint radix, int precision)
    {
        px.Pp.Sign = 1;
        px.Pq.Sign = 1;
        if (rat_ge(px, rat_one, precision))
        {
            Rat tmpx = DupRat(px);
            exprat(ref px, radix, precision);
            tmpx.Pp.Sign *= -1;
            exprat(ref tmpx, radix, precision);
            _addrat(ref px, tmpx, precision);
            divrat(ref px, rat_two, precision);
        }
        else
        {
            _coshrat(ref px, radix, precision);
        }

        // Since px might be epsilon below 1 due to TRIMIT we need this trick here.
        if (rat_lt(px, rat_one, precision))
        {
            px = DupRat(rat_one);
        }
    }

    public static void tanhrat(ref Rat px, uint radix, int precision)
    {
        Rat ptmp = DupRat(px);
        sinhrat(ref px, radix, precision);
        coshrat(ref ptmp, radix, precision);
        mulnumx(ref px.Pp, ptmp.Pq);
        mulnumx(ref px.Pq, ptmp.Pp);
    }

    // ---------------------------------------------------------------------
    // itrans.cpp: inverse trigonometric functions
    // ---------------------------------------------------------------------

    private static void ascalerat(ref Rat pa, AngleType angletype, int precision)
    {
        switch (angletype)
        {
            case AngleType.Radians:
                break;
            case AngleType.Degrees:
                divrat(ref pa, two_pi, precision);
                mulrat(ref pa, rat_360, precision);
                break;
            case AngleType.Gradians:
                divrat(ref pa, two_pi, precision);
                mulrat(ref pa, rat_400, precision);
                break;
        }
    }

    private static void _asinrat(ref Rat px, int precision)
    {
        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();
        pret = DupRat(px);
        Rat thisterm = DupRat(px);
        Number n2 = DupNum(num_one);

        do
        {
            mulrat(ref thisterm, xx, precision);
            mulnumx(ref thisterm.Pp, n2);
            mulnumx(ref thisterm.Pp, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);
    }

    public static void asinanglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        asinrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    public static void asinrat(ref Rat px, uint radix, int precision)
    {
        int sgn = SIGN(px);

        px.Pp.Sign = 1;
        px.Pq.Sign = 1;

        // Avoid the really bad part of the asin curve near +/-1.
        Rat phack = DupRat(px);
        _subrat(ref phack, rat_one, precision);
        // Since px might be epsilon near zero we must set it to zero.
        if (rat_le(phack, rat_smallest, precision) && rat_ge(phack, rat_negsmallest, precision))
        {
            px = DupRat(pi_over_two);
        }
        else
        {
            if (rat_gt(px, pt_eight_five, precision))
            {
                if (rat_gt(px, rat_one, precision))
                {
                    _subrat(ref px, rat_one, precision);
                    if (rat_gt(px, rat_smallest, precision))
                    {
                        throw new CalcException(CalcErr.Domain);
                    }
                    else
                    {
                        px = DupRat(rat_one);
                    }
                }

                Rat pret = DupRat(px);
                mulrat(ref px, pret, precision);
                px.Pp.Sign *= -1;
                _addrat(ref px, rat_one, precision);
                rootrat(ref px, rat_two, radix, precision);
                _asinrat(ref px, precision);
                px.Pp.Sign *= -1;
                _addrat(ref px, pi_over_two, precision);
            }
            else
            {
                _asinrat(ref px, precision);
            }
        }

        px.Pp.Sign = sgn;
        px.Pq.Sign = 1;
    }

    public static void acosanglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        acosrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    public static void acosrat(ref Rat px, uint radix, int precision)
    {
        int sgn = SIGN(px);

        px.Pp.Sign = 1;
        px.Pq.Sign = 1;

        if (rat_equ(px, rat_one, precision))
        {
            if (sgn == -1)
            {
                px = DupRat(pi);
            }
            else
            {
                px = DupRat(rat_zero);
            }
        }
        else
        {
            px.Pp.Sign = sgn;
            asinrat(ref px, radix, precision);
            px.Pp.Sign *= -1;
            _addrat(ref px, pi_over_two, precision);
        }
    }

    public static void atananglerat(ref Rat pa, AngleType angletype, uint radix, int precision)
    {
        atanrat(ref pa, radix, precision);
        ascalerat(ref pa, angletype, precision);
    }

    private static void _atanrat(ref Rat px, int precision)
    {
        Rat xx = CreateTaylorXX(px, precision);
        Rat pret = CreateTaylorRet();

        pret = DupRat(px);
        Rat thisterm = DupRat(px);

        Number n2 = DupNum(num_one);

        xx.Pp.Sign *= -1;

        do
        {
            mulrat(ref thisterm, xx, precision);
            mulnumx(ref thisterm.Pp, n2);
            INC(ref n2);
            INC(ref n2);
            mulnumx(ref thisterm.Pq, n2);
            _addrat(ref pret, thisterm, precision);
        }
        while (!SMALL_ENOUGH_RAT(thisterm, precision));

        DestroyTaylor(ref px, ref pret, precision);
    }

    public static void atanrat(ref Rat px, uint radix, int precision)
    {
        int sgn = SIGN(px);

        px.Pp.Sign = 1;
        px.Pq.Sign = 1;

        if (rat_gt(px, pt_eight_five, precision))
        {
            if (rat_gt(px, rat_two, precision))
            {
                px.Pp.Sign = sgn;
                px.Pq.Sign = 1;
                Rat tmpx = DupRat(rat_one);
                divrat(ref tmpx, px, precision);
                _atanrat(ref tmpx, precision);
                tmpx.Pp.Sign = sgn;
                tmpx.Pq.Sign = 1;
                px = DupRat(pi_over_two);
                _subrat(ref px, tmpx, precision);
            }
            else
            {
                px.Pp.Sign = sgn;
                Rat tmpx = DupRat(px);
                mulrat(ref tmpx, px, precision);
                _addrat(ref tmpx, rat_one, precision);
                rootrat(ref tmpx, rat_two, radix, precision);
                divrat(ref px, tmpx, precision);
                asinrat(ref px, radix, precision);
                px.Pp.Sign = sgn;
                px.Pq.Sign = 1;
            }
        }
        else
        {
            px.Pp.Sign = sgn;
            px.Pq.Sign = 1;
            _atanrat(ref px, precision);
        }

        if (rat_gt(px, pi_over_two, precision))
        {
            _subrat(ref px, pi, precision);
        }
    }

    // ---------------------------------------------------------------------
    // itransh.cpp: inverse hyperbolic functions
    // ---------------------------------------------------------------------

    public static void asinhrat(ref Rat px, uint radix, int precision)
    {
        Rat neg_pt_eight_five = DupRat(pt_eight_five);
        neg_pt_eight_five.Pp.Sign *= -1;
        if (rat_gt(px, pt_eight_five, precision) || rat_lt(px, neg_pt_eight_five, precision))
        {
            Rat ptmp = DupRat(px);
            mulrat(ref ptmp, px, precision);
            _addrat(ref ptmp, rat_one, precision);
            rootrat(ref ptmp, rat_two, radix, precision);
            _addrat(ref px, ptmp, precision);
            _lograt(ref px, precision);
        }
        else
        {
            Rat xx = CreateTaylorXX(px, precision);
            Rat pret = CreateTaylorRet();
            xx.Pp.Sign *= -1;

            pret = DupRat(px);
            Rat thisterm = DupRat(px);

            Number n2 = DupNum(num_one);

            do
            {
                mulrat(ref thisterm, xx, precision);
                mulnumx(ref thisterm.Pp, n2);
                mulnumx(ref thisterm.Pp, n2);
                INC(ref n2);
                mulnumx(ref thisterm.Pq, n2);
                INC(ref n2);
                mulnumx(ref thisterm.Pq, n2);
                _addrat(ref pret, thisterm, precision);
            }
            while (!SMALL_ENOUGH_RAT(thisterm, precision));

            DestroyTaylor(ref px, ref pret, precision);
        }
    }

    public static void acoshrat(ref Rat px, uint radix, int precision)
    {
        if (rat_lt(px, rat_one, precision))
        {
            throw new CalcException(CalcErr.Domain);
        }
        else
        {
            Rat ptmp = DupRat(px);
            mulrat(ref ptmp, px, precision);
            _subrat(ref ptmp, rat_one, precision);
            rootrat(ref ptmp, rat_two, radix, precision);
            _addrat(ref px, ptmp, precision);
            _lograt(ref px, precision);
        }
    }

    public static void atanhrat(ref Rat px, int precision)
    {
        Rat ptmp = DupRat(px);
        _subrat(ref ptmp, rat_one, precision);
        _addrat(ref px, rat_one, precision);
        divrat(ref px, ptmp, precision);
        px.Pp.Sign *= -1;
        _lograt(ref px, precision);
        divrat(ref px, rat_two, precision);
    }
}
