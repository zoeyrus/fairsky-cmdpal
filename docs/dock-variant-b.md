# Dock variant B

## Native rendering

Uses the installed `Microsoft.CommandPalette.Extensions` 0.9.260303001 package.
Each existing `ListItem` now has a separate weather `Icon`, a temperature-first
`Title` (`+12° · Москва`) and a short condition `Subtitle` (`Ясно`).
Version 0.0.7.4 replaces font-dependent emoji with original local color artwork.
Nine PNG icons share a dark contour and a narrow light outer line for light/dark
surfaces. Editable SVG sources are in `FairSky/Assets/Weather`. The same images
are used by Dock, the native home-list icon slot, and the forecast heading.
No image downloads or additional HTTP clients are needed.
Loading and unavailable states use native Segoe glyphs. No custom window or renderer.

Font size, colors, line layout and truncation are controlled by the Dock host;
this SDK does not provide per-card font-size or arbitrary layout properties.
Long city names are retained in the data; the host may ellipsize them.
`Title`, `Subtitle` and `Icon` setters raise property notifications in the installed
SDK, verified by executing the test harness against the actual Toolkit.
The current upstream host uses theme resources and title/subtitle text blocks:
https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/Microsoft.CmdPal.UI/Dock/DockItemControl.xaml
Upstream source is explanatory, not proof of the installed host's visual behavior.

## Behavior and lifetime

- Immediate loading state; the same shared weather service and 20-minute timer interval.
- Cached stale snapshots retain temperature and put `Устарело` first in the subtitle.
- Without a snapshot, the city stays visible and the error identifies MET Norway.
- Clicking still uses the existing `FairSkyPage` with its existing ID.
- Provider and extension disposal now dispose Dock timers. Queued callbacks and
  in-flight completions cannot publish card updates after disposal.
- WeatherService, forecast text, COM IDs and cities.json schema are unchanged;
  home/details changes are limited to icons, and the package version is incremented. No private certificates are included.

## Initial verification (2026-10-19)

Windows .NET SDK 10.0.402; target win-x64.

```powershell
dotnet build FairSky/FairSky.csproj -c Debug -r win-x64 -p:Platform=x64 -p:AppxPackageSigningEnabled=false
dotnet run --project tests/FairSky.Tests/FairSky.Tests.csproj -p:Platform=x64 -p:AppxPackageSigningEnabled=false
```

The test executable passed 19 assertions: temperature formatting across three
cultures, rounded negative zero, stale/recovered/loading/error presentation,
long city names, real SDK property notifications, existing detail-page navigation,
immediate loading state and idempotent disposal with no subsequent updates.
Tests do not read cities.json or send HTTP requests. They do not simulate a full
MET Norway outage; network/cache behavior is unchanged and requires a live check.

MSIX generation also succeeded with signing disabled. Its packaging warning says
`mspdbcmf.exe` is missing, so no symbols package was generated. The initial baseline
solution build also reported the existing APPX1707 SDK metadata warning.
The package version remains 0.0.7.2. The generated MSIX is unsigned, not an
installation-ready release. Use the existing local development signing setup
for deployment; do not commit certificates or keys.

## Icon update verification (2026-10-20)

Version 0.0.7.4 passed 108 assertions, including PNG availability for every
supported weather code, shared local image paths for Dock/details, and stale
snapshot icon preservation. Signed x64 MSIX generation succeeded; SignTool
verified the signature and all nine PNG files were checked byte-for-byte inside
the package. The existing missing-mspdbcmf symbols-package warning remains.
The light/dark asset preview was inspected; actual host rendering of these new
icons still requires user verification after installation.

## User acceptance (2026-10-20)

The user supplied screenshots verifying the 0.0.7.3 home/details pages in both
themes and Dock in both themes. The user also confirmed cached weather remained
available offline and refreshed after connectivity returned. Light-theme
screenshots exposed pale emoji, which the 0.0.7.4 artwork addresses.

After installing 0.0.7.4, the user confirmed the new icons and outlines appeared
correctly in the actual UI. This is user-reported visual acceptance, separate
from automated checks and the asset preview. No PowerToys settings or provider
cache were reset.

PR #3 was merged on 2026-10-20. City search/management and choosing the Dock subset
are separate future development streams; see the repository README.
