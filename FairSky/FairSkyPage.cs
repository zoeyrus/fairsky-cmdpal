using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace FairSky;

internal sealed partial class FairSkyPage : ContentPage
{
    private readonly City _city;
    private readonly WeatherService _weather;
    private readonly CityStore? _store;
    private int _loadStarted;
    private string _markdown;

    public FairSkyPage(City city, WeatherService weather, CityStore? store = null)
    {
        _city = city;
        _weather = weather;
        _store = store;
        RefreshCommands();
        Title = $"Погода — {city.Name}";
        Name = city.Name;
        Id = $"fairsky.details.{city.Latitude.ToString("F4", CultureInfo.InvariantCulture)}.{city.Longitude.ToString("F4", CultureInfo.InvariantCulture)}";
        _markdown = $"# Погода — {_city.Name}\n\nЗагружаем прогноз от MET Norway…";
    }

    private void RefreshCommands()
    {
        if (_store is not null) Commands = CityActions.For(_city, _store, RefreshCommands);
    }

    public override IContent[] GetContent()
    {
        RefreshCommands();
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
            Icon = WeatherIcons.Create(data.Code);
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
    var culture = CultureInfo.GetCultureInfo("ru-RU");
    var sb = new StringBuilder();

    sb.AppendLine(culture, $"# {WeatherIcons.Markdown(weather.Code)} Погода — {_city.Name}");
    sb.AppendLine();
    sb.AppendLine(culture, $"## {weather.Temperature:+0.#;-0.#;0} °C");
    sb.AppendLine();
    sb.AppendLine(culture, $"**{WeatherService.Description(weather.Code)}**");
    sb.AppendLine();
    sb.AppendLine(culture, $"- Ощущается как: **{weather.FeelsLike:+0.#;-0.#;0} °C**");
    sb.AppendLine(culture, $"- Влажность: **{weather.Humidity} %**");
    sb.AppendLine(culture, $"- Ветер: **{weather.Wind:0.#} м/с**");
    sb.AppendLine();
    sb.AppendLine("## Прогноз по дням");
    sb.AppendLine();
    sb.AppendLine("| Дата (UTC) | Мин. | Макс. | Условия |");
    sb.AppendLine("|---|---:|---:|---|");

    foreach (var day in weather.Days)
    {
        sb.AppendLine(culture, $"| {day.Date} | {day.Min:0.#} °C | {day.Max:0.#} °C | {WeatherService.Description(day.Code)} |");
    }

    sb.AppendLine();
    sb.AppendLine(culture, $"Источник: MET Norway · Прогноз на {weather.Updated}");

    if (weather.Stale)
        sb.AppendLine("\n*Нет свежих данных: показан кешированный прогноз.*");

    return sb.ToString();
}

}
