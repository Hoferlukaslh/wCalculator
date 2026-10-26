using System;
using System.Collections.Generic;
using System.Linq;
using Calculator.Engine.UnitConversion;
using Xunit;

namespace Calculator.Engine.Tests;

public class CurrencyTests
{
    // Shape of the answers of https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/currencies/eur.min.json
    private const string RatesJson =
        "{\"date\":\"2026-10-25\",\"eur\":{\"1inch\":12.5,\"btc\":0.00001,\"chf\":0.8,\"eur\":1,\"jpy\":150,\"usd\":1.25,\"xyz\":3}}";

    private const string NamesJson =
        "{\"1inch\":\"1inch\",\"btc\":\"Bitcoin\",\"chf\":\"Swiss Franc\",\"eur\":\"Euro\",\"jpy\":\"Japanese Yen\",\"usd\":\"US Dollar\"}";

    private sealed class Callback : IUnitConverterVMCallback
    {
        public string From { get; private set; } = string.Empty;

        public string To { get; private set; } = string.Empty;

        public void DisplayCallback(string from, string to)
        {
            From = from;
            To = to;
        }

        public void SuggestedValueCallback(List<(string Value, Unit Unit)> suggestedValues)
        {
        }

        public void MaxDigitsReached()
        {
        }
    }

    // One shared snapshot: the loader rebuilds its units whenever the provider returns another instance.
    private static readonly CurrencyRates s_rates = Rates();

    private static CurrencyRates Rates() => CurrencyRates.ParseApi(RatesJson, NamesJson, new DateTime(2026, 10, 26, 8, 0, 0, DateTimeKind.Utc))!;

    private static UnitConverter CreateConverter(Func<CurrencyRates?> provider, string? home = null)
    {
        var loader = new CurrencyConverterDataLoader(new UnitConverterDataLoader(key => key, "CH"), key => key, provider, home);
        var converter = new UnitConverter(loader);
        converter.Initialize();
        return converter;
    }

    [Fact]
    public void Api_answer_is_parsed_and_non_fiat_codes_are_ignored()
    {
        CurrencyRates rates = Rates();
        Assert.Equal("2026-10-25", rates.Date);
        Assert.Equal(new[] { "chf", "eur", "jpy", "usd" }, rates.PerEuro.Keys.OrderBy(k => k, StringComparer.Ordinal).ToArray());
        Assert.Equal("Swiss Franc", rates.Names["chf"]);
        Assert.False(rates.Names.ContainsKey("btc"));
    }

    [Fact]
    public void Broken_answers_are_rejected()
    {
        Assert.Null(CurrencyRates.ParseApi("not json", null, DateTime.UtcNow));
        Assert.Null(CurrencyRates.ParseApi("{\"date\":\"2026-10-25\"}", null, DateTime.UtcNow));
        Assert.Null(CurrencyRates.ParseApi("{\"date\":\"2026-10-25\",\"eur\":{\"usd\":-1}}", null, DateTime.UtcNow));
    }

    [Fact]
    public void Cache_round_trip_keeps_rates_names_and_date()
    {
        CurrencyRates rates = Rates();
        CurrencyRates? copy = CurrencyRates.FromCacheJson(rates.ToCacheJson());
        Assert.NotNull(copy);
        Assert.Equal(rates.Date, copy!.Date);
        Assert.Equal(rates.FetchedUtc, copy.FetchedUtc);
        Assert.Equal(1.25, copy.PerEuro["usd"]);
        Assert.Equal("US Dollar", copy.Names["usd"]);
        Assert.Null(CurrencyRates.FromCacheJson("{}"));
    }

    [Fact]
    public void Currency_is_the_thirteenth_category_and_units_are_sorted_by_code()
    {
        UnitConverter converter = CreateConverter(() => s_rates);
        List<Category> categories = converter.GetCategories();
        Assert.Equal(13, categories.Count);
        Assert.Equal((int)ConverterCategory.Currency, categories.Last().Id);

        (List<Unit> units, Unit from, Unit to) = converter.SetCurrentCategory(categories.Last());
        Assert.Equal(new[] { "CHF", "EUR", "JPY", "USD" }, units.Select(u => u.Abbreviation).ToArray());
        Assert.Equal("USD – US Dollar", units.First(u => u.Abbreviation == "USD").Name);

        // Default pair: the euro against the US dollar for a user whose currency is unknown.
        Assert.Equal("USD", from.Abbreviation);
        Assert.Equal("EUR", to.Abbreviation);
    }

    [Fact]
    public void Home_currency_is_the_default_source()
    {
        UnitConverter converter = CreateConverter(() => s_rates, "CHF");
        Category currency = converter.GetCategories().Last();
        (List<Unit> _, Unit from, Unit to) = converter.SetCurrentCategory(currency);
        Assert.Equal("CHF", from.Abbreviation);
        Assert.Equal("EUR", to.Abbreviation);
    }

    [Fact]
    public void Conversion_uses_the_rates_through_the_euro()
    {
        UnitConverter converter = CreateConverter(() => s_rates);
        var callback = new Callback();
        converter.SetViewModelCallback(callback);
        Category currency = converter.GetCategories().Last();
        (List<Unit> units, Unit _, Unit _) = converter.SetCurrentCategory(currency);
        Unit usd = units.First(u => u.Abbreviation == "USD");
        Unit chf = units.First(u => u.Abbreviation == "CHF");
        Unit eur = units.First(u => u.Abbreviation == "EUR");

        converter.SetCurrentUnitTypes(usd, chf);
        converter.SendCommand(UnitConversionCommand.One);
        converter.SendCommand(UnitConversionCommand.Zero);
        converter.SendCommand(UnitConversionCommand.Zero);
        Assert.Equal("100", callback.From);
        Assert.Equal("64", callback.To); // 100 USD = 100 / 1.25 EUR = 80 EUR = 64 CHF

        converter.SetCurrentUnitTypes(usd, eur);
        Assert.Equal("80", callback.To);
    }

    [Fact]
    public void Units_are_rebuilt_after_a_new_snapshot()
    {
        CurrencyRates? current = null;
        var loader = new CurrencyConverterDataLoader(new UnitConverterDataLoader(key => key, "CH"), key => key, () => current);
        var converter = new UnitConverter(loader);
        converter.Initialize();
        Category currency = converter.GetCategories().Last();
        Assert.Empty(converter.SetCurrentCategory(currency).Units);
        Assert.False(loader.IsStale);

        current = s_rates;
        Assert.True(loader.IsStale);
        converter.ResetCategoriesAndRatios();
        Assert.False(loader.IsStale);
        Assert.Equal(4, converter.SetCurrentCategory(currency).Units.Count);
    }
}
