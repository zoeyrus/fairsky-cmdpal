# City search and management — 0.0.8.0

Scope: find a city, inspect its forecast without saving, explicitly add/remove it,
and reorder saved cities. No independent Dock subset in this sprint.

## Implementation

- `CityStore`: loads the existing JSON array without rewriting it, preserves empty
  lists and all legacy entries, validates new data, saves by atomic replace with
  a previous-file backup, and rejects writes after detected external edits.
- `CitySearchService`: Open-Meteo Geocoding with GeoNames attribution; 10 results,
  Russian result labels where available, 10-second HTTP timeout and bounded
  in-memory query cache. Forecasts continue to use the existing MET Norway service.
- `FairSkyHomePage`: native DynamicListPage. Empty search shows saved cities;
  two or more characters start a 350 ms debounced search. Cancellation and a
  query generation guard prevent stale responses from replacing current results.
- `CityActions`: explicit native menu commands. Viewing never adds a city.
  Country/region are optional fields; old records remain readable.
- `FairSkyDockPage`: keeps the previous band command ID, reuses existing Dock
  items when reordering, disposes removed timers and raises ItemsChanged.
- Saved cities and forecast pages retain outlined local weather images.

## Validation

149 local assertions passed, including legacy round-trip, persistence and backups,
empty list/restart, coordinate duplicates, ordering/boundaries, add limit, corrupt
configuration, external-edit conflict, invalid geocoding data, cache reuse, network
failure, cancellation, out-of-order results, native Add action, translated result
visibility after saving, Dock changes/identity, and disposal.

Live smoke checks found Нью-Йорк (США), Берлин (Германия), Лондон (Британия), and
loaded four-day forecasts from MET Norway for each. Tests used temporary config
files and did not add cities to the user's real configuration.

Optional live check from the repository root:

```powershell
dotnet run --project tests/FairSky.Tests/FairSky.Tests.csproj -p:Platform=x64 -p:AppxPackageSigningEnabled=false -- --live-search
```

The signed Debug x64 MSIX 0.0.8.0 built successfully and passed SignTool verification.
It was installed locally with package status OK; the existing cities.json hash
was unchanged by installation. The only packaging warning is the existing missing
`mspdbcmf.exe` tool (no symbols package); compilation has no new warnings.

## Manual acceptance in Command Palette (pending)

1. Existing saved cities still appear after update/reload.
2. Type New York or Нью-Йорк, Berlin or Берлин, London or Лондон, one at a time.
3. Country/region identify each result; Enter opens a forecast without saving it.
4. Use More → Добавить в мои города, including from the forecast page; clear search.
5. Saved city appears once, remains after extension restart, and appears in Dock.
6. Move above/below updates the saved order and the current all-cities Dock order.
7. Remove a city; it leaves the saved list and Dock. Removing the last leaves an
   empty state; search remains available.
8. Fast typing and clearing search do not resurrect old results. A network failure
   displays an error; clearing the search restores the saved-city list.
9. Verify actual Dock list refresh and menu behavior after mutations. Local event
   tests do not replace host-side visual verification.

Do not merge until the user accepts the actual interface. Do not reset PowerToys
settings or its provider cache to test this update.
