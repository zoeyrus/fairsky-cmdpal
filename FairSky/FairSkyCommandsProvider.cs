using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FairSky;

public partial class FairSkyCommandsProvider : CommandProvider
{
    private readonly ICommandItem[] _commands;
    private readonly ICommandItem[] _bands;
    private readonly List<FairSkyDockItem> _items = [];

    public FairSkyCommandsProvider()
    {
        DisplayName = "FairSky";
        Id = "org.fairsky.cmdpal";
        var service = new WeatherService();
        var config = LoadCities();
        var homePage = new FairSkyHomePage(config, service);
        var commands = new List<ICommandItem>
        {
            new CommandItem(homePage)
            {
                Title = "FairSky",
                Subtitle = "Погода в ваших городах"
            }
        };
        foreach (var city in config)
        {
        _items.Add(new FairSkyDockItem(city, service));
        }
        _commands = commands.ToArray();
        _bands = [new WrappedDockItem(_items.Cast<IListItem>().ToArray(), "org.fairsky.cmdpal.cities", "fairsky")];
    }

    public override ICommandItem[] TopLevelCommands() => _commands;
    public override ICommandItem[]? GetDockBands() => _bands;

    private static City[] LoadCities()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FairSky");
        string path = Path.Combine(dir, "cities.json");
        Directory.CreateDirectory(dir);
        if (!File.Exists(path))
        {
            var defaults = new[] { new City("Москва", 55.7558, 37.6173), new City("Минск", 53.9006, 27.5590), new City("Токио", 35.6762, 139.6503) };
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, WeatherJsonContext.Default.CityArray));
            return defaults;
        }
        try { return JsonSerializer.Deserialize(File.ReadAllText(path), WeatherJsonContext.Default.CityArray)?.Take(12).ToArray() ?? []; }
        catch { return [new City("Москва", 55.7558, 37.6173)]; }
    }
}
