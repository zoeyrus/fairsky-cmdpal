using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using FairSky;
using Microsoft.CommandPalette.Extensions.Toolkit;

var city = new City("Москва", 55.7558, 37.6173);
var weather = new WeatherData(12, 10, 60, 3, 0, "2026-10-19T12:00:00Z", []);
int checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new InvalidOperationException(message);
    checks++;
}

// A user's Windows locale must not alter signed temperatures or city names.
foreach (var culture in new[] { "ru-RU", "en-US", "ar-SA" })
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
    Check(DockWeatherPresentation.FromWeather(city, weather).Title == "+12° · Москва", "Positive temperature and city");
    Check(DockWeatherPresentation.FromWeather(city, weather with { Temperature = -3 }).Title == "−3° · Москва", "Negative temperature");
    Check(DockWeatherPresentation.FromWeather(city, weather with { Temperature = -0.1 }).Title == "0° · Москва", "Rounded zero must not have a sign");
}
var stale = DockWeatherPresentation.FromWeather(city, weather with { Stale = true });
Check(stale.Title.Contains("+12°") && stale.Subtitle.StartsWith("Устарело", StringComparison.Ordinal), "Offline snapshot keeps temperature and shows stale state first");
Check(!DockWeatherPresentation.FromWeather(city, weather).Subtitle.Contains("Устарело"), "Recovery clears stale state");
Check(DockWeatherPresentation.Loading(city).Title == city.Name, "Loading keeps city visible");
Check(DockWeatherPresentation.Unavailable(city).Title == city.Name, "Failure keeps city visible");
Check(DockWeatherPresentation.Unavailable(city).Subtitle.Contains("MET Norway"), "Failure names actual provider");
var longCity = city with { Name = "Петропавловск-Камчатский" };
Check(DockWeatherPresentation.FromWeather(longCity, weather).Title.EndsWith(longCity.Name, StringComparison.Ordinal), "Extension must not truncate city names");

// Asset resolution must work from the built output, including all weather groups.
foreach (int code in new[] { 0, 1, 2, 3, 45, 48, 51, 53, 55, 56, 57, 61, 63, 65, 66, 67, 71, 73, 75, 77, 80, 81, 82, 85, 86, 95, 96, 99, -1 })
{
    string source = WeatherIcons.Source(code);
    Check(System.IO.File.Exists(source), $"Packaged weather image missing for {code}");
    var bytes = System.IO.File.ReadAllBytes(source);
    Check(bytes.Length > 8 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71, "Weather asset must be a PNG");
    var uri = new Uri(source).AbsoluteUri;
    Check(WeatherIcons.Markdown(code).Contains(uri, StringComparison.Ordinal), "Details and Dock must use the same local artwork");
}
Check(DockWeatherPresentation.FromWeather(city, weather).IconSource == WeatherIcons.Source(weather.Code), "Dock uses packaged artwork");
Check(DockWeatherPresentation.FromWeather(city, weather with { Stale = true }).IconSource == WeatherIcons.Source(weather.Code), "Offline snapshot retains its weather artwork");

// Check actual installed SDK notifications, not a substitute ListItem implementation.
var item = new ListItem(new NoOpCommand());
var changes = new List<string>();
item.PropChanged += (_, args) => changes.Add(args.PropertyName);
item.Title = "test";
item.Subtitle = "condition";
item.Icon = new IconInfo("☀");
Check(changes.Contains("Title") && changes.Contains("Subtitle") && changes.Contains("Icon"), "Installed SDK must notify all three native fields");

// Disable scheduling for an isolated test: no HTTP or user configuration access.
using var dock = new FairSkyDockItem(city, new WeatherService(), startRefresh: false);
Check(dock.Command is FairSkyPage && !string.IsNullOrEmpty(dock.Command.Id), "Details navigation ID preserved");
Check(dock.Title == city.Name, "Dock appears synchronously in loading state");
int notifications = 0;
dock.PropChanged += (_, _) => notifications++;
dock.Dispose();
dock.Dispose();
var refresh = typeof(FairSkyDockItem).GetMethod("RefreshAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
await (Task)refresh.Invoke(dock, null)!;
Check(notifications == 0, "Disposed Dock must not update");
Console.WriteLine($"PASS: {checks} checks; SDK notifications, card states, navigation and disposal.");
checks += await CityManagementChecks.RunAsync();
Console.WriteLine($"PASS TOTAL: {checks} assertions.");

if (Array.Exists(args, a => a == "--live-search"))
{
    var geocoding = new CitySearchService();
    var forecasts = new WeatherService();
    foreach (var query in new[] { "Нью-Йорк", "Берлин", "Лондон" })
    {
        var results = await geocoding.SearchAsync(query, System.Threading.CancellationToken.None);
        if (results.Length == 0) throw new InvalidOperationException("Live search returned no results for " + query);
        var selected = results[0];
        var forecast = await forecasts.GetAsync(selected);
        if (!double.IsFinite(forecast.Temperature) || forecast.Days.Length == 0)
            throw new InvalidOperationException("Invalid live forecast for " + query);
        Console.WriteLine($"LIVE PASS: {query} → {selected.Name}, {selected.Country}; {forecast.Temperature} C; {forecast.Days.Length} days");
    }
}
