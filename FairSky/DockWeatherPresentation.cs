using System.Globalization;

namespace FairSky;

// Native Dock owns typography, dimensions and theme colors. Keep both lines short.
internal sealed record DockWeatherPresentation(string Title, string Subtitle, string IconSource)
{
    public static DockWeatherPresentation Loading(City city) =>
        new(city.Name, "Загрузка погоды…", "\uE895");

    public static DockWeatherPresentation Unavailable(City city) =>
        new(city.Name, "MET Norway недоступен", "\uE783");

    public static DockWeatherPresentation FromWeather(City city, WeatherData weather)
    {
        var temperature = weather.Temperature.ToString("+0;−0;0", CultureInfo.InvariantCulture);
        var condition = WeatherService.Description(weather.Code);
        return new(
            $"{temperature}° · {city.Name}",
            weather.Stale ? $"Устарело · {condition}" : condition,
            WeatherIcons.Source(weather.Code));
    }
}
