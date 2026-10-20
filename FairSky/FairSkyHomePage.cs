using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FairSky;

internal sealed partial class FairSkyHomePage : DynamicListPage, IDisposable
{
    private readonly CityStore _store;
    private readonly WeatherService _weather;
    private readonly ICitySearchService _search;
    private readonly TimeSpan _debounce;
    private readonly bool _refreshWeather;
    private readonly object _sync = new();
    private Dictionary<string, (City City, ListItem Item)> _saved = new();
    private IListItem[] _items = [];
    private City[] _results = [];
    private string _query = "";
    private string _status = "";
    private CancellationTokenSource? _searchCancellation;
    private int _generation;
    private bool _disposed;
    private DateTimeOffset _nextRefresh = DateTimeOffset.MinValue;
    private int _refreshing;
    internal Task PendingSearch { get; private set; } = Task.CompletedTask;

    public FairSkyHomePage(CityStore store, WeatherService weather, ICitySearchService search, TimeSpan? debounce = null, bool refreshWeather = true)
    {
        _store = store;
        _weather = weather;
        _search = search;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(350);
        _refreshWeather = refreshWeather;
        Id = "org.fairsky.home";
        Title = "FairSky";
        Name = "Города";
        PlaceholderText = "Найти город: Нью-Йорк, Берлин, Лондон…";
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Title = "Нет сохранённых городов",
            Subtitle = "Введите название города. Откройте прогноз или сохраните через меню «Ещё»."
        };
        _store.Changed += OnCitiesChanged;
        OnCitiesChanged();
    }

    public override IListItem[] GetItems()
    {
        IListItem[] items;
        (City City, ListItem Item)[] refresh = [];
        lock (_sync)
        {
            items = _items;
            if (!_disposed && _refreshWeather && DateTimeOffset.UtcNow >= _nextRefresh && Interlocked.CompareExchange(ref _refreshing, 1, 0) == 0)
            {
                _nextRefresh = DateTimeOffset.UtcNow.AddMinutes(15);
                refresh = _saved.Values.ToArray();
                if (refresh.Length == 0) Volatile.Write(ref _refreshing, 0);
            }
        }
        if (refresh.Length > 0) _ = RefreshSavedAsync(refresh);
        return items;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        CancellationTokenSource? previous;
        lock (_sync)
        {
            if (_disposed) return;
            previous = _searchCancellation;
            _searchCancellation = null;
            _generation++;
            _query = newSearch.Trim();
            _results = [];
            _status = _query.Length == 0 ? "" : _query.Length < 2 ? "Введите минимум 2 символа" : "Поиск городов…";
            IsLoading = _query.Length >= 2;
            BuildItems();
            if (_query.Length >= 2)
            {
                _searchCancellation = new CancellationTokenSource();
                PendingSearch = SearchAsync(_query, _generation, _searchCancellation.Token);
            }
            else PendingSearch = Task.CompletedTask;
        }
        previous?.Cancel();
        previous?.Dispose();
        RaiseItemsChanged();
    }

    private async Task SearchAsync(string query, int generation, CancellationToken token)
    {
        City[] results = [];
        string status = "";
        try
        {
            await Task.Delay(_debounce, token).ConfigureAwait(false);
            results = await _search.SearchAsync(query, token).ConfigureAwait(false);
            if (results.Length == 0) status = "Город не найден. Уточните название или страну.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (ArgumentException ex) { status = ex.Message; }
        catch { status = "Поиск недоступен. Проверьте интернет; сохранённые города остаются в списке."; }
        lock (_sync)
        {
            if (_disposed || generation != _generation || token.IsCancellationRequested) return;
            _results = results;
            _status = status;
            IsLoading = false;
            BuildItems();
        }
        RaiseItemsChanged();
    }

    private void OnCitiesChanged()
    {
        lock (_sync)
        {
            if (_disposed) return;
            var next = new Dictionary<string, (City City, ListItem Item)>();
            foreach (var city in _store.Snapshot())
            {
                string key = CityStore.Key(city);
                var item = _saved.TryGetValue(key, out var existing) ? existing.Item : new ListItem(new FairSkyPage(city, _weather, _store))
                {
                    Title = city.Name,
                    Subtitle = "Загрузка погоды…",
                    Section = "Мои города"
                };
                item.MoreCommands = CityActions.For(city, _store);
                next.TryAdd(key, (city, item));
            }
            _saved = next;
            _nextRefresh = DateTimeOffset.MinValue;
            BuildItems();
        }
        RaiseItemsChanged();
    }

    // Called while _sync is held. Property changes on items are handled by Toolkit.
    private void BuildItems()
    {
        var items = new List<IListItem>();
        if (_store.LoadError is { } error) items.Add(Status(error));
        foreach (var city in _store.Snapshot())
        {
            if (_saved.TryGetValue(CityStore.Key(city), out var entry) && (Matches(city, _query) || _results.Any(r => CityStore.Key(r) == CityStore.Key(city)))) items.Add(entry.Item);
        }
        foreach (var city in _results)
        {
            if (_saved.ContainsKey(CityStore.Key(city))) continue;
            items.Add(new ListItem(new FairSkyPage(city, _weather, _store))
            {
                Title = city.Name,
                Subtitle = Location(city) + " · Прогноз: Enter · Сохранить: меню «Ещё»",
                Icon = new IconInfo("\uE707"),
                Section = "Поиск · Open-Meteo / GeoNames",
                MoreCommands = CityActions.For(city, _store)
            });
        }
        if (!string.IsNullOrEmpty(_status)) items.Add(Status(_status));
        _items = items.ToArray();
    }

    private static ListItem Status(string text) => new(new NoOpCommand()) { Title = text, Icon = new IconInfo("\uE946") };
    internal static string Location(City city) => string.Join(" · ", new[] { city.Country, city.Region }.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct());
    private static bool Matches(City city, string query) => query.Length == 0 || city.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
        || Location(city).Contains(query, StringComparison.OrdinalIgnoreCase);

    private async Task RefreshSavedAsync((City City, ListItem Item)[] entries)
    {
        try { await Task.WhenAll(entries.Select(RefreshSavedCityAsync)).ConfigureAwait(false); }
        finally
        {
            Volatile.Write(ref _refreshing, 0);
            bool notify;
            lock (_sync) notify = !_disposed;
            if (notify) RaiseItemsChanged();
        }
    }

    private async Task RefreshSavedCityAsync((City City, ListItem Item) entry)
    {
        WeatherData? data = null;
        try { data = await _weather.GetAsync(entry.City).ConfigureAwait(false); }
        catch { /* Preserve a useful error state without breaking other cities. */ }
        lock (_sync)
        {
            if (_disposed || !_saved.TryGetValue(CityStore.Key(entry.City), out var current) || !ReferenceEquals(current.Item, entry.Item)) return;
            var location = Location(entry.City);
            var suffix = location.Length > 0 ? " · " + location : "";
            if (data is not null)
            {
                entry.Item.Icon = WeatherIcons.Create(data.Code);
                entry.Item.Subtitle = data.Temperature.ToString("+0;-0;0", CultureInfo.GetCultureInfo("ru-RU"))
                    + " °C · " + WeatherService.Description(data.Code) + (data.Stale ? " · Нет свежих данных" : "") + suffix;
            }
            else entry.Item.Subtitle = "Не удалось загрузить погоду" + suffix;
        }
        RaiseItemsChanged();
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            ++_generation;
            cancellation = _searchCancellation;
            _searchCancellation = null;
        }
        _store.Changed -= OnCitiesChanged;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }
}
