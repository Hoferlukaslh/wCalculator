#!/usr/bin/env python3
"""Generates compact per-locale JSON string tables from the Microsoft Calculator .resw files.

usage: gen_strings.py <Calculator/Resources dir> <output dir>
"""
import json, re, sys
import xml.etree.ElementTree as ET
from pathlib import Path

UI_KEYS = """
StandardModeText ScientificModeText ProgrammerModeText DateCalculationModeText ConverterModeText CalculatorModeText
HistoryLabel.Text MemoryLabel.Text HistoryEmpty.Text MemoryPaneEmpty.Text ClearHistory.Name ClearMemoryMenuItem.Text
Date_AddSubtractOption.Content Date_DifferenceOption.Content AddOption.Content SubtractOption.Content
DateDiff_FromHeader.Header DateDiff_ToHeader.Header AddSubtract_Date_FromHeader.Header Date_DifferenceLabel.Text
YearsLabel.Text MonthsLabel.Text DaysLabel.Text DateLabel.Text
Date_OutOfBoundMessage Date_SameDates Date_Day Date_Days Date_Month Date_Months Date_Week Date_Weeks Date_Year Date_Years
CalculationFailed Hex Dec Oct Bin DegreeMinuteSecond
SettingsHeader.Text SettingsAppearance.Text
""".split()

PREFIXES = ("UnitName_", "UnitAbbreviation_", "CategoryName_")


def read(path):
    out = {}
    if not path.exists():
        return out
    root = ET.parse(path).getroot()
    for d in root.iter("data"):
        name = d.get("name")
        v = d.find("value")
        if name is None or v is None or v.text is None:
            continue
        out[name.strip()] = v.text
    return out


def main(src, dst):
    src, dst = Path(src), Path(dst)
    dst.mkdir(parents=True, exist_ok=True)
    n = 0
    for loc in sorted(p for p in src.iterdir() if p.is_dir()):
        res = read(loc / "Resources.resw")
        eng = read(loc / "CEngineStrings.resw")
        table = {}
        for k, v in eng.items():
            if re.fullmatch(r"\d+", k) or re.fullmatch(r"[A-Za-z]+", k):
                table[k] = v
        for k, v in res.items():
            if k in UI_KEYS or (k.startswith(PREFIXES) and not k.endswith("AccessKey")):
                table[k] = v
        if not table:
            continue
        (dst / f"{loc.name}.json").write_text(
            json.dumps(table, ensure_ascii=False, separators=(",", ":"), sort_keys=True), encoding="utf-8")
        n += 1
    print(f"{n} locales written")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
