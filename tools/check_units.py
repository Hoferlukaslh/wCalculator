#!/usr/bin/env python3
"""Compare the conversion tables of UnitConverterDataLoader.cs with exact reference definitions.

usage: python3 -I check_units.py <UnitConverterDataLoader.cs> [--emit-cs <output.cs>]
"""
import re
import sys
from fractions import Fraction as F

src = open(sys.argv[1], encoding="utf-8").read()
start = src.index("var conversionFactors")
end = src.index("// Build explicit conversion data")
block = src[start:end]

# {category: {unit: float}}
tables = {}
cat = None
for line in block.splitlines():
    m = re.search(r"\[ConverterCategory\.(\w+)\]", line)
    if m:
        cat = m.group(1)
        tables[cat] = {}
        continue
    m = re.match(r"\s*\{ (\w+), ([^}]+?) \},?\s*$", line)
    if m and cat:
        expr = m.group(2).strip()
        expr = re.sub(r"(\d)[dD]\b", r"\1", expr)
        if "/" in expr:
            a, b = expr.split("/")
            val = F(a.strip()) / F(b.strip())
        else:
            val = F(expr)
        tables[cat][m.group(1)] = val

# Exact reference values, expressed in the *table's* base unit of each category.
inch = F(254, 10000)
foot = 12 * inch
yard = 3 * foot
mile = 1760 * yard
lb = F(45359237, 100000000)  # kg
oz = lb / 16
gal_us = F(3785411784, 1000000000000) * 1000  # litres -> cm3 : 3785.411784
gal_us_cm3 = F(3785411784, 1000000)
ref = {
    # base: square metre
    "Area": {
        "Area_Acre": 4840 * yard * yard,
        "Area_SquareFoot": foot * foot,
        "Area_SquareYard": yard * yard,
        "Area_SquareMillimeter": F(1, 10**6),
        "Area_SquareCentimeter": F(1, 10**4),
        "Area_SquareInch": inch * inch,
        "Area_SquareMile": mile * mile,
        "Area_SquareKilometer": F(10**6),
        "Area_Hectare": F(10**4),
        "Area_Pyeong": F(400, 121),
    },
    # base: megabyte (10^6 bytes)
    "Data": {
        "Data_Bit": F(1, 8 * 10**6),
        "Data_Nibble": F(1, 2 * 10**6),
        "Data_Byte": F(1, 10**6),
        "Data_Kilobyte": F(1, 10**3),
        "Data_Gigabyte": F(10**3),
        "Data_Terabyte": F(10**6),
        "Data_Petabyte": F(10**9),
        "Data_Exabytes": F(10**12),
        "Data_Zetabytes": F(10**15),
        "Data_Yottabyte": F(10**18),
        "Data_Kilobit": F(1000, 8 * 10**6),
        "Data_Megabit": F(1, 8),
        "Data_Gigabit": F(1000, 8),
        "Data_Terabit": F(10**6, 8),
        "Data_Petabit": F(10**9, 8),
        "Data_Exabits": F(10**12, 8),
        "Data_Zetabits": F(10**15, 8),
        "Data_Yottabit": F(10**18, 8),
        "Data_Kibibits": F(1024, 8 * 10**6),
        "Data_Kibibytes": F(1024, 10**6),
        "Data_Mebibits": F(1024**2, 8 * 10**6),
        "Data_Mebibytes": F(1024**2, 10**6),
        "Data_Gibibits": F(1024**3, 8 * 10**6),
        "Data_Gibibytes": F(1024**3, 10**6),
        "Data_Tebibits": F(1024**4, 8 * 10**6),
        "Data_Tebibytes": F(1024**4, 10**6),
        "Data_Pebibits": F(1024**5, 8 * 10**6),
        "Data_Pebibytes": F(1024**5, 10**6),
        "Data_Exbibits": F(1024**6, 8 * 10**6),
        "Data_Exbibytes": F(1024**6, 10**6),
        "Data_Zebibits": F(1024**7, 8 * 10**6),
        "Data_Zebibytes": F(1024**7, 10**6),
        "Data_Yobibits": F(1024**8, 8 * 10**6),
        "Data_Yobibytes": F(1024**8, 10**6),
        "Data_FloppyDisk": F(1440 * 1024, 10**6),
    },
    # base: joule
    "Energy": {
        "Energy_Calorie": F(4184, 1000),
        "Energy_Kilocalorie": F(4184),
        "Energy_BritishThermalUnit": F(1055056, 1000),
        "Energy_Kilojoule": F(1000),
        "Energy_Kilowatthour": F(3600000),
        # CODATA 2018 / SI 2019: exact since the 2019 redefinition
        "Energy_ElectronVolt": F(1602176634, 10**9) * F(1, 10**19),
    },
    # base: metre
    "Length": {
        "Length_Inch": inch,
        "Length_Foot": foot,
        "Length_Yard": yard,
        "Length_Mile": mile,
        "Length_Micron": F(1, 10**6),
        "Length_Millimeter": F(1, 1000),
        "Length_Nanometer": F(1, 10**9),
        "Length_Angstrom": F(1, 10**10),
        "Length_Centimeter": F(1, 100),
        "Length_Kilometer": F(1000),
        "Length_NauticalMile": F(1852),
    },
    # base: watt
    "Power": {
        "Power_BritishThermalUnitPerMinute": F(1055056, 1000) / 60,
        "Power_Kilowatt": F(1000),
        # mechanical horsepower = 550 ft·lbf/s
        "Power_Horsepower": 550 * foot * lb * F(980665, 100000),
        "Power_FootPoundPerMinute": foot * lb * F(980665, 100000) / 60,
    },
    # base: second
    "Time": {
        "Time_Day": F(86400),
        "Time_Week": F(604800),
        "Time_Year": F(365 * 86400) + F(86400, 4),  # Julian year
        "Time_Millisecond": F(1, 1000),
        "Time_Microsecond": F(1, 10**6),
        "Time_Minute": F(60),
        "Time_Hour": F(3600),
    },
    # base: millilitre (cm3)
    "Volume": {
        "Volume_GallonUS": gal_us_cm3,
        "Volume_QuartUS": gal_us_cm3 / 4,
        "Volume_PintUS": gal_us_cm3 / 8,
        "Volume_CupUS": gal_us_cm3 / 16,
        "Volume_FluidOunceUS": gal_us_cm3 / 128,
        "Volume_TablespoonUS": gal_us_cm3 / 256,
        "Volume_TeaspoonUS": gal_us_cm3 / 768,
        "Volume_GallonUK": F(454609, 100),
        "Volume_QuartUK": F(454609, 100) / 4,
        "Volume_PintUK": F(454609, 100) / 8,
        "Volume_FluidOunceUK": F(454609, 100) / 160,
        "Volume_Liter": F(1000),
        "Volume_CubicCentimeter": F(1),
        "Volume_Milliliter": F(1),
        "Volume_CubicMeter": F(10**6),
        "Volume_CubicInch": inch**3 * 10**6,
        "Volume_CubicFoot": foot**3 * 10**6,
        "Volume_CubicYard": yard**3 * 10**6,
    },
    # base: kilogram
    "Weight": {
        "Weight_Hectogram": F(1, 10),
        "Weight_Decagram": F(1, 100),
        "Weight_Gram": F(1, 1000),
        "Weight_Pound": lb,
        "Weight_Ounce": oz,
        "Weight_Milligram": F(1, 10**6),
        "Weight_Centigram": F(1, 10**5),
        "Weight_Decigram": F(1, 10**4),
        "Weight_LongTon": 2240 * lb,
        "Weight_Tonne": F(1000),
        "Weight_Stone": 14 * lb,
        "Weight_Carat": F(2, 10**4),
        "Weight_ShortTon": 2000 * lb,
    },
    # base: cm/s
    "Speed": {
        "Speed_FeetPerSecond": foot * 100,
        "Speed_KilometersPerHour": F(100000, 3600),
        "Speed_Knot": F(1852 * 100, 3600),
        "Speed_MetersPerSecond": F(100),
        "Speed_MilesPerHour": mile * 100 / 3600,
    },
    # base: degree
    "Angle": {
        "Angle_Radian": F(180) / F(31415926535897932384626433832795, 10**31),
        "Angle_Gradian": F(9, 10),
    },
    # base: atmosphere
    "Pressure": {
        "Pressure_Bar": F(100000, 101325),
        "Pressure_KiloPascal": F(1000, 101325),
        "Pressure_Pascal": F(1, 101325),
        # 1 atm = 760 mmHg by definition of the standard atmosphere
        "Pressure_MillimeterOfMercury": F(1, 760),
        # psi = lbf/in2
        "Pressure_PSI": lb * F(980665, 100000) / (inch * inch) / 101325,
    },
}

# Energy foot-pound: lbf*ft = lb * g0 * ft
ref["Energy"]["Energy_FootPound"] = lb * F(980665, 100000) * foot

BASE = {
    "Area": "Area_SquareMeter", "Data": "Data_Megabyte", "Energy": "Energy_Joule", "Length": "Length_Meter",
    "Power": "Power_Watt", "Time": "Time_Second", "Volume": "Volume_Milliliter", "Weight": "Weight_Kilogram",
    "Speed": "Speed_CentimetersPerSecond", "Angle": "Angle_Degree", "Pressure": "Pressure_Atmosphere",
}
for c, b in BASE.items():
    assert tables[c][b] == 1, (c, b)
    ref[c][b] = F(1)

keys = dict(re.findall(r'AddUnit\(ConverterCategory\.\w+, (\w+), "([\w-]+)"', src))

bad = 0
checked = 0
print(f"{'unit':38s} {'file value':>26s} {'reference':>26s} {'rel.err':>10s}")
for category, units in ref.items():
    for unit, exact in units.items():
        got = tables[category].get(unit)
        if got is None:
            print(f"!! {unit} missing in table")
            continue
        checked += 1
        err = abs(got - exact) / exact
        flag = ""
        if err > F(1, 10**9):
            flag = "  <-- DIFFERS"
            bad += 1
        elif err > F(1, 10**14):
            flag = "  (rounded)"
        if flag:
            print(f"{unit:38s} {float(got):26.17g} {float(exact):26.17g} {float(err):10.2e}{flag}")

print(f"\nchecked {checked} factors, {bad} differ by more than 1e-9 relative")

if "--emit-cs" in sys.argv:
    out = sys.argv[sys.argv.index("--emit-cs") + 1]
    lines = [
        "// <auto-generated> by tools/check_units.py --emit-cs. Do not edit by hand. </auto-generated>",
        "// Exact reference values (SI / NIST / legal definitions of the US and UK units) of every unit of the converter,",
        "// expressed in the base unit of its category (the unit whose factor is 1 in UnitConverterDataLoader).",
        "using System.Collections.Generic;",
        "using Calculator.Engine.UnitConversion;",
        "",
        "namespace Calculator.Engine.Tests;",
        "",
        "internal static class UnitReferenceData",
        "{",
        "    // category, unit name key, base unit name key, reference value of 1 unit in base units",
        "    public static IEnumerable<object[]> Factors()",
        "    {",
    ]
    # Units that only exist for some regions (the test loader uses the "FR" region): checked by this script only.
    REGIONAL = {"Area_Pyeong"}
    for category, units in ref.items():
        base_key = keys[BASE[category]]
        for unit, exact in units.items():
            if unit in REGIONAL:
                continue
            lines.append(f'        yield return new object[] {{ ConverterCategory.{category}, "{keys[unit]}", "{base_key}", {float(exact)!r}d }};')
    lines += ["    }", "}", ""]
    open(out, "w", encoding="utf-8", newline="\n").write("\n".join(lines))
    print("wrote", out)
