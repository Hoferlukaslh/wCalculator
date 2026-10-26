// Port of CalcManager CEngine scicomm.cpp (the command state machine).
// Original: Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Globalization;
using Calculator.Engine.Ratpak;
using static Calculator.Engine.CalcUtils;
using static Calculator.Engine.CCommand;

namespace Calculator.Engine;

internal sealed partial class CCalcEngine
{
    // NPrecedenceOfOp
    //
    // returns a virtual number for precedence for the operator. We expect binary operator only, otherwise the lowest number
    // 0 is returned. Higher the number, higher the precedence of the operator.
    private static int NPrecedenceOfOp(int nopCode)
    {
        switch (nopCode)
        {
            case IDC_AND:
            case IDC_NAND:
            case IDC_NOR:
                return 1;
            case IDC_ADD:
            case IDC_SUB:
                return 2;
            case IDC_LSHF:
            case IDC_RSHF:
            case IDC_RSHFL:
            case IDC_MOD:
            case IDC_DIV:
            case IDC_MUL:
                return 3;
            case IDC_PWR:
            case IDC_ROOT:
            case IDC_LOGBASEY:
                return 4;
            default: // IDC_OR, IDC_XOR and everything else
                return 0;
        }
    }

    // HandleErrorCommand
    //
    // When it is discovered by the state machine that at this point the input is not valid (eg. "1+)"), we want to proceed as though this input never
    // occurred and may be some feedback to user like Beep. The rest of input can then continue by just ignoring this command.
    private void HandleErrorCommand(int idc)
    {
        if (!IsGuiSettingOpCode(idc))
        {
            // We would have saved the prev command. Need to forget this state
            _nTempCom = _nLastCom;
        }
    }

    private void HandleMaxDigitsReached()
    {
        _calcDisplay?.MaxDigitsReached();
    }

    private void ClearTemporaryValues()
    {
        _bInv = false;
        _input.Clear();
        _bRecord = true;
        CheckAndAddLastBinOpToHistory();
        DisplayNum();
        _bError = false;
    }

    private void ClearDisplay()
    {
        _calcDisplay?.SetExpressionDisplay(new List<(string Text, int CommandIndex)>(), new List<IExpressionCommand>());
    }

    public void ProcessCommand(int wParam)
    {
        if (wParam == IDC_SET_RESULT)
        {
            wParam = IDC_RECALL;
            _bSetCalcState = true;
        }

        ProcessCommandWorker(wParam);
    }

    private void ProcessCommandWorker(int wParam)
    {
        // Save the last command.  Some commands are not saved in this manor, these
        // commands are:
        // Inv, Deg, Rad, Grad, Stat, FE, MClear, Back, and Exp.  The excluded
        // commands are not
        // really mathematical operations, rather they are GUI mode settings.
        if (!IsGuiSettingOpCode(wParam))
        {
            _nLastCom = _nTempCom;
            _nTempCom = wParam;
        }

        // Clear expression shown after = sign, when user do any action.
        if (!_bNoPrevEqu)
        {
            ClearDisplay();
        }

        if (_bError)
        {
            if (wParam == IDC_CLEAR)
            {
                // handle "C" normally
            }
            else if (wParam == IDC_CENTR)
            {
                // treat "CE" as "C"
                wParam = IDC_CLEAR;
            }
            else
            {
                HandleErrorCommand(wParam);
                return;
            }
        }

        // Toggle Record/Display mode if appropriate.
        if (_bRecord)
        {
            if (IsBinOpCode(wParam) || IsUnaryOpCode(wParam) || IsOpInRange(wParam, IDC_FE, IDC_MMINUS) || IsOpInRange(wParam, IDC_OPENP, IDC_CLOSEP)
                || IsOpInRange(wParam, IDM_HEX, IDM_BIN) || IsOpInRange(wParam, IDM_QWORD, IDM_BYTE) || IsOpInRange(wParam, IDM_DEG, IDM_GRAD)
                || IsOpInRange(wParam, IDC_BINEDITSTART, IDC_BINEDITEND) || (IDC_INV == wParam) || (IDC_SIGN == wParam && 10 != _radix) || (IDC_RAND == wParam)
                || (IDC_EULER == wParam))
            {
                _bRecord = false;
                _currentVal = _input.ToRational(_radix, _precision);
                DisplayNum(); // Causes 3.000 to shrink to 3. on first op.
            }
        }
        else if (IsDigitOpCode(wParam) || wParam == IDC_PNT)
        {
            _bRecord = true;
            _input.Clear();

            /*
             * Account for scenarios where an equation includes any input after closing parenthesis - i.e. "(8)2=16".
             * This prevents the calculator from ending an equation and adding to history prematurely.
             */
            if (_nLastCom != IDC_CLOSEP)
            {
                CheckAndAddLastBinOpToHistory();
            }
        }

        // Interpret digit keys.
        if (IsDigitOpCode(wParam))
        {
            uint iValue = (uint)(wParam - IDC_0);

            // this is redundant, illegal keys are disabled
            if (iValue >= _radix)
            {
                HandleErrorCommand(wParam);
                return;
            }

            if (!_input.TryAddDigit(iValue, _radix, _fIntegerMode, GetMaxDecimalValueString(), _dwWordBitWidth, _cIntDigitsSav))
            {
                HandleErrorCommand(wParam);
                HandleMaxDigitsReached();
                return;
            }

            // Check if the last command was a closing parenthesis
            if (_nLastCom == IDC_CLOSEP)
            {
                HandleImplicitMultiplicationAfterCloseParen();
            }

            DisplayNum();

            return;
        }

        // BINARY OPERATORS:
        if (IsBinOpCode(wParam))
        {
            // Change the operation if last input was operation.
            if (IsBinOpCode(_nLastCom))
            {
                bool fPrecInvToHigher = false; // Is Precedence Inversion from lower to higher precedence happening ??

                _nOpCode = wParam;

                // Check to see if by changing this binop, a Precedence inversion is happening.
                // Eg. 1 * 2  + and + is getting changed to ^. The previous precedence rules would have already computed
                // 1*2, so we will put additional brackets to cover for precedence inversion and it will become (1 * 2) ^
                // Here * is _nPrevOpCode, _currentVal is 2  (by 1*2), _nLastCom is +, _nOpCode is ^
                if (_fPrecedence && 0 != _nPrevOpCode)
                {
                    int nPrev = NPrecedenceOfOp(_nPrevOpCode);
                    int nx = NPrecedenceOfOp(_nLastCom);
                    int ni = NPrecedenceOfOp(_nOpCode);
                    if (nx <= nPrev && ni > nPrev) // condition for Precedence Inversion
                    {
                        fPrecInvToHigher = true;
                        _nPrevOpCode = 0; // Once the precedence inversion has put additional brackets, its no longer required
                    }
                }

                _historyCollector.ChangeLastBinOp(_nOpCode, fPrecInvToHigher, _fIntegerMode);
                DisplayAnnounceBinaryOperator();
                return;
            }

            if (!_historyCollector.FOpndAddedToHistory())
            {
                // if the prev command was ) or unop then it is already in history as a opnd form (...)
                _historyCollector.AddOpndToHistory(_numberString, _currentVal);
            }

            /* _bChangeOp is true if there was an operation done and the   */
            /* current _currentVal is the result of that operation.  This is so */
            /* entering 3+4+5= gives 7 after the first + and 12 after the */
            /* the =.  The rest of this stuff attempts to do precedence in*/
            /* Scientific mode.                                           */
            if (_bChangeOp)
            {
                // "DoPrecedenceCheckAgain" loop of the original (a goto) is expressed as a while loop.
                while (true)
                {
                    int nx = NPrecedenceOfOp(wParam);
                    int ni = NPrecedenceOfOp(_nOpCode);

                    if ((nx > ni) && _fPrecedence)
                    {
                        if (_precedenceOpCount < MAXPRECDEPTH)
                        {
                            _precedenceVals[_precedenceOpCount] = _lastVal;

                            _nPrecOp[_precedenceOpCount] = _nOpCode;
                            _historyCollector.PushLastOpndStart(); // Eg. 1 + 2  *, Need to remember the start of 2 to do Precedence inversion if need to
                        }
                        else
                        {
                            _precedenceOpCount = MAXPRECDEPTH - 1;
                            HandleErrorCommand(wParam);
                        }

                        _precedenceOpCount++;
                        break;
                    }

                    /* do the last operation and then if the precedence array is not
                     * empty or the top is not the '(' demarcator then pop the top
                     * of the array and recheck precedence against the new operator
                     */
                    _currentVal = DoOperation(_nOpCode, _currentVal, _lastVal);
                    _nPrevOpCode = _nOpCode;

                    if (!_bError)
                    {
                        DisplayNum();
                        if (!_fPrecedence)
                        {
                            string groupedString = GroupDigitsPerRadix(_numberString, _radix);
                            _historyCollector.CompleteEquation(groupedString);
                            _historyCollector.AddOpndToHistory(_numberString, _currentVal);
                        }
                    }

                    if ((_precedenceOpCount != 0) && (_nPrecOp[_precedenceOpCount - 1] != 0))
                    {
                        _precedenceOpCount--;
                        _nOpCode = _nPrecOp[_precedenceOpCount];

                        _lastVal = _precedenceVals[_precedenceOpCount];

                        nx = NPrecedenceOfOp(_nOpCode);

                        // Precedence Inversion Higher to lower can happen which needs explicit enclosure of brackets
                        // Eg.  1 + 2 * Or 3 Or.  We would have pushed 1+ before, and now last + forces 2 Or 3 to be evaluated
                        // because last Or is less or equal to first + (after 1). But we see that 1+ is in stack and we evaluated to 2 Or 3
                        // This is precedence inversion happened because of operator changed in between. We put extra brackets like
                        // 1 + (2 Or 3)
                        if (ni <= nx)
                        {
                            _historyCollector.EnclosePrecInversionBrackets();
                        }

                        _historyCollector.PopLastOpndStart();
                        continue;
                    }

                    break;
                }
            }

            DisplayAnnounceBinaryOperator();
            _lastVal = _currentVal;
            _nOpCode = wParam;
            _historyCollector.AddBinOpToHistory(_nOpCode, _fIntegerMode);
            _bNoPrevEqu = _bChangeOp = true;
            return;
        }

        // UNARY OPERATORS:
        if (IsUnaryOpCode(wParam) || (wParam == IDC_DEGREES))
        {
            /* Functions are unary operations.                            */
            /* If the last thing done was an operator, _currentVal was cleared. */
            /* In that case we better use the number before the operator  */
            /* was entered, otherwise, things like 5+ 1/x give Divide By  */
            /* zero.  This way 5+=gives 10 like most calculators do.      */
            if (IsBinOpCode(_nLastCom))
            {
                _currentVal = _lastVal;
            }

            // we do not add percent sign to history or to two line display.
            // instead, we add the result of applying %.
            if (wParam != IDC_PERCENT)
            {
                if (!_historyCollector.FOpndAddedToHistory())
                {
                    _historyCollector.AddOpndToHistory(_numberString, _currentVal);
                }

                _historyCollector.AddUnaryOpToHistory(wParam, _bInv, _angletype);
            }

            if ((wParam == IDC_SIN) || (wParam == IDC_COS) || (wParam == IDC_TAN) || (wParam == IDC_SINH) || (wParam == IDC_COSH) || (wParam == IDC_TANH)
                || (wParam == IDC_SEC) || (wParam == IDC_CSC) || (wParam == IDC_COT) || (wParam == IDC_SECH) || (wParam == IDC_CSCH) || (wParam == IDC_COTH))
            {
                if (IsCurrentTooBigForTrig())
                {
                    _currentVal = 0;
                    DisplayError(CalcErr.Domain);
                    return;
                }
            }

            _currentVal = SciCalcFunctions(_currentVal, (uint)wParam);

            if (_bError)
            {
                return;
            }

            /* Display the result, reset flags, and reset indicators.     */
            DisplayNum();

            if (wParam == IDC_PERCENT)
            {
                CheckAndAddLastBinOpToHistory();
                _historyCollector.AddOpndToHistory(_numberString, _currentVal, true /* Add to primary and secondary display */);
            }

            /* reset the _bInv flag and indicators if it is set
            and have been used */

            if (_bInv
                && ((wParam == IDC_CHOP) || (wParam == IDC_SIN) || (wParam == IDC_COS) || (wParam == IDC_TAN) || (wParam == IDC_LN) || (wParam == IDC_DMS)
                    || (wParam == IDC_DEGREES) || (wParam == IDC_SINH) || (wParam == IDC_COSH) || (wParam == IDC_TANH) || (wParam == IDC_SEC) || (wParam == IDC_CSC)
                    || (wParam == IDC_COT) || (wParam == IDC_SECH) || (wParam == IDC_CSCH) || (wParam == IDC_COTH)))
            {
                _bInv = false;
            }

            return;
        }

        // Tiny binary edit windows clicked. Toggle that bit and update display
        if (IsOpInRange(wParam, IDC_BINEDITSTART, IDC_BINEDITEND))
        {
            // Same reasoning as for unary operators. We need to seed it previous number
            if (IsBinOpCode(_nLastCom))
            {
                _currentVal = _lastVal;
            }

            CheckAndAddLastBinOpToHistory();

            if (TryToggleBit(ref _currentVal, (uint)(wParam - IDC_BINEDITSTART)))
            {
                DisplayNum();
            }

            return;
        }

        /* Now branch off to do other commands and functions.                 */
        switch (wParam)
        {
            case IDC_CLEAR: /* Total clear.                                       */
                if (!_bChangeOp)
                {
                    // Preserve history, if everything done before was a series of unary operations.
                    CheckAndAddLastBinOpToHistory(false);
                }

                _lastVal = 0;

                _bChangeOp = false;
                _openParenCount = 0;
                _precedenceOpCount = _nTempCom = _nLastCom = _nOpCode = 0;
                _nPrevOpCode = 0;
                _bNoPrevEqu = true;
                _carryBit = 0;

                /* clear the parenthesis status box indicator, this will not be
                cleared for CENTR */
                if (_calcDisplay != null)
                {
                    _calcDisplay.SetParenthesisNumber(0);
                    ClearDisplay();
                }

                _historyCollector.ClearHistoryLine(string.Empty);
                ClearTemporaryValues();
                break;

            case IDC_CENTR: /* Clear only temporary values.                       */
                // Clear the INV & leave (=xx indicator active
                ClearTemporaryValues();
                break;

            case IDC_BACK:
                // Divide number by the current radix and truncate.
                // Only allow backspace if we're recording.
                if (_bRecord)
                {
                    _input.Backspace();
                    DisplayNum();
                }
                else
                {
                    HandleErrorCommand(wParam);
                }

                break;

            /* EQU enables the user to press it multiple times after and      */
            /* operation to enable repeats of the last operation.             */
            case IDC_EQU:
                while (_openParenCount > 0)
                {
                    // when _bError is set and _openParenCount is non-zero it goes into infinite loop
                    if (_bError)
                    {
                        break;
                    }

                    // automatic closing of all the parenthesis to get a meaningful result as well as ensure data integrity
                    _nTempCom = _nLastCom; // Put back this last saved command to the prev state so ) can be handled properly
                    ProcessCommand(IDC_CLOSEP);
                    _nLastCom = _nTempCom;  // Actually this is IDC_CLOSEP
                    _nTempCom = wParam; // put back in the state where last op seen was IDC_CLOSEP, and current op is IDC_EQU
                }

                if (!_bNoPrevEqu)
                {
                    // It is possible now unary op changed the num in screen, but still _lastVal hasn't changed.
                    _lastVal = _currentVal;
                }

                /* Last thing keyed in was an operator.  Lets do the op on*/
                /* a duplicate of the last entry.                     */
                if (IsBinOpCode(_nLastCom))
                {
                    _currentVal = _lastVal;
                }

                if (!_historyCollector.FOpndAddedToHistory())
                {
                    _historyCollector.AddOpndToHistory(_numberString, _currentVal);
                }

                // Evaluate the precedence stack.
                ResolveHighestPrecedenceOperation();
                while (_fPrecedence && _precedenceOpCount > 0)
                {
                    _precedenceOpCount--;
                    _nOpCode = _nPrecOp[_precedenceOpCount];
                    _lastVal = _precedenceVals[_precedenceOpCount];

                    // Precedence Inversion check
                    int ni = NPrecedenceOfOp(_nPrevOpCode);
                    int nx = NPrecedenceOfOp(_nOpCode);
                    if (ni <= nx)
                    {
                        _historyCollector.EnclosePrecInversionBrackets();
                    }

                    _historyCollector.PopLastOpndStart();

                    _bNoPrevEqu = true;

                    ResolveHighestPrecedenceOperation();
                }

                if (!_bError)
                {
                    string groupedString = GroupDigitsPerRadix(_numberString, _radix);
                    _historyCollector.CompleteEquation(groupedString);

                    _lastVal = _currentVal;
                    _nPrevOpCode = 0;
                    _precedenceOpCount = 0;
                }

                _bChangeOp = false;
                break;

            case IDC_OPENP:
            case IDC_CLOSEP:

                // -IF- the Paren holding array is full and we try to add a paren
                // -OR- the paren holding array is empty and we try to remove a
                //      paren
                // -OR- the precedence holding array is full
                if ((_openParenCount >= MAXPRECDEPTH && (wParam == IDC_OPENP)) || (_openParenCount == 0 && (wParam != IDC_OPENP))
                    || ((_precedenceOpCount >= MAXPRECDEPTH && _nPrecOp[_precedenceOpCount - 1] != 0)))
                {
                    if (_openParenCount == 0 && (wParam != IDC_OPENP))
                    {
                        _calcDisplay?.OnNoRightParenAdded();
                    }

                    HandleErrorCommand(wParam);
                    break;
                }

                if (wParam == IDC_OPENP)
                {
                    // if there's an omitted multiplication sign
                    if (IsDigitOpCode(_nLastCom) || IsUnaryOpCode(_nLastCom) || _nLastCom == IDC_PNT || _nLastCom == IDC_CLOSEP)
                    {
                        ProcessCommand(IDC_MUL);
                    }

                    CheckAndAddLastBinOpToHistory();
                    _historyCollector.AddOpenBraceToHistory();

                    // Open level of parentheses, save number and operation.
                    _parenVals[_openParenCount] = _lastVal;

                    _nOp[_openParenCount++] = _bChangeOp ? _nOpCode : 0;

                    /* save a special marker on the precedence array */
                    if (_precedenceOpCount < _nPrecOp.Length)
                    {
                        _nPrecOp[_precedenceOpCount++] = 0;
                    }

                    _lastVal = 0;
                    if (IsBinOpCode(_nLastCom))
                    {
                        // We want 1 + ( to start as 1 + (0. Any number you type replaces 0. But if it is 1 + 3 (, it is
                        // treated as 1 + (3
                        _currentVal = 0;
                    }

                    _nTempCom = 0;
                    _nOpCode = 0;
                    _bChangeOp = false; // a ( is like starting a fresh sub equation
                }
                else
                {
                    // Last thing keyed in was an operator. Lets do the op on a duplicate of the last entry.
                    if (IsBinOpCode(_nLastCom))
                    {
                        _currentVal = _lastVal;
                    }

                    if (!_historyCollector.FOpndAddedToHistory())
                    {
                        _historyCollector.AddOpndToHistory(_numberString, _currentVal);
                    }

                    // Get the operation and number and return result.
                    _currentVal = DoOperation(_nOpCode, _currentVal, _lastVal);
                    _nPrevOpCode = _nOpCode;

                    // Now process the precedence stack till we get to an opcode which is zero.
                    for (_nOpCode = _nPrecOp[--_precedenceOpCount]; _nOpCode != 0; _nOpCode = _nPrecOp[--_precedenceOpCount])
                    {
                        // Precedence Inversion check
                        int ni = NPrecedenceOfOp(_nPrevOpCode);
                        int nx = NPrecedenceOfOp(_nOpCode);
                        if (ni <= nx)
                        {
                            _historyCollector.EnclosePrecInversionBrackets();
                        }

                        _historyCollector.PopLastOpndStart();

                        _lastVal = _precedenceVals[_precedenceOpCount];

                        _currentVal = DoOperation(_nOpCode, _currentVal, _lastVal);
                        _nPrevOpCode = _nOpCode;
                    }

                    _historyCollector.AddCloseBraceToHistory();

                    // Now get back the operation and opcode at the beginning of this parenthesis pair
                    _openParenCount -= 1;
                    _lastVal = _parenVals[_openParenCount];
                    _nOpCode = _nOp[_openParenCount];

                    // _bChangeOp should be true if _nOpCode is valid
                    _bChangeOp = _nOpCode != 0;
                }

                // Set the "(=xx" indicator.
                _calcDisplay?.SetParenthesisNumber(_openParenCount);

                if (!_bError)
                {
                    DisplayNum();
                }

                break;

            // BASE CHANGES:
            case IDM_HEX:
            case IDM_DEC:
            case IDM_OCT:
            case IDM_BIN:
                SetRadixTypeAndNumWidth((RadixType)(wParam - IDM_HEX), (NumWidth)(-1));
                _historyCollector.UpdateHistoryExpression(_radix, _precision);
                break;

            case IDM_QWORD:
            case IDM_DWORD:
            case IDM_WORD:
            case IDM_BYTE:
                if (_bRecord)
                {
                    _currentVal = _input.ToRational(_radix, _precision);
                    _bRecord = false;
                }

                // Compat. mode BaseX: Qword, Dword, Word, Byte
                SetRadixTypeAndNumWidth((RadixType)(-1), (NumWidth)(wParam - IDM_QWORD));
                break;

            case IDM_DEG:
            case IDM_RAD:
            case IDM_GRAD:
                _angletype = (AngleType)(wParam - IDM_DEG);
                break;

            case IDC_SIGN:
                if (_bRecord)
                {
                    if (_input.TryToggleSign(_fIntegerMode, GetMaxDecimalValueString()))
                    {
                        DisplayNum();
                    }
                    else
                    {
                        HandleErrorCommand(wParam);
                    }

                    break;
                }

                // Doing +/- while in Record mode is not a unary operation
                if (IsBinOpCode(_nLastCom))
                {
                    _currentVal = _lastVal;
                }

                if (!_historyCollector.FOpndAddedToHistory())
                {
                    _historyCollector.AddOpndToHistory(_numberString, _currentVal);
                }

                _currentVal = -_currentVal;

                DisplayNum();
                _historyCollector.AddUnaryOpToHistory(IDC_SIGN, _bInv, _angletype);
                break;

            case IDC_RECALL:
                if (_bSetCalcState)
                {
                    // Not a Memory recall. set the result
                    _bSetCalcState = false;
                }
                else
                {
                    // Recall immediate memory value.
                    _currentVal = _memoryValue;
                }

                CheckAndAddLastBinOpToHistory();
                DisplayNum();
                break;

            case IDC_MPLUS:
            {
                /* MPLUS adds _currentVal to immediate memory and kills the "mem"   */
                /* indicator if the result is zero.                           */
                Rational result = _memoryValue + _currentVal;
                _memoryValue = TruncateNumForIntMath(result); // Memory should follow the current int mode
                break;
            }

            case IDC_MMINUS:
            {
                /* MMINUS subtracts _currentVal to immediate memory and kills the "mem"   */
                /* indicator if the result is zero.                           */
                Rational result = _memoryValue - _currentVal;
                _memoryValue = TruncateNumForIntMath(result);
                break;
            }

            case IDC_STORE:
            case IDC_MCLEAR:
                _memoryValue = wParam == IDC_STORE ? TruncateNumForIntMath(_currentVal) : new Rational(0);
                break;

            case IDC_PI:
                if (!_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // pi is like entering the number
                    _currentVal = new Rational(_bInv ? RatPak.two_pi : RatPak.pi);

                    DisplayNum();
                    _bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case IDC_RAND:
                if (!_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // rand is like entering the number

                    string str = GenerateRandomNumber().ToString("F" + _precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                    if (_decimalSeparator != '.')
                    {
                        str = str.Replace('.', _decimalSeparator);
                    }

                    Rat? rat = RatPak.StringToRat(false, str, false, string.Empty, _radix, _precision);
                    _currentVal = rat != null ? new Rational(rat) : new Rational(0);

                    DisplayNum();
                    _bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case IDC_EULER:
                if (!_fIntegerMode)
                {
                    CheckAndAddLastBinOpToHistory(); // e is like entering the number
                    _currentVal = new Rational(RatPak.rat_exp);

                    DisplayNum();
                    _bInv = false;
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case IDC_FE:
                // Toggle exponential notation display.
                _nFE = _nFE == NumberFormat.Float ? NumberFormat.Scientific : NumberFormat.Float;
                DisplayNum();
                break;

            case IDC_EXP:
                if (_bRecord && !_fIntegerMode && _input.TryBeginExponent())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case IDC_PNT:
                // Check if the last command was a closing parenthesis
                if (_nLastCom == IDC_CLOSEP)
                {
                    HandleImplicitMultiplicationAfterCloseParen();
                }

                if (_bRecord && !_fIntegerMode && _input.TryAddDecimalPt())
                {
                    DisplayNum();
                    break;
                }

                HandleErrorCommand(wParam);
                break;

            case IDC_INV:
                _bInv = !_bInv;
                break;
        }
    }

    // Treat a number or decimal point typed right after a closing parenthesis as an implicit multiplication.
    private void HandleImplicitMultiplicationAfterCloseParen()
    {
        _nOpCode = IDC_MUL;
        _lastVal = _currentVal;

        // We need to clear any previous state from last calculation
        _holdVal = new Rational(0);
        _bNoPrevEqu = true;

        // Add the operand to history before adding the implicit multiplication
        if (!_historyCollector.FOpndAddedToHistory())
        {
            _historyCollector.AddOpenBraceToHistory();
            _historyCollector.AddOpndToHistory(_numberString, _currentVal);
            _historyCollector.AddCloseBraceToHistory();
        }

        // Add the implicit multiplication to history
        _historyCollector.AddBinOpToHistory(_nOpCode, _fIntegerMode);

        _bChangeOp = true;
        _nPrevOpCode = 0;

        // Clear any pending operations in the precedence stack
        while (_precedenceOpCount > 0)
        {
            _precedenceOpCount--;
            _nPrecOp[_precedenceOpCount] = 0;
        }
    }

    // Helper function to resolve one item on the precedence stack.
    private void ResolveHighestPrecedenceOperation()
    {
        // Is there a valid operation around?
        if (_nOpCode != 0)
        {
            // If this is the first EQU in a string, set _holdVal=_currentVal
            // Otherwise let _currentVal=_holdVal.  This keeps _currentVal constant
            // through all EQUs in a row.
            if (_bNoPrevEqu)
            {
                _holdVal = _currentVal;
            }
            else
            {
                _currentVal = _holdVal;
                DisplayNum(); // to update the _numberString
                _historyCollector.AddBinOpToHistory(_nOpCode, _fIntegerMode, false);
                _historyCollector.AddOpndToHistory(_numberString, _currentVal); // Adding the repeated last op to history
            }

            // Do the current or last operation.
            _currentVal = DoOperation(_nOpCode, _currentVal, _lastVal);
            _nPrevOpCode = _nOpCode;
            _lastVal = _currentVal;

            // Check for errors.  If this wasn't done, DisplayNum
            // would immediately overwrite any error message.
            if (!_bError)
            {
                DisplayNum();
            }

            // No longer the first EQU.
            _bNoPrevEqu = false;
        }
        else if (!_bError)
        {
            DisplayNum();
        }
    }

    // CheckAndAddLastBinOpToHistory
    //
    //  This is a very confusing helper routine to add the last entered binary operator to the history. This is expected to
    // leave the history with <exp> <binop> state. It can really add the last entered binary op, or it can actually remove
    // the last operand from history. This happens because you can 'type' or 'compute' over last operand in some cases, thereby
    // effectively removing only it from the equation but still keeping the previous portion of the equation. Eg. 1 + 4 sqrt 5. The last
    // 5 will remove sqrt(4) as it is not used anymore to participate in 1 + 5
    // If you are messing with this, test cases like this CE, statistical functions, ( & MR buttons
    private void CheckAndAddLastBinOpToHistory(bool addToHistory = true)
    {
        if (_bChangeOp)
        {
            if (_historyCollector.FOpndAddedToHistory())
            {
                // if last time opnd was added but the last command was not a binary operator, then it must have come
                // from commands which add the operand, like unary operator. So history at this is showing 1 + sqrt(4)
                // but in reality the sqrt(4) is getting replaced by new number (may be unary op, or MR or SUM etc.)
                // So erase the last operand
                _historyCollector.RemoveLastOpndFromHistory();
            }
        }
        else if (_historyCollector.FOpndAddedToHistory() && !_bError)
        {
            // Corner case, where opnd is already in history but still a new opnd starting (1 + 4 sqrt 5). This is yet another
            // special casing of previous case under if (_bChangeOp), but this time we can do better than just removing it
            // Let us make a current value =. So in case of 4 SQRT (or a equation under braces) and then a new equation is started, we can just form
            // a useful equation of sqrt(4) = 2 and continue a new equation from now on. But no point in doing this for things like
            // MR, SUM etc. All you will get is 5 = 5 kind of no useful equation.
            if ((IsUnaryOpCode(_nLastCom) || IDC_SIGN == _nLastCom || IDC_CLOSEP == _nLastCom) && 0 == _openParenCount)
            {
                if (addToHistory)
                {
                    _historyCollector.CompleteHistoryLine(GroupDigitsPerRadix(_numberString, _radix));
                }
            }
            else
            {
                _historyCollector.RemoveLastOpndFromHistory();
            }
        }
    }

    // change the display area from a static text to an editbox, which has the focus can make
    // Magnifier (Accessibility tool) work
    private void SetPrimaryDisplay(string text, bool isError = false)
    {
        if (_calcDisplay != null)
        {
            _calcDisplay.SetPrimaryDisplay(text, isError);
            _calcDisplay.SetIsInError(isError);
        }
    }

    private void DisplayAnnounceBinaryOperator()
    {
        // If _calcDisplay is null, this is not a high priority function
        // and should not be the reason we crash.
        _calcDisplay?.BinaryOperatorReceived();
    }

    // Unary operator Function Name table Element
    // since unary operators button names aren't exactly friendly for history purpose,
    // we have this separate table to get its localized name and for its Inv function if it exists.
    private sealed class FunctionNameElement
    {
        public FunctionNameElement(
            string degreeString,
            string inverseDegreeString = "",
            string radString = "",
            string inverseRadString = "",
            string gradString = "",
            string inverseGradString = "",
            string programmerModeString = "")
        {
            DegreeString = degreeString;
            InverseDegreeString = inverseDegreeString;
            RadString = radString;
            InverseRadString = inverseRadString;
            GradString = gradString;
            InverseGradString = inverseGradString;
            ProgrammerModeString = programmerModeString;
            HasAngleStrings = radString.Length != 0 || inverseRadString.Length != 0 || gradString.Length != 0 || inverseGradString.Length != 0;
        }

        public string DegreeString { get; }          // Used by default if there are no rad or grad specific strings.

        public string InverseDegreeString { get; }   // Will fall back to DegreeString if empty

        public string RadString { get; }

        public string InverseRadString { get; }      // Will fall back to RadString if empty

        public string GradString { get; }

        public string InverseGradString { get; }     // Will fall back to GradString if empty

        public string ProgrammerModeString { get; }

        public bool HasAngleStrings { get; }
    }

    // Table for each unary operator
    private static readonly Dictionary<int, FunctionNameElement> s_operatorStringTable = new Dictionary<int, FunctionNameElement>
    {
        { IDC_CHOP, new FunctionNameElement(string.Empty, EngineStrings.SIDS_FRAC) },

        { IDC_SIN, new FunctionNameElement(EngineStrings.SIDS_SIND, EngineStrings.SIDS_ASIND, EngineStrings.SIDS_SINR, EngineStrings.SIDS_ASINR, EngineStrings.SIDS_SING, EngineStrings.SIDS_ASING) },
        { IDC_COS, new FunctionNameElement(EngineStrings.SIDS_COSD, EngineStrings.SIDS_ACOSD, EngineStrings.SIDS_COSR, EngineStrings.SIDS_ACOSR, EngineStrings.SIDS_COSG, EngineStrings.SIDS_ACOSG) },
        { IDC_TAN, new FunctionNameElement(EngineStrings.SIDS_TAND, EngineStrings.SIDS_ATAND, EngineStrings.SIDS_TANR, EngineStrings.SIDS_ATANR, EngineStrings.SIDS_TANG, EngineStrings.SIDS_ATANG) },

        { IDC_SINH, new FunctionNameElement(string.Empty, EngineStrings.SIDS_ASINH) },
        { IDC_COSH, new FunctionNameElement(string.Empty, EngineStrings.SIDS_ACOSH) },
        { IDC_TANH, new FunctionNameElement(string.Empty, EngineStrings.SIDS_ATANH) },

        { IDC_SEC, new FunctionNameElement(EngineStrings.SIDS_SECD, EngineStrings.SIDS_ASECD, EngineStrings.SIDS_SECR, EngineStrings.SIDS_ASECR, EngineStrings.SIDS_SECG, EngineStrings.SIDS_ASECG) },
        { IDC_CSC, new FunctionNameElement(EngineStrings.SIDS_CSCD, EngineStrings.SIDS_ACSCD, EngineStrings.SIDS_CSCR, EngineStrings.SIDS_ACSCR, EngineStrings.SIDS_CSCG, EngineStrings.SIDS_ACSCG) },
        { IDC_COT, new FunctionNameElement(EngineStrings.SIDS_COTD, EngineStrings.SIDS_ACOTD, EngineStrings.SIDS_COTR, EngineStrings.SIDS_ACOTR, EngineStrings.SIDS_COTG, EngineStrings.SIDS_ACOTG) },

        { IDC_SECH, new FunctionNameElement(EngineStrings.SIDS_SECH, EngineStrings.SIDS_ASECH) },
        { IDC_CSCH, new FunctionNameElement(EngineStrings.SIDS_CSCH, EngineStrings.SIDS_ACSCH) },
        { IDC_COTH, new FunctionNameElement(EngineStrings.SIDS_COTH, EngineStrings.SIDS_ACOTH) },

        { IDC_LN, new FunctionNameElement(string.Empty, EngineStrings.SIDS_POWE) },
        { IDC_SQR, new FunctionNameElement(EngineStrings.SIDS_SQR) },
        { IDC_CUB, new FunctionNameElement(EngineStrings.SIDS_CUBE) },
        { IDC_FAC, new FunctionNameElement(EngineStrings.SIDS_FACT) },
        { IDC_REC, new FunctionNameElement(EngineStrings.SIDS_RECIPROC) },
        { IDC_DMS, new FunctionNameElement(string.Empty, EngineStrings.SIDS_DEGREES) },
        { IDC_SIGN, new FunctionNameElement(EngineStrings.SIDS_NEGATE) },
        { IDC_DEGREES, new FunctionNameElement(EngineStrings.SIDS_DEGREES) },
        { IDC_POW2, new FunctionNameElement(EngineStrings.SIDS_TWOPOWX) },
        { IDC_LOGBASEY, new FunctionNameElement(EngineStrings.SIDS_LOGBASEY) },
        { IDC_ABS, new FunctionNameElement(EngineStrings.SIDS_ABS) },
        { IDC_CEIL, new FunctionNameElement(EngineStrings.SIDS_CEIL) },
        { IDC_FLOOR, new FunctionNameElement(EngineStrings.SIDS_FLOOR) },
        { IDC_NAND, new FunctionNameElement(EngineStrings.SIDS_NAND) },
        { IDC_NOR, new FunctionNameElement(EngineStrings.SIDS_NOR) },
        { IDC_RSHFL, new FunctionNameElement(EngineStrings.SIDS_RSH) },
        { IDC_RORC, new FunctionNameElement(EngineStrings.SIDS_ROR) },
        { IDC_ROLC, new FunctionNameElement(EngineStrings.SIDS_ROL) },
        { IDC_CUBEROOT, new FunctionNameElement(EngineStrings.SIDS_CUBEROOT) },
        { IDC_MOD, new FunctionNameElement(EngineStrings.SIDS_MOD, programmerModeString: EngineStrings.SIDS_PROGRAMMER_MOD) },
    };

    public static string OpCodeToUnaryString(int nOpCode, bool fInv, AngleType angletype)
    {
        // Try to lookup the ID in the UFNE table
        string ids = string.Empty;

        if (s_operatorStringTable.TryGetValue(nOpCode, out FunctionNameElement? element))
        {
            if (!element.HasAngleStrings || AngleType.Degrees == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseDegreeString;
                }

                if (ids.Length == 0)
                {
                    ids = element.DegreeString;
                }
            }
            else if (AngleType.Radians == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseRadString;
                }

                if (ids.Length == 0)
                {
                    ids = element.RadString;
                }
            }
            else if (AngleType.Gradians == angletype)
            {
                if (fInv)
                {
                    ids = element.InverseGradString;
                }

                if (ids.Length == 0)
                {
                    ids = element.GradString;
                }
            }
        }

        if (ids.Length != 0)
        {
            return GetString(ids);
        }

        // If we didn't find an ID in the table, use the op code.
        return OpCodeToString(nOpCode);
    }

    public static string OpCodeToBinaryString(int nOpCode, bool isIntegerMode)
    {
        // Try to lookup the ID in the UFNE table
        string ids = string.Empty;

        if (s_operatorStringTable.TryGetValue(nOpCode, out FunctionNameElement? element))
        {
            if (isIntegerMode && element.ProgrammerModeString.Length != 0)
            {
                ids = element.ProgrammerModeString;
            }
            else
            {
                ids = element.DegreeString;
            }
        }

        if (ids.Length != 0)
        {
            return GetString(ids);
        }

        // If we didn't find an ID in the table, use the op code.
        return OpCodeToString(nOpCode);
    }

    public bool IsCurrentTooBigForTrig() => _currentVal >= _maxTrigonometricNum;

    public string GetCurrentResultForRadix(uint radix, int precision, bool groupDigitsPerRadix)
    {
        Rational rat = _bRecord ? _input.ToRational(_radix, _precision) : _currentVal;

        RatPak.ChangeConstants(_radix, precision);

        string numberString = GetStringForDisplay(rat, radix);
        if (numberString.Length != 0)
        {
            // Revert the precision to previously stored precision
            RatPak.ChangeConstants(_radix, _precision);
        }

        if (groupDigitsPerRadix)
        {
            return GroupDigitsPerRadix(numberString, radix);
        }

        return numberString;
    }

    public string GetStringForDisplay(Rational rat, uint radix)
    {
        string result = string.Empty;

        // Check for standard\scientific mode
        if (!_fIntegerMode)
        {
            result = rat.ToString(radix, _nFE, _precision);
        }
        else
        {
            // Programmer mode
            // Find most significant bit to determine if number is negative
            Rational tempRat = TruncateNumForIntMath(rat);

            try
            {
                ulong w64Bits = tempRat.ToUInt64_t();
                bool fMsb = ((w64Bits >> (_dwWordBitWidth - 1)) & 1) != 0;
                if ((radix == 10) && fMsb)
                {
                    // If high bit is set, then get the decimal number in negative 2's complement form.
                    tempRat = -((tempRat ^ GetChopNumber()) + 1);
                }

                result = tempRat.ToString(radix, _nFE, _precision);
            }
            catch (CalcException)
            {
            }
        }

        return result;
    }

    private double GenerateRandomNumber()
    {
        _random ??= new Random();
        return _random.NextDouble();
    }
}
