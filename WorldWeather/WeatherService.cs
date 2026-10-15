using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WorldWeather;

public sealed record City(string Name, double Latitude, double Longitude);
public sealed record DayWeather(string Date, double Max, double Min, int Code);
public sealed record WeatherData(double Temperature, double FeelsLike, int Humidity, double Wind, int Code, string Updated, DayWeather[] Days, bool Stale = false);

internal sealed class WeatherService
{
    // Replace support@example.com with an address where the API operator can contact you.
    private static readonly HttpClient Http = CreateClient();
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    private sealed record CacheEntry(WeatherData Data, DateTimeOffset Expires, DateTimeOffset? LastModified);

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("WorldWeather/0.2 (zoeyrus@gmail.com)");
        return client;
    }

    public async Task<WeatherData> GetAsync(City city, bool force = false)
    {
        string lat = city.Latitude.ToString("F4", CultureInfo.InvariantCulture);
        string lon = city.Longitude.ToString("F4", CultureInfo.InvariantCulture);
        string key = $"{lat}:{lon}";
        if (!force && _cache.TryGetValue(key, out var cached) && DateTimeOffset.UtcNow < cached.Expires)
            return cached.Data;

        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!force && _cache.TryGetValue(key, out cached) && DateTimeOffset.UtcNow < cached.Expires)
                return cached.Data;

            var url = $"https://api.met.no/weatherapi/locationforecast/2.0/compact?lat={lat}&lon={lon}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (_cache.TryGetValue(key, out cached) && cached.LastModified is { } lm)
                request.Headers.IfModifiedSince = lm;

            using var response = await Http.SendAsync(request).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
            {
                var fresh = cached with { Expires = Expiry(response) };
                _cache[key] = fresh;
                return fresh.Data;
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var document = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            var timeseries = document.RootElement.GetProperty("properties").GetProperty("timeseries");
            if (timeseries.GetArrayLength() == 0) throw new InvalidOperationException("MET Norway вернул пустой прогноз.");

            JsonElement now = timeseries[0];
            JsonElement instant = now.GetProperty("data").GetProperty("instant").GetProperty("details");
            double temp = Number(instant, "air_temperature");
            double wind = Number(instant, "wind_speed");
            int humidity = (int)Math.Round(Number(instant, "relative_humidity"));
            int code = CodeFromSymbol(SymbolFromEntry(now));
            // MET Norway compact does not include a 'feels like' field. Calculate wind chill
            // only in its standard validity range; otherwise show air temperature.
            double feelsLike = temp;
            if (temp <= 10 && wind > 1.34)
            {
                double kmh = wind * 3.6;
                feelsLike = 13.12 + 0.6215 * temp - 11.37 * Math.Pow(kmh, 0.16) + 0.3965 * temp * Math.Pow(kmh, 0.16);
            }

            // MET provides forecast timestamps in UTC; group by UTC calendar date.
            var daily = new SortedDictionary<DateTime, List<(double Temp, int Code)>>();
            foreach (var entry in timeseries.EnumerateArray())
            {
                var time = DateTimeOffset.Parse(entry.GetProperty("time").GetString()!, CultureInfo.InvariantCulture);
                var day = time.UtcDateTime.Date;
                if (day < DateTime.UtcNow.Date) continue;
                var details = entry.GetProperty("data").GetProperty("instant").GetProperty("details");
                if (!details.TryGetProperty("air_temperature", out var value)) continue;
                if (!daily.TryGetValue(day, out var list)) daily[day] = list = new();
                list.Add((value.GetDouble(), CodeFromSymbol(SymbolFromEntry(entry))));
            }

            var days = daily.Take(4).Select(pair =>
                new DayWeather(pair.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    pair.Value.Max(x => x.Temp), pair.Value.Min(x => x.Temp),
                    pair.Value.Select(x => x.Code).FirstOrDefault(x => x != 3, 3))).ToArray();

            var result = new WeatherData(temp, feelsLike, humidity, wind, code,
                now.GetProperty("time").GetString() ?? "", days);
            _cache[key] = new CacheEntry(result, Expiry(response), response.Content.Headers.LastModified);
            return result;
        }
        catch when (_cache.TryGetValue(key, out var existing))
        {
            return existing.Data with { Stale = true };
        }
        finally { gate.Release(); }
    }

    private static DateTimeOffset Expiry(HttpResponseMessage response)
    {
        // Respect server cache validity, with conservative fallback.
        return response.Content.Headers.Expires is { } expires && expires > DateTimeOffset.UtcNow
            ? expires : DateTimeOffset.UtcNow.AddMinutes(20);
    }

    private static double Number(JsonElement item, string field) =>
        item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : 0;

    private static string SymbolFromEntry(JsonElement entry)
    {
        var data = entry.GetProperty("data");
        foreach (var interval in new[] { "next_1_hours", "next_6_hours", "next_12_hours" })
            if (data.TryGetProperty(interval, out var obj) && obj.TryGetProperty("summary", out var summary)
                && summary.TryGetProperty("symbol_code", out var symbol))
                return symbol.GetString() ?? "";
        return "";
    }

    // Map MET Norway's symbolic condition strings to Open-Meteo/WMO-like codes
    // already understood by the existing WorldWeatherPage / DockItem UI.
    private static int CodeFromSymbol(string symbol)
    {
        var s = symbol.ToLowerInvariant();
        if (s.Contains("thunder")) return 95;
        if (s.Contains("snow")) return 73;
        if (s.Contains("sleet")) return 67;
        if (s.Contains("rain")) return s.Contains("heavy") ? 65 : 61;
        if (s.Contains("fog")) return 45;
        if (s.Contains("partlycloudy")) return 2;
        if (s.Contains("fair")) return 1;
        if (s.Contains("cloudy")) return 3;
        if (s.Contains("clearsky")) return 0;
        return 3;
    }

    public static string Description(int code) => code switch
    {
        0 => "Ясно", 1 => "Преимущественно ясно", 2 => "Переменная облачность", 3 => "Облачно",
        45 or 48 => "Туман", 51 or 53 or 55 => "Морось", 56 or 57 => "Ледяная морось",
        61 or 63 or 65 => "Дождь", 66 or 67 => "Мокрый снег", 71 or 73 or 75 or 77 => "Снег",
        80 or 81 or 82 => "Ливни", 85 or 86 => "Снегопад", 95 or 96 or 99 => "Гроза", _ => "Нет данных"
    };
    public static string Symbol(int code) => code switch
    {
        0 or 1 => "☀", 2 => "⛅", 3 => "☁", 45 or 48 => "🌫", 71 or 73 or 75 or 77 or 85 or 86 => "❄", 95 or 96 or 99 => "⛈", _ => "☂"
    };
}
