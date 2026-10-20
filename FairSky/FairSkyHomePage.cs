
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace FairSky;

internal sealed partial class FairSkyHomePage : ListPage
{
    private readonly City[] _cityConfig;
    private readonly WeatherService _service;
    private readonly ListItem[] _cities;

    private DateTimeOffset _nextRefresh = DateTimeOffset.MinValue;
    private int _refreshing;

    public FairSkyHomePage(City[] cities, WeatherService service)
    {
        Id = "org.fairsky.home";
        Title = "FairSky";
        Name = "Города";

        _cityConfig = cities;
        _service = service;
        _cities = new ListItem[cities.Length];

        for (int i = 0; i < cities.Length; i++)
        {
            var city = cities[i];

            _cities[i] = new ListItem(new FairSkyPage(city, service))
            {
                Title = city.Name,
                Subtitle = "Загрузка погоды..."
            };
        }
    }

    public override IListItem[] GetItems()
    {
        if (DateTimeOffset.UtcNow >= _nextRefresh &&
            Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
        {
            _nextRefresh = DateTimeOffset.UtcNow.AddMinutes(15);
            _ = RefreshWeatherAsync();
        }

        return _cities;
    }

    private async Task RefreshWeatherAsync()
    {
        try
        {
            var tasks = new Task[_cityConfig.Length];

            for (int i = 0; i < _cityConfig.Length; i++)
            {
                tasks[i] = LoadWeatherAsync(
                    _cityConfig[i],
                    _cities[i]);
            }

            await Task.WhenAll(tasks);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    private async Task LoadWeatherAsync(City city, ListItem item)
    {
        try
        {
            var weather = await _service.GetAsync(city);

            var culture = CultureInfo.GetCultureInfo("ru-RU");

            string temperature = weather.Temperature.ToString(
                "+0;-0;0", culture);

            item.Icon = WeatherIcons.Create(weather.Code);
            string description = WeatherService.Description(weather.Code);

            item.Subtitle =
                $"{temperature} °C · {description}" +
                (weather.Stale ? " · Нет свежих данных" : "");
        }
        catch
        {
            item.Subtitle = "Не удалось загрузить погоду";
        }

        RaiseItemsChanged();
    }
}
