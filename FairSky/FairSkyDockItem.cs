using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FairSky;

internal sealed partial class FairSkyDockItem : ListItem, IDisposable
{
    private readonly City _city;
    private readonly WeatherService _service;
    private readonly Timer _timer;
    private int _updating;

    public FairSkyDockItem(City city, WeatherService service) : base(new FairSkyPage(city, service))
    {
        _city = city;
        _service = service;
        Title = $"{city.Name}: …";
        Subtitle = "Погода загружается";
        _timer = new Timer(_ => _ = RefreshAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(20));
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _updating, 1) != 0) return;
        try
        {
            var d = await _service.GetAsync(_city);
            Title = $"{WeatherService.Symbol(d.Code)} {_city.Name} {d.Temperature:+0;-0;0}°";
            Subtitle = $"{WeatherService.Description(d.Code)} · ощущается {d.FeelsLike:+0;-0;0}°" + (d.Stale ? " · офлайн" : "");
        }
        catch { Subtitle = "Нет соединения с Open-Meteo"; }
        finally { Volatile.Write(ref _updating, 0); }
    }

    public void Dispose() => _timer.Dispose();
}
