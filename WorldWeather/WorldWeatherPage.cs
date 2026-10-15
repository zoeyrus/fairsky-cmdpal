using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace WorldWeather;

internal sealed partial class WorldWeatherPage : ContentPage
{
    private readonly City _city;
    private readonly WeatherService _weather;
    private int _loadStarted;
    private string _markdown;

    public WorldWeatherPage(City city, WeatherService weather)
    {
        _city = city;
        _weather = weather;
        Title = $"Погода — {city.Name}";
        Name = city.Name;
        Id = $"worldweather.details.{city.Latitude.ToString("F4", CultureInfo.InvariantCulture)}.{city.Longitude.ToString("F4", CultureInfo.InvariantCulture)}";
        _markdown = $"# Погода — {_city.Name}\n\nЗагружаем прогноз от MET Norway…";
    }

    public override IContent[] GetContent()
    {
        // Returning immediately is important: never block Command Palette on HTTP.
        if (Interlocked.Exchange(ref _loadStarted, 1) == 0)
        {
            _ = LoadForecastAsync();
        }

        return [new MarkdownContent(_markdown)];
    }

    private async Task LoadForecastAsync()
    {
        try
        {
            var data = await _weather.GetAsync(_city).ConfigureAwait(false);
            _markdown = FormatForecast(data);
        }
        catch (Exception ex)
        {
            _markdown = $"# Погода — {_city.Name}\n\nНе удалось получить прогноз.\n\n" +
                $"Ошибка: `{ex.GetType().Name}: {ex.Message.Replace("`", "'")}`\n\n" +
                "Страница работает; проверь подключение к api.met.no.";
        }
        finally
        {
            // Ask Command Palette to request GetContent() again.
            RaiseItemsChanged();
        }
    }

    private string FormatForecast(WeatherData weather)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {WeatherService.Symbol(weather.Code)} Погода — {_city.Name}");
        sb.AppendLine();
        sb.AppendLine($"## {weather.Temperature:+0.#;-0.#;0} °C");
        sb.AppendLine();
        sb.AppendLine($"**{WeatherService.Description(weather.Code)}**");
        sb.AppendLine();
        sb.AppendLine($"- Ощущается как: **{weather.FeelsLike:+0.#;-0.#;0} °C**");
        sb.AppendLine($"- Влажность: **{weather.Humidity} %**");
        sb.AppendLine($"- Ветер: **{weather.Wind:0.#} м/с**");
        sb.AppendLine();
        sb.AppendLine("## Прогноз по дням");
        sb.AppendLine();
        sb.AppendLine("| Дата (UTC) | Мин. | Макс. | Условия |");
        sb.AppendLine("|---|---:|---:|---|");
        foreach (var day in weather.Days)
        {
            sb.AppendLine($"| {day.Date} | {day.Min:0.#} °C | {day.Max:0.#} °C | {WeatherService.Description(day.Code)} |");
        }
        sb.AppendLine();
        sb.AppendLine($"Источник: MET Norway · Прогноз на {weather.Updated}");
        if (weather.Stale)
            sb.AppendLine("\n*Нет свежих данных: показан кешированный прогноз.*");
        return sb.ToString();
    }
}
