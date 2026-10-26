using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Calculator.Engine.UnitConversion;

namespace CalcApp.Services;

public enum CurrencyState
{
    /// <summary>Nothing happened yet.</summary>
    Idle,

    /// <summary>A download is running.</summary>
    Refreshing,

    /// <summary>The rates are the ones that were just downloaded (or are recent enough).</summary>
    Up,

    /// <summary>The download failed: the rates come from the cache, or there are none.</summary>
    Offline,
}

/// <summary>
/// Downloads the exchange rates of the free, key-less "currency-api" of fawazahmed0
/// (https://github.com/fawazahmed0/exchange-api, CC0, updated daily, no rate limit) and keeps the last answer
/// in a cache file so that the converter keeps working offline.
/// Two mirrors are tried in turn: jsDelivr, then Cloudflare Pages.
/// </summary>
public sealed class CurrencyService
{
    private static readonly string[] s_bases =
    {
        "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@latest/v1/",
        "https://latest.currency-api.pages.dev/v1/",
    };

    private const string RatesFile = "currencies/eur.min.json";
    private const string NamesFile = "currencies.min.json";

    /// <summary>The rates are refreshed at most this often unless the user asks for it.</summary>
    private static readonly TimeSpan s_maxAge = TimeSpan.FromHours(6);

    private static readonly HttpClient s_http = CreateClient();

    private readonly string _cachePath;
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

    public static CurrencyService Instance { get; } = new CurrencyService();

    private CurrencyService()
    {
        _cachePath = Path.Combine(SettingsService.DataDirectory, "currency-rates.json");
        Current = LoadCache();
    }

    /// <summary>The newest rates known (downloaded or cached), or null.</summary>
    public CurrencyRates? Current { get; private set; }

    public CurrencyState State { get; private set; } = CurrencyState.Idle;

    /// <summary>Raised on the calling thread of <see cref="RefreshAsync"/> whenever <see cref="Current"/> or <see cref="State"/> changed.</summary>
    public event Action? Changed;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("wCalculator/1.0");
        return client;
    }

    /// <summary>
    /// Downloads the rates unless the cache is recent and <paramref name="force"/> is false.
    /// Never throws: on failure the state becomes <see cref="CurrencyState.Offline"/> and the old rates are kept.
    /// </summary>
    public async Task RefreshAsync(bool force)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(true))
        {
            return; // already refreshing
        }

        try
        {
            CurrencyRates? current = Current;
            if (!force && current != null && DateTime.UtcNow - current.FetchedUtc < s_maxAge)
            {
                State = CurrencyState.Up;
                Changed?.Invoke();
                return;
            }

            State = CurrencyState.Refreshing;
            Changed?.Invoke();

            CurrencyRates? fresh = await DownloadAsync().ConfigureAwait(true);
            if (fresh != null)
            {
                Current = fresh;
                State = CurrencyState.Up;
                SaveCache(fresh);
            }
            else
            {
                State = CurrencyState.Offline;
            }

            Changed?.Invoke();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static async Task<CurrencyRates?> DownloadAsync()
    {
        foreach (string baseUrl in s_bases)
        {
            try
            {
                string ratesJson = await s_http.GetStringAsync(baseUrl + RatesFile).ConfigureAwait(false);
                string? namesJson = null;
                try
                {
                    namesJson = await s_http.GetStringAsync(baseUrl + NamesFile).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The names are optional: the codes are shown when they are missing.
                }

                CurrencyRates? parsed = CurrencyRates.ParseApi(ratesJson, namesJson, DateTime.UtcNow);
                if (parsed != null)
                {
                    return parsed;
                }
            }
            catch (Exception)
            {
                // Try the next mirror.
            }
        }

        return null;
    }

    private CurrencyRates? LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                return CurrencyRates.FromCacheJson(File.ReadAllText(_cachePath));
            }
        }
        catch (Exception)
        {
            // A broken cache is simply ignored.
        }

        return null;
    }

    private void SaveCache(CurrencyRates rates)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, rates.ToCacheJson());
        }
        catch (Exception)
        {
            // The cache is a convenience.
        }
    }
}
