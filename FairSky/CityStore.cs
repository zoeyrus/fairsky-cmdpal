using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FairSky;

internal sealed class CityStore
{
    public const int MaximumCities = 12;
    private readonly object _sync = new();
    private readonly string _path;
    private City[] _cities = [];
    private string? _savedText;
    public string? LoadError { get; private set; }
    public event Action? Changed;

    public CityStore(string path, City[]? defaults = null)
    {
        _path = path;
        try
        {
            if (File.Exists(path))
            {
                _savedText = File.ReadAllText(path);
                var cities = JsonSerializer.Deserialize(_savedText, WeatherJsonContext.Default.CityArray)
                    ?? throw new JsonException("Expected a city array.");
                if (cities.Any(c => c is null || !IsValid(c))) throw new JsonException("Invalid city.");
                _cities = cities; // Preserve every legacy row, even above the new-add limit.
            }
            else
            {
                _cities = defaults ?? [new("Москва", 55.7558, 37.6173), new("Минск", 53.9006, 27.5590), new("Токио", 35.6762, 139.6503)];
                Save(_cities);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadError = "Не удалось прочитать список городов. Проверьте cities.json и перезапустите расширение; файл не перезаписан.";
        }
    }

    public City[] Snapshot() { lock (_sync) return (City[])_cities.Clone(); }
    public bool Contains(City city) { lock (_sync) return _cities.Any(c => Key(c) == Key(city)); }
    public static string Key(City city) => string.Create(CultureInfo.InvariantCulture, $"{city.Latitude:F4}:{city.Longitude:F4}");
    public static bool IsValid(City city) => !string.IsNullOrWhiteSpace(city.Name)
        && double.IsFinite(city.Latitude) && double.IsFinite(city.Longitude)
        && city.Latitude is >= -90 and <= 90 && city.Longitude is >= -180 and <= 180;

    public string Add(City city)
    {
        lock (_sync)
        {
            EnsureWritable();
            if (!IsValid(city)) throw new ArgumentException("Некорректные координаты города.");
            if (_cities.Any(c => Key(c) == Key(city))) return "Город уже сохранён";
            if (_cities.Length >= MaximumCities) return $"Можно сохранить до {MaximumCities} городов. Удалите один из списка.";
            var next = _cities.Append(city).ToArray();
            Save(next);
            _cities = next;
        }
        Changed?.Invoke();
        return "Город добавлен в мои города";
    }

    public string Remove(City city)
    {
        lock (_sync)
        {
            EnsureWritable();
            var next = _cities.Where(c => Key(c) != Key(city)).ToArray();
            if (next.Length == _cities.Length) return "Город уже удалён";
            Save(next);
            _cities = next;
        }
        Changed?.Invoke();
        return "Город удалён из моих городов";
    }

    public string Move(City city, int offset)
    {
        lock (_sync)
        {
            EnsureWritable();
            var index = Array.FindIndex(_cities, c => Key(c) == Key(city));
            var target = index + offset;
            if (index < 0 || offset is not (-1 or 1) || target < 0 || target >= _cities.Length) return "Порядок не изменён";
            var next = (City[])_cities.Clone();
            (next[index], next[target]) = (next[target], next[index]);
            Save(next);
            _cities = next;
        }
        Changed?.Invoke();
        return "Порядок городов изменён";
    }

    private void EnsureWritable()
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
    }

    private void Save(City[] cities)
    {
        // Do not overwrite a configuration changed outside this extension instance.
        var current = File.Exists(_path) ? File.ReadAllText(_path) : null;
        if (current != _savedText) throw new IOException("Список изменён вне FairSky. Перезапустите расширение перед сохранением.");
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".cities-{Guid.NewGuid():N}.tmp");
        var text = JsonSerializer.Serialize(cities, WeatherJsonContext.Default.CityArray);
        try
        {
            File.WriteAllText(temporary, text);
            if (File.Exists(_path)) File.Replace(temporary, _path, _path + ".bak");
            else File.Move(temporary, _path);
            _savedText = text;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
