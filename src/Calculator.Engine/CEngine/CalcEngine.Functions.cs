// Port of CalcManager CEngine scifunc.cpp and scioper.cpp (scientific functions and operations).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using Calculator.Engine.Ratpak;
using static Calculator.Engine.CCommand;
using static Calculator.Engine.RationalMath;

namespace Calculator.Engine;

internal sealed partial class CCalcEngine
{
    private const uint SCODE_MASK = 0xFFFF;

    /// <summary>Routines for more complex mathematical functions/error checking.</summary>
    private Rational SciCalcFunctions(Rational rat, uint op)
    {
        Rational result = new Rational();
        try
        {
            switch ((int)op)
            {
                case IDC_CHOP:
                    result = _bInv ? Frac(rat) : Integer(rat);
                    break;

                /* Return complement. */
                case IDC_COM:
                    if (_radix == 10 && !_fIntegerMode)
                    {
                        result = -(Integer(rat) + 1);
                    }
                    else
                    {
                        result = rat ^ GetChopNumber();
                    }

                    break;

                case IDC_ROL:
                case IDC_ROLC:
                    if (_fIntegerMode)
                    {
                        result = Integer(rat);

                        ulong w64Bits = result.ToUInt64_t();
                        ulong msb = (w64Bits >> (_dwWordBitWidth - 1)) & 1;
                        w64Bits <<= 1; // LShift by 1

                        if (op == IDC_ROL)
                        {
                            w64Bits |= msb; // Set the prev Msb as the current Lsb
                        }
                        else
                        {
                            w64Bits |= _carryBit; // Set the carry bit as the LSB
                            _carryBit = msb;      // Store the msb as the next carry bit
                        }

                        result = w64Bits;
                    }

                    break;

                case IDC_ROR:
                case IDC_RORC:
                    if (_fIntegerMode)
                    {
                        result = Integer(rat);

                        ulong w64Bits = result.ToUInt64_t();
                        ulong lsb = ((w64Bits & 0x01) == 1) ? 1UL : 0UL;
                        w64Bits >>= 1; // RShift by 1

                        if (op == IDC_ROR)
                        {
                            w64Bits |= lsb << (_dwWordBitWidth - 1);
                        }
                        else
                        {
                            w64Bits |= _carryBit << (_dwWordBitWidth - 1);
                            _carryBit = lsb;
                        }

                        result = w64Bits;
                    }

                    break;

                case IDC_PERCENT:
                    // If the operator is multiply/divide, we evaluate this as "X [op] (Y%)"
                    // Otherwise, we evaluate it as "X [op] (X * Y%)"
                    if (_nOpCode == IDC_MUL || _nOpCode == IDC_DIV)
                    {
                        result = rat / 100;
                    }
                    else
                    {
                        result = rat * (_lastVal / 100);
                    }

                    break;

                case IDC_SIN: /* Sine; normal and arc */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ASin(rat, _angletype) : Sin(rat, _angletype);
                    }

                    break;

                case IDC_SINH: /* Sine- hyperbolic and archyperbolic */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ASinh(rat) : Sinh(rat);
                    }

                    break;

                case IDC_COS: /* Cosine, follows convention of sine function. */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ACos(rat, _angletype) : Cos(rat, _angletype);
                    }

                    break;

                case IDC_COSH: /* Cosine hyperbolic, follows convention of sine h function. */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ACosh(rat) : Cosh(rat);
                    }

                    break;

                case IDC_TAN: /* Same as sine and cosine. */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ATan(rat, _angletype) : Tan(rat, _angletype);
                    }

                    break;

                case IDC_TANH: /* Same as sine h and cosine h. */
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ATanh(rat) : Tanh(rat);
                    }

                    break;

                case IDC_SEC:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ACos(Invert(rat), _angletype) : Invert(Cos(rat, _angletype));
                    }

                    break;

                case IDC_CSC:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ASin(Invert(rat), _angletype) : Invert(Sin(rat, _angletype));
                    }

                    break;

                case IDC_COT:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ATan(Invert(rat), _angletype) : Invert(Tan(rat, _angletype));
                    }

                    break;

                case IDC_SECH:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ACosh(Invert(rat)) : Invert(Cosh(rat));
                    }

                    break;

                case IDC_CSCH:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ASinh(Invert(rat)) : Invert(Sinh(rat));
                    }

                    break;

                case IDC_COTH:
                    if (!_fIntegerMode)
                    {
                        result = _bInv ? ATanh(Invert(rat)) : Invert(Tanh(rat));
                    }

                    break;

                case IDC_REC: /* Reciprocal. */
                    result = Invert(rat);
                    break;

                case IDC_SQR: /* Square */
                    result = Pow(rat, 2);
                    break;

                case IDC_SQRT: /* Square Root */
                    result = Root(rat, 2);
                    break;

                case IDC_CUBEROOT:
                case IDC_CUB: /* Cubing and cube root functions. */
                    result = IDC_CUBEROOT == (int)op ? Root(rat, 3) : Pow(rat, 3);
                    break;

                case IDC_LOG: /* Functions for common log. */
                    result = Log10(rat);
                    break;

                case IDC_POW10:
                    result = Pow(10, rat);
                    break;

                case IDC_POW2:
                    result = Pow(2, rat);
                    break;

                case IDC_LN: /* Functions for natural log. */
                    result = _bInv ? Exp(rat) : Log(rat);
                    break;

                case IDC_FAC: /* Calculate factorial.  Inverse is ineffective. */
                    result = Fact(rat);
                    break;

                case IDC_DEGREES:
                    ProcessCommand(IDC_INV);

                    // This case falls through to IDC_DMS case because in the old Win32 Calc,
                    // the degrees functionality was achieved as 'Inv' of 'dms' operation,
                    // so setting the IDC_INV command first and then performing 'dms' operation as global variables _bInv, _bRecord
                    // are set properly through ProcessCommand(IDC_INV)
                    result = DegreesMinutesSeconds(rat);
                    break;

                case IDC_DMS:
                    result = DegreesMinutesSeconds(rat);
                    break;

                case IDC_CEIL:
                    result = (Frac(rat) > 0) ? Integer(rat + 1) : Integer(rat);
                    break;

                case IDC_FLOOR:
                    result = (Frac(rat) < 0) ? Integer(rat - 1) : Integer(rat);
                    break;

                case IDC_ABS:
                    result = Abs(rat);
                    break;
            }
        }
        catch (CalcException ex)
        {
            DisplayError(ex.ErrorCode);
            result = rat;
        }

        return result;
    }

    // Conversion between degrees/minutes/seconds and decimal degrees (the 'dms' operation and its inverse).
    private Rational DegreesMinutesSeconds(Rational rat)
    {
        Rational result = new Rational();
        if (!_fIntegerMode)
        {
            int shftRat = _bInv ? 100 : 60;

            Rational degreeRat = Integer(rat);

            Rational minuteRat = (rat - degreeRat) * shftRat;

            Rational secondRat = minuteRat;

            minuteRat = Integer(minuteRat);

            secondRat = (secondRat - minuteRat) * shftRat;

            //
            // degreeRat == degrees, minuteRat == minutes, secondRat == seconds
            //
            shftRat = _bInv ? 60 : 100;
            secondRat /= shftRat;

            minuteRat = (minuteRat + secondRat) / shftRat;

            result = degreeRat + minuteRat;
        }

        return result;
    }

    /// <summary>Routine to display error messages and set _bError flag. Errors are called with DisplayError (n).</summary>
    public void DisplayError(uint nError)
    {
        string errorString = GetString(EngineStrings.IDS_ERRORS_FIRST + (int)(nError & SCODE_MASK));

        SetPrimaryDisplay(errorString, true /*isError*/);

        _bError = true; // Set error flag.  Only cleared with CLEAR or CENTR.

        _historyCollector.ClearHistoryLine(errorString);
    }

    // Routines to perform standard operations &|^~<<>>+-/*% and pwr.
    private Rational DoOperation(int operation, Rational lhs, Rational rhs)
    {
        // Remove any variance in how 0 could be represented in rat e.g. -0, 0/n, etc.
        Rational result = lhs != 0 ? lhs : new Rational(0);

        try
        {
            switch (operation)
            {
                case IDC_AND:
                    result &= rhs;
                    break;

                case IDC_OR:
                    result |= rhs;
                    break;

                case IDC_XOR:
                    result ^= rhs;
                    break;

                case IDC_NAND:
                    result = (result & rhs) ^ GetChopNumber();
                    break;

                case IDC_NOR:
                    result = (result | rhs) ^ GetChopNumber();
                    break;

                case IDC_RSHF:
                {
                    if (_fIntegerMode && result >= _dwWordBitWidth) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcException(CalcErr.NoResult);
                    }

                    ulong w64Bits = rhs.ToUInt64_t();
                    bool fMsb = ((w64Bits >> (_dwWordBitWidth - 1)) & 1) != 0;

                    Rational holdVal = result;
                    result = Rational.Rsh(rhs, holdVal);

                    if (fMsb)
                    {
                        result = Integer(result);

                        Rational tempRat = Rational.Rsh(GetChopNumber(), holdVal);
                        tempRat = Integer(tempRat);

                        result |= tempRat ^ GetChopNumber();
                    }

                    break;
                }

                case IDC_RSHFL:
                    if (_fIntegerMode && result >= _dwWordBitWidth) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcException(CalcErr.NoResult);
                    }

                    result = Rational.Rsh(rhs, result);
                    break;

                case IDC_LSHF:
                    if (_fIntegerMode && result >= _dwWordBitWidth) // Lsh/Rsh >= than current word size is always 0
                    {
                        throw new CalcException(CalcErr.NoResult);
                    }

                    result = Rational.Lsh(rhs, result);
                    break;

                case IDC_ADD:
                    result += rhs;
                    break;

                case IDC_SUB:
                    result = rhs - result;
                    break;

                case IDC_MUL:
                    result *= rhs;
                    break;

                case IDC_DIV:
                case IDC_MOD:
                {
                    int iNumeratorSign = 1, iDenominatorSign = 1;
                    Rational temp = result;
                    result = rhs;

                    if (_fIntegerMode)
                    {
                        ulong w64Bits = rhs.ToUInt64_t();
                        bool fMsb = ((w64Bits >> (_dwWordBitWidth - 1)) & 1) != 0;

                        if (fMsb)
                        {
                            result = (rhs ^ GetChopNumber()) + 1;

                            iNumeratorSign = -1;
                        }

                        w64Bits = temp.ToUInt64_t();
                        fMsb = ((w64Bits >> (_dwWordBitWidth - 1)) & 1) != 0;

                        if (fMsb)
                        {
                            temp = (temp ^ GetChopNumber()) + 1;

                            iDenominatorSign = -1;
                        }
                    }

                    if (operation == IDC_DIV)
                    {
                        result /= temp;
                        if (_fIntegerMode && (iNumeratorSign * iDenominatorSign) == -1)
                        {
                            result = -Integer(result);
                        }
                    }
                    else
                    {
                        if (_fIntegerMode)
                        {
                            // Programmer mode, use remrat (remainder after division)
                            result %= temp;

                            if (iNumeratorSign == -1)
                            {
                                result = -Integer(result);
                            }
                        }
                        else
                        {
                            // other modes, use modrat (modulus after division)
                            result = Mod(result, temp);
                        }
                    }

                    break;
                }

                case IDC_PWR: // Calculates rhs to the result(th) power.
                    result = Pow(rhs, result);
                    break;

                case IDC_ROOT: // Calculates rhs to the result(th) root.
                    result = Root(rhs, result);
                    break;

                case IDC_LOGBASEY:
                    result = Log(rhs) / Log(result);
                    break;
            }
        }
        catch (CalcException ex)
        {
            DisplayError(ex.ErrorCode);

            // On error, return the original value
            result = lhs;
        }

        return result;
    }
}
