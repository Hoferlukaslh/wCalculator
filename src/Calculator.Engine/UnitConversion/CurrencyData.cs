// Currency support for the unit converter (not part of the original C# engine: Windows Calculator reads its
// rates from a Microsoft web service; here they come from a free, key-less API, see CurrencyService in the app).

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

#nullable enable

namespace Calculator.Engine.UnitConversion;

/// <summary>
/// A snapshot of exchange rates: how many units of each currency are worth one euro.
/// Codes are stored in lower case ("usd"), exactly as the fawazahmed0 currency-api returns them.
/// </summary>
public sealed class CurrencyRates
{
    /// <summary>ISO 4217 codes (lower case) shown by the converter; crypto-assets and metals returned by the API are ignored.</summary>
    public static readonly string[] SupportedCodes = (
        "aed afn all amd ang aoa ars aud awg azn bam bbd bdt bgn bhd bif bmd bnd bob brl bsd btn bwp byn bzd " +
        "cad cdf chf clp cny cop crc cup cve czk djf dkk dop dzd egp ern etb eur fjd fkp gbp gel ghs gip gmd " +
        "gnf gtq gyd hkd hnl htg huf idr ils inr iqd irr isk jmd jod jpy kes kgs khr kmf kpw krw kwd kyd kzt " +
        "lak lbp lkr lrd lsl lyd mad mdl mga mkd mmk mnt mop mru mur mvr mwk mxn myr mzn nad ngn nio nok npr " +
        "nzd omr pab pen pgk php pkr pln pyg qar ron rsd rub rwf sar sbd scr sdg sek sgd shp sle sos srd ssp " +
        "stn svc syp szl thb tjs tmt tnd top try ttd twd tzs uah ugx usd uyu uzs ves vnd vuv wst xaf xcd xof " +
        "xpf yer zar zmw zwg zwl").Split(' ');

    private static readonly HashSet<string> s_supported = new HashSet<string>(SupportedCodes, StringComparer.Ordinal);

    public CurrencyRates(
        string date,
        DateTime fetchedUtc,
        IReadOnlyDictionary<string, double> perEuro,
        IReadOnlyDictionary<string, string> names)
    {
        Date = date;
        FetchedUtc = fetchedUtc;
        PerEuro = perEuro;
        Names = names;
    }

    /// <summary>Publication date of the rates (as given by the API, "yyyy-MM-dd").</summary>
    public string Date { get; }

    /// <summary>When this snapshot was downloaded.</summary>
    public DateTime FetchedUtc { get; }

    /// <summary>Units of the currency per 1 EUR.</summary>
    public IReadOnlyDictionary<string, double> PerEuro { get; }

    /// <summary>English display names by code.</summary>
    public IReadOnlyDictionary<string, string> Names { get; }

    public static bool IsSupported(string code) => s_supported.Contains(code);

    /// <summary>
    /// Parses the answers of the API: <c>{"date":"2026-10-25","eur":{"usd":1.16,...}}</c> for the rates and
    /// <c>{"usd":"US Dollar",...}</c> for the names (may be null). Returns null when no usable rate is found.
    /// </summary>
    public static CurrencyRates? ParseApi(string ratesJson, string? namesJson, DateTime fetchedUtc)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(ratesJson);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string date = string.Empty;
            var rates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JsonProperty p in root.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.String && p.NameEquals("date"))
                {
                    date = p.Value.GetString() ?? string.Empty;
                }
                else if (p.Value.ValueKind == JsonValueKind.Object)
                {
                    // The object is named after its base currency ("eur"); the rates are relative to it.
                    // The service is queried with the euro as the base, which is what the rest of the code expects.
                    if (!p.NameEquals("eur"))
                    {
                        continue;
                    }

                    foreach (JsonProperty r in p.Value.EnumerateObject())
                    {
                        string code = r.Name.ToLowerInvariant();
                        if (s_supported.Contains(code)
                            && r.Value.ValueKind == JsonValueKind.Number
                            && r.Value.TryGetDouble(out double value)
                            && value > 0 && !double.IsInfinity(value) && !double.IsNaN(value))
                        {
                            rates[code] = value;
                        }
                    }
                }
            }

            if (rates.Count < 2)
            {
                return null;
            }

            rates["eur"] = 1.0;
            return new CurrencyRates(date, fetchedUtc, rates, ParseNames(namesJson, rates.Keys));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string> ParseNames(string? namesJson, IEnumerable<string> codes)
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(namesJson))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(namesJson!);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    HashSet<string> wanted = new HashSet<string>(codes, StringComparer.Ordinal);
                    foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                    {
                        string code = p.Name.ToLowerInvariant();
                        if (wanted.Contains(code) && p.Value.ValueKind == JsonValueKind.String)
                        {
                            string? n = p.Value.GetString();
                            if (!string.IsNullOrWhiteSpace(n))
                            {
                                names[code] = n!.Trim();
                            }
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // names are optional
            }
        }

        return names;
    }

    /// <summary>Serializes the snapshot for the local cache.</summary>
    public string ToCacheJson()
    {
        using var stream = new System.IO.MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("date", Date);
            w.WriteString("fetchedUtc", FetchedUtc.ToString("o", CultureInfo.InvariantCulture));
            w.WriteStartObject("rates");
            foreach (KeyValuePair<string, double> kv in PerEuro.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                w.WriteNumber(kv.Key, kv.Value);
            }

            w.WriteEndObject();
            w.WriteStartObject("names");
            foreach (KeyValuePair<string, string> kv in Names.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                w.WriteString(kv.Key, kv.Value);
            }

            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Reads a snapshot written by <see cref="ToCacheJson"/>; null when the text is not valid.</summary>
    public static CurrencyRates? FromCacheJson(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("rates", out JsonElement ratesEl)
                || ratesEl.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string date = root.TryGetProperty("date", out JsonElement d) && d.ValueKind == JsonValueKind.String ? d.GetString() ?? string.Empty : string.Empty;
            DateTime fetched = DateTime.MinValue;
            if (root.TryGetProperty("fetchedUtc", out JsonElement f) && f.ValueKind == JsonValueKind.String)
            {
                DateTime.TryParse(f.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out fetched);
            }

            var rates = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (JsonProperty p in ratesEl.EnumerateObject())
            {
                if (p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetDouble(out double v) && v > 0)
                {
                    rates[p.Name] = v;
                }
            }

            if (rates.Count < 2)
            {
                return null;
            }

            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("names", out JsonElement namesEl) && namesEl.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty p in namesEl.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String)
                    {
                        names[p.Name] = p.Value.GetString() ?? p.Name;
                    }
                }
            }

            return new CurrencyRates(date, fetched, rates, names);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Adds the currency category to another data loader. The units and the ratios are built from the
/// <see cref="CurrencyRates"/> returned by the provider; call <see cref="UnitConverter.ResetCategoriesAndRatios"/>
/// after the provider returned a new snapshot.
/// </summary>
public sealed class CurrencyConverterDataLoader : IConverterDataLoader
{
    /// <summary>Currency unit ids start here (the ids of the other categories are below 1000).</summary>
    public const int UnitIdBase = 1000;

    private readonly IConverterDataLoader _inner;
    private readonly Func<string, string> _getString;
    private readonly Func<CurrencyRates?> _ratesProvider;
    private readonly string _homeCurrency;

    private CurrencyRates? _builtFrom;
    private bool _built;
    private List<Unit> _units = new List<Unit>();
    private Dictionary<int, Dictionary<Unit, ConversionData>> _ratios = new Dictionary<int, Dictionary<Unit, ConversionData>>();

    /// <param name="inner">The loader of the other categories.</param>
    /// <param name="getString">Returns the localized string of a resource key.</param>
    /// <param name="ratesProvider">Returns the current rates, or null when none are available (yet).</param>
    /// <param name="homeCurrency">ISO code of the user's currency (default source unit); null for USD.</param>
    public CurrencyConverterDataLoader(
        IConverterDataLoader inner,
        Func<string, string> getString,
        Func<CurrencyRates?> ratesProvider,
        string? homeCurrency = null)
    {
        _inner = inner;
        _getString = getString;
        _ratesProvider = ratesProvider;
        _homeCurrency = string.IsNullOrEmpty(homeCurrency) ? "usd" : homeCurrency!.ToLowerInvariant();
    }

    /// <summary>The snapshot the units were built from (null when there was none).</summary>
    public CurrencyRates? BuiltFrom => _builtFrom;

    /// <summary>True when the provider has a newer snapshot than the one the units were built from.</summary>
    public bool IsStale => !ReferenceEquals(_ratesProvider(), _builtFrom) || !_built;

    public void LoadData() => _inner.LoadData();

    public List<Category> GetOrderedCategories()
    {
        List<Category> categories = _inner.GetOrderedCategories();
        categories.Add(new Category((int)ConverterCategory.Currency, _getString("CategoryName_CurrencyText"), false));
        return categories;
    }

    public List<Unit> GetOrderedUnits(Category c)
    {
        if (c.Id != (int)ConverterCategory.Currency)
        {
            return _inner.GetOrderedUnits(c);
        }

        EnsureBuilt();
        return new List<Unit>(_units);
    }

    public Dictionary<Unit, ConversionData> LoadOrderedRatios(Unit u)
    {
        if (u.Id < UnitIdBase)
        {
            return _inner.LoadOrderedRatios(u);
        }

        EnsureBuilt();
        return _ratios.TryGetValue(u.Id, out Dictionary<Unit, ConversionData>? r)
            ? new Dictionary<Unit, ConversionData>(r)
            : new Dictionary<Unit, ConversionData>();
    }

    public bool SupportsCategory(Category target) => true;

    private void EnsureBuilt()
    {
        CurrencyRates? rates = _ratesProvider();
        if (_built && ReferenceEquals(rates, _builtFrom))
        {
            return;
        }

        _built = true;
        _builtFrom = rates;
        _units = new List<Unit>();
        _ratios = new Dictionary<int, Dictionary<Unit, ConversionData>>();
        if (rates == null)
        {
            return;
        }

        string from = rates.PerEuro.ContainsKey(_homeCurrency) ? _homeCurrency : "usd";
        string to = from == "eur" ? "usd" : "eur";

        var codes = new List<string>();
        for (int i = 0; i < CurrencyRates.SupportedCodes.Length; i++)
        {
            string code = CurrencyRates.SupportedCodes[i];
            if (rates.PerEuro.ContainsKey(code))
            {
                codes.Add(code);
            }
        }

        var unitByCode = new Dictionary<string, Unit>(StringComparer.Ordinal);
        foreach (string code in codes)
        {
            int id = UnitIdBase + Array.IndexOf(CurrencyRates.SupportedCodes, code);
            string upper = code.ToUpperInvariant();
            string name = rates.Names.TryGetValue(code, out string? n) && n.Length > 0 ? upper + " – " + n : upper;
            var unit = new Unit(id, name, upper, code == from, code == to, false);
            unitByCode[code] = unit;
            _units.Add(unit);
        }

        // 1 unit of X is worth 1 / perEuro[X] euros, so one X is perEuro[Y] / perEuro[X] units of Y.
        foreach (string source in codes)
        {
            double sourceRate = rates.PerEuro[source];
            var entries = new Dictionary<Unit, ConversionData>();
            foreach (string target in codes)
            {
                entries[unitByCode[target]] = new ConversionData(rates.PerEuro[target] / sourceRate, 0, false);
            }

            _ratios[unitByCode[source].Id] = entries;
        }
    }
}
