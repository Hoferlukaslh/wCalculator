using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Calculator.Engine;
using Calculator.Engine.DateCalculation;
using Xunit;
using static Calculator.Engine.Tests.Calc.CalcKind;

namespace Calculator.Engine.Tests;

/// <summary>
/// Tests that do not rely on hand-picked answers: the engine is compared with an independent oracle
/// (System.Decimal, System.DateTime), with mathematical identities, and with random key sequences.
/// All random data comes from fixed seeds, so every failure is reproducible.
/// </summary>
public class EngineInvariantTests
{
    // Creating a Calc recomputes the 32/64-digit ratpak constants, which is slow when done thousands of times.
    // The arithmetic tests therefore share one Calc per mode and press C before each expression
    // (tests of this assembly never run in parallel, see TestSupport.cs).
    private static readonly Dictionary<Calc.CalcKind, Calc> s_shared = new Dictionary<Calc.CalcKind, Calc>();

    private static Calc Fresh(Calc.CalcKind kind)
    {
        if (!s_shared.TryGetValue(kind, out Calc? calc))
        {
            calc = new Calc(kind);
            s_shared[kind] = calc;
        }

        return calc.Send(Command.CommandCLEAR);
    }

    private static string Plain(string display) => display.Replace(",", string.Empty);

    private static decimal ToDecimal(string display) =>
        decimal.Parse(Plain(display), NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>"-12.5" becomes the key sequence "12.5n" understood by <see cref="Calc.Type"/>.</summary>
    private static string Keys(string signed) => signed.StartsWith('-') ? signed.Substring(1) + "n" : signed;

    private static string RandomNumber(Random rng, int maxIntDigits, int maxFracDigits)
    {
        int intDigits = rng.Next(1, maxIntDigits + 1);
        int fracDigits = rng.Next(0, maxFracDigits + 1);
        var sb = new StringBuilder();
        if (rng.Next(3) == 0)
        {
            sb.Append('-');
        }

        sb.Append((char)('1' + rng.Next(9)));
        for (int i = 1; i < intDigits; i++)
        {
            sb.Append((char)('0' + rng.Next(10)));
        }

        if (fracDigits > 0)
        {
            sb.Append('.');
            for (int i = 0; i < fracDigits; i++)
            {
                sb.Append((char)('0' + rng.Next(10)));
            }
        }

        return sb.ToString();
    }

    private static void AssertNoProblems(List<string> problems) =>
        Assert.True(problems.Count == 0, $"{problems.Count} problem(s):{Environment.NewLine}{string.Join(Environment.NewLine, problems.Take(10))}");

    // ---- exact arithmetic against System.Decimal --------------------------------------------------------------

    [Theory]
    [InlineData((int)Calc.CalcKind.Standard, 4, 4)]   // Standard mode displays 16 digits: keep every result within 16 digits
    [InlineData((int)Calc.CalcKind.Scientific, 8, 6)] // Scientific mode computes with 32 digits
    public void Addition_subtraction_and_multiplication_are_exact(int calcKind, int maxIntDigits, int maxFracDigits)
    {
        // (the enum is passed as an int because Calc is internal and a public test cannot expose it)
        var kind = (Calc.CalcKind)calcKind;
        var rng = new Random(20261026);
        var problems = new List<string>();
        for (int i = 0; i < 400; i++)
        {
            string a = RandomNumber(rng, maxIntDigits, maxFracDigits);
            string b = RandomNumber(rng, maxIntDigits, maxFracDigits);
            decimal da = decimal.Parse(a, CultureInfo.InvariantCulture);
            decimal db = decimal.Parse(b, CultureInfo.InvariantCulture);
            foreach ((char op, decimal expected) in new[] { ('+', da + db), ('-', da - db), ('*', da * db) })
            {
                string shown = Fresh(kind).Type($"{Keys(a)}{op}{Keys(b)}=").Primary;
                if (ToDecimal(shown) != expected)
                {
                    problems.Add($"{kind}: {a} {op} {b} = {shown} (expected {expected})");
                }
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Division_matches_System_Decimal_to_15_significant_digits()
    {
        var rng = new Random(7);
        var problems = new List<string>();
        for (int i = 0; i < 300; i++)
        {
            string a = RandomNumber(rng, 8, 6);
            string b = RandomNumber(rng, 6, 4);
            decimal expected = decimal.Parse(a, CultureInfo.InvariantCulture) / decimal.Parse(b, CultureInfo.InvariantCulture);
            string shown = Fresh(Scientific).Type($"{Keys(a)}/{Keys(b)}=").Primary;
            decimal actual = ToDecimal(shown);
            if (Math.Abs(actual - expected) > Math.Abs(expected) * 1e-15m)
            {
                problems.Add($"{a} / {b} = {shown} (expected about {expected})");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Operator_precedence_follows_the_mode()
    {
        var rng = new Random(11);
        var problems = new List<string>();
        for (int i = 0; i < 200; i++)
        {
            decimal[] v = new decimal[3];
            string[] s = new string[3];
            for (int k = 0; k < 3; k++)
            {
                s[k] = RandomNumber(rng, 4, 2);
                v[k] = decimal.Parse(s[k], CultureInfo.InvariantCulture);
            }

            string text = $"{Keys(s[0])}+{Keys(s[1])}*{Keys(s[2])}=";
            decimal scientific = ToDecimal(Fresh(Scientific).Type(text).Primary);
            decimal standard = ToDecimal(Fresh(Standard).Type(text).Primary);
            if (scientific != v[0] + (v[1] * v[2]))
            {
                problems.Add($"Scientific: {s[0]} + {s[1]} * {s[2]} = {scientific}");
            }

            if (standard != (v[0] + v[1]) * v[2])
            {
                problems.Add($"Standard (left to right): {s[0]} + {s[1]} * {s[2]} = {standard}");
            }
        }

        AssertNoProblems(problems);
    }

    // ---- identities that must hold whatever the numbers ----------------------------------------------------

    [Fact]
    public void Adding_then_subtracting_the_same_number_gives_back_the_start()
    {
        var rng = new Random(3);
        var problems = new List<string>();
        for (int i = 0; i < 300; i++)
        {
            string a = RandomNumber(rng, 10, 8);
            string b = RandomNumber(rng, 10, 8);
            decimal shown = ToDecimal(Fresh(Scientific).Type($"{Keys(a)}+{Keys(b)}-{Keys(b)}=").Primary);
            if (shown != decimal.Parse(a, CultureInfo.InvariantCulture))
            {
                problems.Add($"({a} + {b}) - {b} = {shown}");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Dividing_then_multiplying_by_the_same_number_gives_back_the_start()
    {
        // Results are kept as exact fractions between operations, so 1/3*3 is exactly 1, 1/7*7 exactly 1, ...
        var rng = new Random(5);
        var problems = new List<string>();
        foreach (int divisor in new[] { 3, 7, 9, 11, 13, 17, 19, 23, 29, 31 })
        {
            for (int i = 0; i < 20; i++)
            {
                string a = RandomNumber(rng, 9, 0).TrimStart('-');
                string shown = Plain(Fresh(Standard).Type($"{a}/{divisor}*{divisor}=").Primary);
                if (shown != a)
                {
                    problems.Add($"{a} / {divisor} * {divisor} = {shown}");
                }
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Square_key_equals_multiplying_by_itself()
    {
        var rng = new Random(13);
        var problems = new List<string>();
        for (int i = 0; i < 200; i++)
        {
            string a = RandomNumber(rng, 6, 4);
            string viaKey = Fresh(Scientific).Type(Keys(a)).Send(Command.CommandSQR).Primary;
            string viaProduct = Fresh(Scientific).Type($"{Keys(a)}*{Keys(a)}=").Primary;
            if (viaKey != viaProduct)
            {
                problems.Add($"{a}: x² key gives {viaKey}, {a}*{a} gives {viaProduct}");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Square_root_of_a_perfect_square_is_exact()
    {
        var problems = new List<string>();
        for (int n = 1; n <= 500; n += 7)
        {
            long square = (long)n * n;
            string shown = Plain(Fresh(Scientific).Type(square.ToString(CultureInfo.InvariantCulture)).Send(Command.CommandSQRT).Primary);
            if (shown != n.ToString(CultureInfo.InvariantCulture))
            {
                problems.Add($"sqrt({square}) = {shown}");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Decimal_fractions_do_not_show_binary_floating_point_noise()
    {
        Assert.Equal("0.3", new Calc(Standard).Type("0.1+0.2=").Primary);
        Assert.Equal("0.3", new Calc(Standard).Type("0.1*3=").Primary);
        Assert.Equal("0.7", new Calc(Standard).Type("1-0.3=").Primary);
        Assert.Equal("1.1", new Calc(Standard).Type("0.7+0.4=").Primary);
        Assert.Equal("0.01", new Calc(Standard).Type("0.1*0.1=").Primary);
        Assert.Equal("1", new Calc(Standard).Type("1/7*7=").Primary);
    }

    // ---- errors are reported, never thrown, and the calculator recovers -----------------------------------------

    [Fact]
    public void Invalid_operations_show_an_error()
    {
        var cases = new List<(string Name, Func<Calc> Run)>
        {
            ("1/0", () => new Calc(Standard).Type("1/0=")),
            ("0/0", () => new Calc(Standard).Type("0/0=")),
            ("sqrt(-1)", () => new Calc(Standard).Type("1n").Send(Command.CommandSQRT)),
            ("1/x of 0", () => new Calc(Standard).Type("0").Send(Command.CommandREC)),
            ("ln(-1)", () => new Calc(Scientific).Type("1n").Send(Command.CommandLN)),
            ("log(-1)", () => new Calc(Scientific).Type("1n").Send(Command.CommandLOG)),
            ("asin(2)", () => new Calc(Scientific).Send(Command.CommandDEG).Type("2").Send(Command.CommandASIN)),
            ("acos(2)", () => new Calc(Scientific).Send(Command.CommandDEG).Type("2").Send(Command.CommandACOS)),
        };

        var problems = new List<string>();
        foreach ((string name, Func<Calc> run) in cases)
        {
            try
            {
                Calc c = run();
                if (!c.Display.IsError)
                {
                    problems.Add($"{name}: no error shown, display says '{c.Primary}'");
                }
            }
            catch (Exception e)
            {
                problems.Add($"{name}: exception {e.GetType().Name}: {e.Message}");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Calculator_recovers_after_an_error()
    {
        Calc c = new Calc(Standard).Type("1/0=");
        Assert.True(c.Display.IsError);
        c.Send(Command.CommandCLEAR);
        Assert.False(c.Display.IsError);
        Assert.Equal("0", c.Primary);
        Assert.Equal("4", c.Type("2+2=").Primary);
    }

    // ---- programmer mode: two's complement for every word size ---------------------------------------------

    [Fact]
    public void Programmer_addition_wraps_around_at_every_word_size()
    {
        var problems = new List<string>();
        foreach ((Command size, string max, string wrapped) in new[]
        {
            (Command.CommandByte, "127", "-128"),
            (Command.CommandWord, "32767", "-32768"),
            (Command.CommandDword, "2147483647", "-2147483648"),
            (Command.CommandQword, "9223372036854775807", "-9223372036854775808"),
        })
        {
            string shown = Plain(new Calc(Programmer).Send(size).Type(max + "+1=").Primary);
            if (shown != wrapped)
            {
                problems.Add($"{size}: {max} + 1 = {shown} (expected {wrapped})");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Programmer_multiplication_keeps_only_the_bits_of_the_word_size()
    {
        var problems = new List<string>();
        foreach ((Command size, string product, string expected) in new[]
        {
            (Command.CommandByte, "100*2", "-56"),                                       // 200 - 256
            (Command.CommandWord, "200*200", "-25536"),                                  // 40000 - 65536
            (Command.CommandDword, "100000*100000", "1410065408"),                       // 10^10 mod 2^32
            (Command.CommandQword, "10000000000*10000000000", "7766279631452241920"),   // 10^20 mod 2^64
        })
        {
            string shown = Plain(new Calc(Programmer).Send(size).Type(product + "=").Primary);
            if (shown != expected)
            {
                problems.Add($"{size}: {product} = {shown} (expected {expected})");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Programmer_division_truncates_towards_zero()
    {
        Assert.Equal("-3", new Calc(Programmer).Type("7n/2=").Primary);
        Assert.Equal("3", new Calc(Programmer).Type("7/2=").Primary);
        Assert.Equal("-3", new Calc(Programmer).Type("7/2n=").Primary);
    }

    [Fact]
    public void Negative_numbers_are_shown_in_two_complement_in_other_bases()
    {
        Calc qword = new Calc(Programmer).Type("0-1=");
        Assert.Equal("FFFFFFFFFFFFFFFF", qword.Manager.GetResultForRadix(16, 64, false));

        Calc @byte = new Calc(Programmer).Send(Command.CommandByte).Type("0-1=");
        Assert.Equal("FF", @byte.Manager.GetResultForRadix(16, 64, false));
        Assert.Equal("11111111", @byte.Manager.GetResultForRadix(2, 64, false));
    }

    // ---- date arithmetic against System.DateTime -------------------------------------------------------------------

    [Fact]
    public void Day_differences_and_additions_match_System_DateTime()
    {
        var engine = new DateCalculationEngine();
        var rng = new Random(2026);
        var origin = new DateTime(1800, 1, 1); // the engine supports dates from 1601: stay clear of the lower bound when subtracting
        int range = (new DateTime(2400, 12, 31) - origin).Days;
        var problems = new List<string>();
        for (int i = 0; i < 2000; i++)
        {
            DateTime a = origin.AddDays(rng.Next(range));
            DateTime b = origin.AddDays(rng.Next(range));
            DateDifference? diff = engine.TryGetDateDifference(a, b, DateUnit.Day);
            int expected = Math.Abs((b - a).Days);
            if (diff == null || diff.Value.Day != expected)
            {
                problems.Add($"{a:yyyy-MM-dd} to {b:yyyy-MM-dd}: {diff?.Day.ToString() ?? "null"} days, expected {expected}");
            }

            int n = rng.Next(0, 40000);
            DateTime? added = engine.AddDuration(a, new DateDifference(0, 0, 0, n));
            if (added != a.AddDays(n))
            {
                problems.Add($"{a:yyyy-MM-dd} + {n} days = {added:yyyy-MM-dd}, expected {a.AddDays(n):yyyy-MM-dd}");
            }

            DateTime? subtracted = engine.SubtractDuration(a, new DateDifference(0, 0, 0, n));
            if (subtracted != a.AddDays(-n))
            {
                problems.Add($"{a:yyyy-MM-dd} - {n} days = {subtracted:yyyy-MM-dd}, expected {a.AddDays(-n):yyyy-MM-dd}");
            }
        }

        AssertNoProblems(problems);
    }

    [Fact]
    public void Leap_years_follow_the_gregorian_rules()
    {
        var engine = new DateCalculationEngine();
        foreach (int year in new[] { 1900, 2000, 2023, 2024, 2100 })
        {
            DateDifference? d = engine.TryGetDateDifference(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), DateUnit.Day);
            Assert.Equal(DateTime.IsLeapYear(year) ? 366 : 365, d!.Value.Day);
        }
    }

    // ---- random key sequences: no crash, no freeze ---------------------------------------------------------------------

    private static Command[] Pool(Calc.CalcKind kind)
    {
        var pool = new List<Command>();
        for (int i = 0; i < 10; i++)
        {
            pool.Add((Command)((int)Command.Command0 + i));
            pool.Add((Command)((int)Command.Command0 + i)); // digits twice as likely
        }

        pool.AddRange(new[]
        {
            Command.CommandPNT, Command.CommandSIGN, Command.CommandADD, Command.CommandSUB, Command.CommandMUL,
            Command.CommandDIV, Command.CommandEQU, Command.CommandCLEAR, Command.CommandCENTR, Command.CommandBACK,
            Command.CommandSQRT, Command.CommandSQR, Command.CommandREC, Command.CommandPERCENT,
        });

        if (kind == Scientific)
        {
            pool.AddRange(new[]
            {
                Command.CommandSIN, Command.CommandCOS, Command.CommandTAN, Command.CommandSINH, Command.CommandCOSH,
                Command.CommandTANH, Command.CommandASIN, Command.CommandACOS, Command.CommandATAN, Command.CommandLN,
                Command.CommandLOG, Command.CommandCUB, Command.CommandPWR, Command.CommandROOT, Command.CommandMOD,
                Command.CommandOPENP, Command.CommandCLOSEP, Command.CommandINV, Command.CommandPI, Command.CommandEuler,
                Command.CommandEXP, Command.CommandPOW10, Command.CommandPOW2, Command.CommandDEG, Command.CommandRAD,
                Command.CommandGRAD, Command.CommandFE,
            });
        }

        if (kind == Programmer)
        {
            pool.AddRange(new[]
            {
                Command.CommandA, Command.CommandB, Command.CommandC, Command.CommandD, Command.CommandE, Command.CommandF,
                Command.CommandAnd, Command.CommandOR, Command.CommandXor, Command.CommandNot, Command.CommandLSHF,
                Command.CommandRSHF, Command.CommandMOD, Command.CommandByte, Command.CommandWord, Command.CommandDword,
                Command.CommandQword, Command.CommandHex, Command.CommandDec, Command.CommandOct, Command.CommandBin,
            });
        }

        return pool.ToArray();
    }

    [Theory]
    [InlineData((int)Calc.CalcKind.Standard)]
    [InlineData((int)Calc.CalcKind.Scientific)]
    [InlineData((int)Calc.CalcKind.Programmer)]
    public void Random_key_sequences_never_crash_or_hang(int calcKind)
    {
        var kind = (Calc.CalcKind)calcKind;
        // The factorial key is left out on purpose: its cost grows with the input and is capped by the original engine.
        var rng = new Random(20261026 + (int)kind);
        Command[] pool = Pool(kind);
        var problems = new List<string>();
        for (int run = 0; run < 500; run++)
        {
            Command[] sequence = Enumerable.Range(0, rng.Next(1, 45)).Select(_ => pool[rng.Next(pool.Length)]).ToArray();
            Exception? failure = null;
            Task task = Task.Run(() =>
            {
                try
                {
                    var c = new Calc(kind);
                    foreach (Command command in sequence)
                    {
                        c.Manager.SendCommand(command);
                    }

                    if (c.Primary == null)
                    {
                        throw new InvalidOperationException("the display text is null");
                    }
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });

            // Blocking on purpose: this is how a hung sequence is detected.
#pragma warning disable xUnit1031
            string keys = string.Join(" ", sequence.Select(c => c.ToString().Replace("Command", string.Empty)));
            if (!task.Wait(TimeSpan.FromSeconds(20)))
            {
                problems.Add($"HANG (>20 s) in {kind}: {keys}");
            }
            else if (failure != null)
            {
                problems.Add($"CRASH in {kind}: {keys} -> {failure.GetType().Name}: {failure.Message}");
            }
#pragma warning restore xUnit1031
        }

        AssertNoProblems(problems);
    }
}
