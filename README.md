# FairSky

**An open-source weather extension for Microsoft PowerToys Command Palette and Dock.**

Check the weather in your saved cities from a compact Command Palette list, or keep city weather cards visible in Dock. Select a city to open a detailed forecast with temperature, humidity, wind and an outlook covering up to four UTC calendar days.

## Available in the 0.0.8.0 development build

- Search for cities worldwide from the FairSky search box, with country/region labels.
- Open a forecast without saving the city, or use the **More / Ещё** menu to add it to your cities.
- Remove saved cities and move them up/down from the same menu. Changes are saved locally and update the Dock list.
- Native Dock cards with a weather icon, temperature, city name and short condition description.
- Detailed forecasts opened from either the home page or Dock.
- Outlined color weather icons shared across Dock, the city list and forecast pages, designed for light and dark themes.
- Forecast data from [MET Norway Locationforecast](https://api.met.no/weatherapi/locationforecast/2.0/documentation), with no weather API key required.
- Shared in-memory caching; when a refresh fails, an available cached forecast is retained and marked as stale.
- Celsius temperatures, wind in m/s and Russian interface text.

## Project status

**Development preview — FairSky 0.0.8.0.** City search and management are implemented in this branch. The previous 0.0.7.4 build was visually accepted by the user, including outlined icons and offline weather recovery. The new management interface still requires acceptance in the actual Command Palette UI.

The implementation passed 149 local automated assertions. Live checks found New York, Berlin and London through Open-Meteo Geocoding and retrieved forecasts for all three from MET Norway, without changing the user's saved cities. Local tests cover legacy configuration, persistence, duplicates, ordering, corruption/external-edit protection, search errors, stale responses and Dock notifications.

A public, trusted installer has not been released. Local development signing is required. The build can report a missing `mspdbcmf.exe` warning: this prevents generating a symbols package, but the tested MSIX build and signature verification succeeded.

### City search and saved cities

Open FairSky and type one city name at a time (at least two characters). Search starts after a short pause. Country and region help distinguish results. **Enter** opens the forecast without saving; **More / Ещё → Добавить в мои города** saves it. The forecast page also exposes the save/remove actions. Clear the search box to return to your saved list.

Saved cities offer **Удалить из моих городов**, **Переместить выше** and **Переместить ниже**. Up to 12 cities can be added; repeated coordinates are not added twice. Existing files with more entries are retained without truncation. Removing all cities keeps an empty list after restart.

Configuration remains an array in `%LOCALAPPDATA%\FairSky\cities.json`. Old name/latitude/longitude entries still load; new results can additionally store country and region. Updates replace the file atomically and keep the previous version in `cities.json.bak`. A malformed file or a detected external edit is not overwritten: fix the file and reload the extension before further edits.

### Current limitations

- Every saved city still appears in Dock; a separate selection and ordering for Dock belongs to the next sprint.
- Worldwide city search requires internet access. Previously successful search queries are cached briefly in memory; saved cities remain available when search fails.
- Weather caching is in memory and does not survive an extension restart. Offline forecasts require an earlier successful fetch in the same session.
- City identity currently uses coordinates rounded to four decimal places. Different geocoding points for the same named city can be separate entries.
- Dock controls dimensions and typography, so long labels may be shortened by the host.
- Daily forecasts are grouped by UTC date, rather than the city's local date.

## Next development stream: cities shown in Dock

Choose which saved cities appear in Dock and their order, including the option to show none. Removing a city from Dock should not remove it from the saved list. The number of selected cities determines the number of cards. This separate Dock selection is not part of 0.0.8.0.

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

## City search attribution

City lookup uses [Open-Meteo Geocoding](https://open-meteo.com/en/docs/geocoding-api), with location data from [GeoNames](https://www.geonames.org/). This is separate from weather forecasts, which continue to use MET Norway. The current personal/non-commercial preview uses the public endpoint without an API key; commercial use must follow the provider's terms.

## Weather data attribution

Forecast data: [Norwegian Meteorological Institute (MET Norway)](https://api.met.no/). Respect the API's identification, caching and attribution requirements. Forecast values are modeled data and may differ from observed conditions.

## Privacy

FairSky sends typed city search queries to `geocoding-api.open-meteo.com` and requests forecasts for selected latitude/longitude coordinates from `api.met.no`. Merely viewing a search result does not save it to the city list. The current preview does not require an account. Weather icons are bundled locally and do not make image requests. A fuller privacy statement will accompany the public release.

## License and contributing

MIT — see [LICENSE](LICENSE). Third-party components retain their respective licenses and notices. Issues, bug reports, translations and pull requests are welcome; see [CONTRIBUTING.md](CONTRIBUTING.md).
