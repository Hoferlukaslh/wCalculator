namespace CalcApp;

/// <summary>
/// Phosphor icon glyphs (https://phosphoricons.com, MIT license) of the font Assets/Fonts/Phosphor/Phosphor.ttf.
/// Use them with the "ico" style class so that the Phosphor font family is applied.
/// </summary>
public static class Icons
{
    public static readonly string Calculator = "";
    public static readonly string Function = "";
    public static readonly string Code = "";
    public static readonly string CalendarBlank = "";
    public static readonly string Ruler = "";
    public static readonly string Scales = "";
    public static readonly string Thermometer = "";
    public static readonly string Lightning = "";
    public static readonly string Square = "";
    public static readonly string Speedometer = "";
    public static readonly string Gauge = "";
    public static readonly string Clock = "";
    public static readonly string Power = "";
    public static readonly string HardDrives = "";
    public static readonly string Flask = "";
    public static readonly string Angle = "";
    public static readonly string CurrencyCircleDollar = "";
    public static readonly string ArrowsDownUp = "";
    public static readonly string Sun = "";
    public static readonly string Moon = "";
    public static readonly string DeviceMobile = "";
    public static readonly string Desktop = "";
    public static readonly string CircleHalf = "";
    public static readonly string Palette = "";
    public static readonly string DropHalf = "";
    public static readonly string Copy = "";
    public static readonly string ClipboardText = "";
    public static readonly string Backspace = "";
    public static readonly string ClockCounterClockwise = "";
    public static readonly string Trash = "";
    public static readonly string Plus = "";
    public static readonly string Minus = "";
    public static readonly string Divide = "";
    public static readonly string EqualsSign = "";
    public static readonly string PlusMinus = "";
    public static readonly string Percent = "";
    public static readonly string X = "";
    public static readonly string Gear = "";
    public static readonly string List = "";
    public static readonly string ArrowClockwise = "";
    public static readonly string ArrowCounterClockwise = "";
    public static readonly string Check = "";
    public static readonly string Warning = "";
    public static readonly string Vibrate = "";
    public static readonly string WifiSlash = "";
    public static readonly string CaretDown = "";
    public static readonly string CloudCheck = "";
    public static readonly string CloudSlash = "";
    public static readonly string SlidersHorizontal = "";
    public static readonly string Eyedropper = "";

    /// <summary>Icon of a unit converter category (ids of <c>Calculator.Engine.UnitConversion.ConverterCategory</c>).</summary>
    public static string ForConverterCategory(int categoryId) => categoryId switch
    {
        4 => Flask,         // volume
        5 => Ruler,         // length
        6 => Scales,        // weight
        7 => Thermometer,   // temperature
        8 => Lightning,     // energy
        9 => Square,        // area
        10 => Speedometer,  // speed
        11 => Clock,        // time
        12 => Power,        // power
        13 => HardDrives,   // data
        14 => Gauge,        // pressure
        15 => Angle,        // angle
        16 => CurrencyCircleDollar,
        _ => ArrowsDownUp,
    };
}
