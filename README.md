# FairSky

**An open-source weather extension for Microsoft PowerToys Command Palette and Dock.**

FairSky displays weather forecasts for multiple cities in Command Palette Dock. Select a city to open a detailed forecast with temperature, humidity, wind, and a four-day outlook.

## Features

- Multiple pinned city weather items in Command Palette Dock
- Detailed city forecasts in Command Palette
- Forecast data from [MET Norway Locationforecast](https://api.met.no/weatherapi/locationforecast/2.0/documentation)
- No weather API key required
- Celsius and m/s readouts in the current prototype

## Project status

**Early preview (based on the working WorldWeather 0.0.4.0 prototype).** This repository is being prepared for an open-source release. The first public installation package has not yet been verified or published. The initial UI uses Russian strings. Internationalization, city management, packaging and distribution are on the roadmap.

## Build from source

Requirements: Windows with PowerToys Command Palette, .NET 10 SDK, Windows SDK 10.0.26100, and the project-specific Command Palette extension dependencies.

From the extension project directory:

```powershell
dotnet build -c Debug -r win-x64 -p:Platform=x64 -p:GenerateAppxPackageOnBuild=true
```

The output MSIX must be signed with your own trusted development certificate before local installation. **Never commit signing keys or PFX files.** Consult the Microsoft documentation for Command Palette extension development and MSIX signing.

## Weather data attribution

Forecast data: [Norwegian Meteorological Institute (MET Norway)](https://api.met.no/). Respect the API's identification, caching and attribution requirements. Forecast values are modeled data and may differ from observed conditions.

## Privacy

FairSky requests forecasts for configured latitude/longitude coordinates from api.met.no. The current prototype does not require an account. A fuller privacy statement will accompany the public release.

## License

MIT — see [LICENSE](LICENSE). Third-party components retain their respective licenses and notices.

## Contributing

Issues, bug reports, translations, and pull requests are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).
