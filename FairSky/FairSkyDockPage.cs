using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace FairSky;

// Keep the previous band's command ID so existing Dock pins continue to resolve.
internal sealed partial class FairSkyDockPage : ListPage, IDisposable
{
    private readonly CityStore _store;
    private readonly WeatherService _weather;
    private readonly object _sync = new();
    private readonly bool _startRefresh;
    private Dictionary<string, FairSkyDockItem> _cities = new();
    private IListItem[] _items = [];
    private bool _disposed;

    public FairSkyDockPage(CityStore store, WeatherService weather, bool startRefresh = true)
    {
        _store = store;
        _weather = weather;
        _startRefresh = startRefresh;
        Id = "org.fairsky.cmdpal.cities";
        Name = "fairsky";
        Title = "FairSky";
        _store.Changed += RefreshCities;
        RefreshCities();
    }

    public override IListItem[] GetItems() { lock (_sync) return _items; }

    private void RefreshCities()
    {
        lock (_sync)
        {
            if (_disposed) return;
            var next = new Dictionary<string, FairSkyDockItem>();
            foreach (var city in _store.Snapshot())
            {
                string key = CityStore.Key(city);
                if (next.ContainsKey(key)) continue;
                next[key] = _cities.TryGetValue(key, out var item) ? item : new FairSkyDockItem(city, _weather, _startRefresh, _store);
                next[key].MoreCommands = CityActions.For(city, _store);
            }
            foreach (var entry in _cities) if (!next.ContainsKey(entry.Key)) entry.Value.Dispose();
            _cities = next;
            _items = next.Values.Cast<IListItem>().ToArray();
        }
        RaiseItemsChanged();
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var city in _cities.Values) city.Dispose();
        }
        _store.Changed -= RefreshCities;
    }
}
