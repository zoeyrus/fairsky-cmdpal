using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.IO;

namespace FairSky;

public partial class FairSkyCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;
    private readonly FairSkyHomePage _home;
    private readonly FairSkyDockPage _dock;

    public FairSkyCommandsProvider()
    {
        DisplayName = "FairSky";
        Id = "org.fairsky.cmdpal";
        var weather = new WeatherService();
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FairSky", "cities.json");
        var store = new CityStore(path);
        _home = new FairSkyHomePage(store, weather, new CitySearchService());
        _dock = new FairSkyDockPage(store, weather);
        _commands = [new CommandItem(_home) { Title = "FairSky", Subtitle = "Поиск городов и прогноз погоды" }];
        _bands = [new CommandItem(_dock) { Title = "fairsky" }];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
    public override ICommandItem[]? GetDockBands() => _bands;

    public override void Dispose()
    {
        _home.Dispose();
        _dock.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
