// Port of CalcManager CEngine Number / Rational / RationalMath.
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using Calculator.Engine.Ratpak;

namespace Calculator.Engine;

/// <summary>Immutable snapshot of a ratpak NUMBER (sign, exponent and mantissa digits).</summary>
public sealed class EngineNumber
{
    private readonly uint[] _mantissa;

    public EngineNumber()
        : this(1, 0, new uint[] { 0 })
    {
    }

    public EngineNumber(int sign, int exp, uint[] mantissa)
    {
        Sign = sign;
        Exp = exp;
        _mantissa = (uint[])mantissa.Clone();
    }

    internal EngineNumber(Ratpak.Number p)
    {
        Sign = p.Sign;
        Exp = p.Exp;
        _mantissa = new uint[p.Cdigit];
        Array.Copy(p.Mant, _mantissa, p.Cdigit);
    }

    public int Sign { get; }

    public int Exp { get; }

    public uint[] Mantissa => _mantissa;

    public bool IsZero
    {
        get
        {
            foreach (uint d in _mantissa)
            {
                if (d != 0)
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal Ratpak.Number ToPNUMBER()
    {
        var ret = RatPak.createnum(_mantissa.Length + 1);
        ret.Sign = Sign;
        ret.Exp = Exp;
        ret.Cdigit = _mantissa.Length;
        Array.Copy(_mantissa, ret.Mant, _mantissa.Length);
        return ret;
    }
}

/// <summary>An immutable arbitrary precision rational number (P / Q).</summary>
public sealed class Rational
{
    /// <summary>Default Base/Radix to use for Rational calculations. RatPack calculations currently support up to Base64.</summary>
    public const uint RATIONAL_BASE = 10;

    /// <summary>Default Precision to use for Rational calculations.</summary>
    public const int RATIONAL_PRECISION = 128;

    public EngineNumber P { get; }

    public EngineNumber Q { get; }

    public Rational()
    {
        P = new EngineNumber();
        Q = new EngineNumber(1, 0, new uint[] { 1 });
    }

    public Rational(EngineNumber n)
    {
        int qExp = 0;
        if (n.Exp < 0)
        {
            qExp -= n.Exp;
        }

        P = new EngineNumber(n.Sign, 0, n.Mantissa);
        Q = new EngineNumber(1, qExp, new uint[] { 1 });
    }

    public Rational(EngineNumber p, EngineNumber q)
    {
        P = p;
        Q = q;
    }

    public Rational(int i)
    {
        Rat pr = RatPak.i32torat(i);
        P = new EngineNumber(pr.Pp);
        Q = new EngineNumber(pr.Pq);
    }

    public Rational(uint ui)
    {
        Rat pr = RatPak.Ui32torat(ui);
        P = new EngineNumber(pr.Pp);
        Q = new EngineNumber(pr.Pq);
    }

    public Rational(ulong ui)
    {
        uint hi = (uint)((ui >> 32) & 0xffffffff);
        uint lo = (uint)ui;

        Rational temp = Or(Lsh(new Rational(hi), new Rational(32)), new Rational(lo));

        P = temp.P;
        Q = temp.Q;
    }

    internal Rational(Rat prat)
    {
        P = new EngineNumber(prat.Pp);
        Q = new EngineNumber(prat.Pq);
    }

    public static implicit operator Rational(int i) => new Rational(i);

    public static implicit operator Rational(uint ui) => new Rational(ui);

    public static implicit operator Rational(ulong ui) => new Rational(ui);

    internal Rat ToPRAT() => new Rat { Pp = P.ToPNUMBER(), Pq = Q.ToPNUMBER() };

    public static Rational operator -(Rational r) => new Rational(new EngineNumber(-1 * r.P.Sign, r.P.Exp, r.P.Mantissa), r.Q);

    public static Rational operator +(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.addrat(ref l, rhs.ToPRAT(), RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational operator -(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.subrat(ref l, rhs.ToPRAT(), RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational operator *(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.mulrat(ref l, rhs.ToPRAT(), RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational operator /(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.divrat(ref l, rhs.ToPRAT(), RATIONAL_PRECISION);
        return new Rational(l);
    }

    /// <summary>
    /// Calculate the remainder after division, the sign of a result will match the sign of lhs.
    /// This has the same behavior as the standard C/C++ operator '%'. To calculate the modulus use RationalMath.Mod.
    /// </summary>
    public static Rational operator %(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.remrat(ref l, rhs.ToPRAT());
        return new Rational(l);
    }

    public static Rational Lsh(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.lshrat(ref l, rhs.ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational Rsh(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.rshrat(ref l, rhs.ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational And(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.andrat(ref l, rhs.ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational Or(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.orrat(ref l, rhs.ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational Xor(Rational lhs, Rational rhs)
    {
        Rat l = lhs.ToPRAT();
        RatPak.xorrat(ref l, rhs.ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
        return new Rational(l);
    }

    public static Rational operator &(Rational lhs, Rational rhs) => And(lhs, rhs);

    public static Rational operator |(Rational lhs, Rational rhs) => Or(lhs, rhs);

    public static Rational operator ^(Rational lhs, Rational rhs) => Xor(lhs, rhs);

    public static bool operator ==(Rational? lhs, Rational? rhs)
    {
        if (ReferenceEquals(lhs, rhs))
        {
            return true;
        }

        if (lhs is null || rhs is null)
        {
            return false;
        }

        return RatPak.rat_equ(lhs.ToPRAT(), rhs.ToPRAT(), RATIONAL_PRECISION);
    }

    public static bool operator !=(Rational? lhs, Rational? rhs) => !(lhs == rhs);

    public static bool operator <(Rational lhs, Rational rhs) => RatPak.rat_lt(lhs.ToPRAT(), rhs.ToPRAT(), RATIONAL_PRECISION);

    public static bool operator >(Rational lhs, Rational rhs) => rhs < lhs;

    public static bool operator <=(Rational lhs, Rational rhs) => !(lhs > rhs);

    public static bool operator >=(Rational lhs, Rational rhs) => !(lhs < rhs);

    public override bool Equals(object? obj) => obj is Rational r && this == r;

    public override int GetHashCode() => 0;

    public string ToString(uint radix, NumberFormat fmt, int precision)
    {
        return RatPak.RatToString(ToPRAT(), fmt, radix, precision);
    }

    public ulong ToUInt64_t()
    {
        return RatPak.rattoUi64(ToPRAT(), RATIONAL_BASE, RATIONAL_PRECISION);
    }
}

/// <summary>Mathematical functions on <see cref="Rational"/> values.</summary>
public static class RationalMath
{
    private const uint BASE = Rational.RATIONAL_BASE;
    private const int PREC = Rational.RATIONAL_PRECISION;

    public static Rational Frac(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.fracrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Integer(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.intrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Pow(Rational baseValue, Rational pow)
    {
        Rat baseRat = baseValue.ToPRAT();
        RatPak.powrat(ref baseRat, pow.ToPRAT(), BASE, PREC);
        return new Rational(baseRat);
    }

    public static Rational Root(Rational baseValue, Rational root) => Pow(baseValue, Invert(root));

    public static Rational Fact(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.factrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Exp(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.exprat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Log(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.lograt(ref prat, PREC);
        return new Rational(prat);
    }

    public static Rational Log10(Rational rat) => Log(rat) / new Rational(RatPak.ln_ten);

    public static Rational Invert(Rational rat) => new Rational(1) / rat;

    public static Rational Abs(Rational rat) =>
        new Rational(new EngineNumber(1, rat.P.Exp, rat.P.Mantissa), new EngineNumber(1, rat.Q.Exp, rat.Q.Mantissa));

    public static Rational Sin(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.sinanglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Cos(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.cosanglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Tan(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.tananglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ASin(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.asinanglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ACos(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.acosanglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ATan(Rational rat, AngleType angletype)
    {
        Rat prat = rat.ToPRAT();
        RatPak.atananglerat(ref prat, angletype, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Sinh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.sinhrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Cosh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.coshrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational Tanh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.tanhrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ASinh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.asinhrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ACosh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.acoshrat(ref prat, BASE, PREC);
        return new Rational(prat);
    }

    public static Rational ATanh(Rational rat)
    {
        Rat prat = rat.ToPRAT();
        RatPak.atanhrat(ref prat, PREC);
        return new Rational(prat);
    }

    /// <summary>
    /// Calculate the modulus after division, the sign of the result will match the sign of b.
    /// When one of the operand is negative the result will differ from the C/C++ operator '%'.
    /// </summary>
    public static Rational Mod(Rational a, Rational b)
    {
        Rat prat = a.ToPRAT();
        RatPak.modrat(ref prat, b.ToPRAT());
        return new Rational(prat);
    }
}
