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
    private readonly object _lifetime = new();
    private int _updating;
    private bool _disposed;

    public FairSkyDockItem(City city, WeatherService service, bool startRefresh = true) : base(new FairSkyPage(city, service))
    {
        _city = city;
        _service = service;
        Apply(DockWeatherPresentation.Loading(city));
        _timer = new Timer(_ => _ = RefreshAsync(), null, startRefresh ? TimeSpan.Zero : Timeout.InfiniteTimeSpan, TimeSpan.FromMinutes(20));
    }

    private async Task RefreshAsync()
    {
        if (Interlocked.Exchange(ref _updating, 1) != 0) return;
        try
        {
            lock (_lifetime)
            {
                if (_disposed) return;
            }

            var data = await _service.GetAsync(_city).ConfigureAwait(false);
            Apply(DockWeatherPresentation.FromWeather(_city, data));
        }
        catch
        {
            // WeatherService returns a stale snapshot when cached data is available.
            Apply(DockWeatherPresentation.Unavailable(_city));
        }
        finally { Volatile.Write(ref _updating, 0); }
    }

    private void Apply(DockWeatherPresentation presentation)
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            // Toolkit setters raise PropChanged; no polling or list rebuild is needed.
            Icon = new IconInfo(presentation.Symbol);
            Title = presentation.Title;
            Subtitle = presentation.Subtitle;
        }
    }

    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            _disposed = true;
            _timer.Dispose();
        }
    }
}
