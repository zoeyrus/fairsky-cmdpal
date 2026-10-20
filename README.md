# FairSky

**An open-source weather extension for Microsoft PowerToys Command Palette and Dock.**

Check the weather in your saved cities from a compact Command Palette list, or keep city weather cards visible in Dock. Select a city to open a detailed forecast with temperature, humidity, wind and an outlook covering up to four UTC calendar days.

## Available now

- A searchable home page listing your configured cities.
- Native Dock cards with a weather icon, temperature, city name and short condition description.
- Detailed forecasts opened from either the home page or Dock.
- Outlined color weather icons shared across Dock, the city list and forecast pages, designed for light and dark themes.
- Forecast data from [MET Norway Locationforecast](https://api.met.no/weatherapi/locationforecast/2.0/documentation), with no weather API key required.
- Shared in-memory caching; when a refresh fails, an available cached forecast is retained and marked as stale.
- Celsius temperatures, wind in m/s and Russian interface text.

## Project status

**Early preview — FairSky 0.0.7.4.** The x64 development package has been built, signed, installed and checked in the actual Command Palette environment. The user verified the home page and detailed forecast in light/dark themes, confirmed the updated outlined icons, and confirmed cached data remained available offline and refreshed after connectivity returned.

The latest implementation passed 108 automated assertions covering card states, temperature formatting, SDK notifications, navigation, disposal and bundled weather images. These are local checks; GitHub Actions did not run checks for PR #3.

A public, trusted installer has not been released. Local development signing is required. The build can report a missing `mspdbcmf.exe` warning: this prevents generating a symbols package, but the tested MSIX build and signature verification succeeded.

### Current limitations

- Search on the home page filters configured cities; it does not yet search for new cities worldwide.
- Cities are configured through `%LOCALAPPDATA%\FairSky\cities.json`; there is no in-app add/remove interface yet.
- All configured cities currently appear in Dock. Selecting a separate Dock subset and changing its order are planned.
- The forecast cache is in memory and does not survive an extension restart. Offline display requires an earlier successful forecast during the same session.
- Dock controls card dimensions and typography, so long names or conditions may be shortened by the host.
- Daily forecasts are grouped by UTC date, rather than the city's local date.

## Next development streams

These features are planned and are not included in 0.0.7.4.

### 1. City search and management

Search for cities such as New York, Berlin or London, distinguish results by country/region, and open a forecast without saving the city. Provide an explicit action to add a city to the saved list, plus removal and reordering. Preserve existing city configuration when upgrading.

Worldwide city lookup will need a geocoding source; weather forecasts will continue to use MET Norway.

### 2. Cities shown in Dock

Choose which saved cities appear in Dock and their order, including the option to show none. Removing a city from Dock should not remove it from the saved list. The number of selected cities determines the number of cards.

## Build and check

Requirements: Windows with PowerToys Command Palette, .NET 10 SDK, Windows SDK 10.0.26100, and the dependencies declared by the project. The tested extension SDK version is `Microsoft.CommandPalette.Extensions` 0.9.260303001.

Open `FairSky.code-workspace` in Windows VS Code. Use **Ctrl+Shift+B** for **FairSky: Build Debug x64**, or **Terminal → Run Task → FairSky: Check Dock** for the automated checks. For Git and pull requests without manually entering commands, see the [VS Code workflow guide (Russian)](docs/github-workflow.ru.md).

Alternatively, from the repository root:

```powershell
dotnet build FairSky/FairSky.csproj -c Debug -r win-x64 -p:Platform=x64 -p:AppxPackageSigningEnabled=false
dotnet run --project tests/FairSky.Tests/FairSky.Tests.csproj -p:Platform=x64 -p:AppxPackageSigningEnabled=false
```

To generate an unsigned MSIX for subsequent local signing:

```powershell
dotnet build FairSky/FairSky.csproj -c Debug -r win-x64 -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false
```

Sign the MSIX with your trusted local development certificate before installation. Never commit signing keys, PFX files or private user configuration. A successful build alone does not verify the visible Command Palette interface.

## Weather data attribution

Forecast data: [Norwegian Meteorological Institute (MET Norway)](https://api.met.no/). Respect the API's identification, caching and attribution requirements. Forecast values are modeled data and may differ from observed conditions.

## Privacy

FairSky requests forecasts for configured latitude/longitude coordinates from `api.met.no`. The current preview does not require an account. Weather icons are bundled locally and do not make image requests. A fuller privacy statement will accompany the public release.

## License and contributing

MIT — see [LICENSE](LICENSE). Third-party components retain their respective licenses and notices. Issues, bug reports, translations and pull requests are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).
