using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FairSky;

internal interface ICitySearchService
{
    Task<City[]> SearchAsync(string query, CancellationToken cancellationToken);
}

internal sealed class CitySearchService : ICitySearchService
{
    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly HttpClient _http;
    private readonly object _sync = new();
    private readonly Dictionary<string, (DateTimeOffset Expires, City[] Cities)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CitySearchService(HttpClient? http = null) => _http = http ?? SharedHttp;

    public async Task<City[]> SearchAsync(string query, CancellationToken cancellationToken)
    {
        query = query.Trim();
        if (query.Length < 2) return [];
        if (query.Length > 100) throw new ArgumentException("Введите название короче 100 символов.");
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            if (_cache.TryGetValue(query, out var entry) && entry.Expires > DateTimeOffset.UtcNow)
                return (City[])entry.Cities.Clone();
        }
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(query)}&count=10&language=ru&format=json";
        using var response = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var cities = Parse(document.RootElement);
        lock (_sync)
        {
            if (_cache.Count >= 50) _cache.Clear();
            _cache[query] = (DateTimeOffset.UtcNow.AddMinutes(30), cities);
        }
        return (City[])cities.Clone();
    }

    private static City[] Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid geocoding response.");
        if (!root.TryGetProperty("results", out var results)) return [];
        if (results.ValueKind != JsonValueKind.Array) throw new JsonException("Invalid geocoding response.");
        var cities = new List<City>();
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("latitude", out var lat) || lat.ValueKind != JsonValueKind.Number || !lat.TryGetDouble(out double latitude)
                || !result.TryGetProperty("longitude", out var lon) || lon.ValueKind != JsonValueKind.Number || !lon.TryGetDouble(out double longitude)) continue;
            var city = new City(Text(result, "name"), latitude, longitude, Text(result, "country"), Text(result, "admin1"));
            if (CityStore.IsValid(city) && cities.All(c => CityStore.Key(c) != CityStore.Key(city))) cities.Add(city);
            if (cities.Count == 10) break;
        }
        return cities.ToArray();
    }

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
}
