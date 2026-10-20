using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FairSky;
using Microsoft.CommandPalette.Extensions;

internal static class CityManagementChecks
{
    public static async Task<int> RunAsync()
    {
        int count = 0;
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException(name); count++; }
        var root = Path.Combine(Path.GetTempPath(), "FairSkyTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "cities.json");
            const string legacy = "[{\"Name\":\"Москва\",\"Latitude\":55.7558,\"Longitude\":37.6173}]";
            File.WriteAllText(path, legacy);
            var store = new CityStore(path);
            Check(store.Snapshot().Length == 1 && store.Snapshot()[0].Name == "Москва", "Legacy configuration loads");
            Check(File.ReadAllText(path) == legacy, "Reading legacy configuration must not rewrite it");
            int changes = 0;
            store.Changed += () => changes++;
            var berlin = new City("Берлин", 52.52437, 13.41053, "Германия", "Берлин");
            store.Add(berlin);
            Check(changes == 1 && store.Snapshot().Length == 2, "Save publishes one change");
            Check(File.ReadAllText(path + ".bak") == legacy, "Previous configuration is backed up");
            store.Add(berlin with { Name = "Berlin" });
            Check(changes == 1 && store.Snapshot().Length == 2, "Translated duplicate cannot be added twice");
            var restored = new CityStore(path);
            Check(restored.Snapshot()[1].Country == "Германия" && restored.Snapshot()[0].Latitude == 55.7558, "Restart preserves metadata and existing coordinates");
            store.Move(berlin, -1);
            Check(store.Snapshot()[0] == berlin && new CityStore(path).Snapshot()[0] == berlin, "Reordering is persisted");
            store.Move(berlin, -1);
            Check(changes == 2, "Boundary reorder does not write");
            foreach (var city in store.Snapshot()) store.Remove(city);
            Check(new CityStore(path).Snapshot().Length == 0, "An empty saved list stays empty on restart");
            Check(changes == 4, "One notification per real mutation");
            var invalid = berlin with { Latitude = double.NaN };
            try { store.Add(invalid); throw new NotSupportedException("Expected validation failure"); } catch (ArgumentException) { count++; }
            Check(store.Snapshot().Length == 0, "Invalid city must not change memory");

            File.WriteAllText(path, legacy); // Simulate an external editor while the store is open.
            try { store.Add(berlin); throw new NotSupportedException("Expected external edit conflict"); } catch (IOException) { count++; }
            Check(File.ReadAllText(path) == legacy && store.Snapshot().Length == 0, "External edits must not be overwritten");
            File.WriteAllText(path, "broken JSON");
            var broken = new CityStore(path);
            Check(broken.LoadError is not null, "Corrupt config is reported");
            try { broken.Add(berlin); throw new NotSupportedException("Expected read-only damaged store"); } catch (InvalidOperationException) { count++; }
            Check(File.ReadAllText(path) == "broken JSON", "Corrupt file is retained for recovery");

            var limitPath = Path.Combine(root, "limit.json");
            var twelve = Enumerable.Range(0, 12).Select(i => new City("Город " + i, i, i)).ToArray();
            var limited = new CityStore(limitPath, twelve);
            limited.Add(berlin);
            Check(limited.Snapshot().Length == 12, "Existing limit is respected on new additions");
            File.WriteAllText(limitPath, JsonSerializer.Serialize(twelve.Append(berlin).ToArray(), WeatherJsonContext.Default.CityArray));
            Check(new CityStore(limitPath).Snapshot().Length == 13, "Legacy rows above the add limit are never discarded");

            var handler = new SearchHandler();
            using var http = new HttpClient(handler);
            var search = new CitySearchService(http);
            handler.Json = """
                {"results":[
                    {"name":"Берлин","latitude":52.52437,"longitude":13.41053,"country":"Германия","admin1":"Берлин"},
                    {"name":"Berlin duplicate","latitude":52.52437,"longitude":13.41053},
                    {"name":"Invalid","latitude":"bad","longitude":1},
                    {"name":"Out of range","latitude":100,"longitude":1},
                    {"name":"London","latitude":51.5,"longitude":-0.12}
                ]}
                """;
            Check((await search.SearchAsync("a", CancellationToken.None)).Length == 0 && handler.Calls == 0, "Short searches do not send HTTP");
            var found = await search.SearchAsync(" Берлин ", CancellationToken.None);
            Check(found.Length == 2 && found[0].Country == "Германия", "Geocoding metadata and invalid result filtering");
            Check(Uri.UnescapeDataString(handler.LastUri!.Query).Contains("name=Берлин", StringComparison.Ordinal), "Non-ASCII queries are correctly encoded");
            _ = await search.SearchAsync("берлин", CancellationToken.None);
            Check(handler.Calls == 1, "Repeated searches reuse the cache");
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            try { await search.SearchAsync("Берлин", canceled.Token); throw new NotSupportedException("Expected cancellation"); } catch (OperationCanceledException) { count++; }
            handler.Json = "{}";
            Check((await search.SearchAsync("No match", CancellationToken.None)).Length == 0, "Missing results means no matches");
            handler.Code = HttpStatusCode.ServiceUnavailable;
            try { await search.SearchAsync("Offline", CancellationToken.None); throw new NotSupportedException("Expected HTTP failure"); } catch (HttpRequestException) { count++; }
            handler.Code = HttpStatusCode.OK; handler.Json = "[]";
            try { await search.SearchAsync("Malformed", CancellationToken.None); throw new NotSupportedException("Expected JSON failure"); } catch (JsonException) { count++; }

            var uiStore = new CityStore(Path.Combine(root, "ui.json"), []);
            var fake = new ControlledSearch();
            using var home = new FairSkyHomePage(uiStore, new WeatherService(), fake, TimeSpan.FromMilliseconds(1), refreshWeather: false);
            using var dock = new FairSkyDockPage(uiStore, new WeatherService(), startRefresh: false);
            var originalBand = new Microsoft.CommandPalette.Extensions.Toolkit.WrappedDockItem([], "org.fairsky.cmdpal.cities", "fairsky");
            Check(dock.Id == originalBand.Command!.Id, "Existing pinned Dock band identity is unchanged");
            int dockChanges = 0;
            dock.ItemsChanged += (_, _) => dockChanges++;
            home.UpdateSearchText("", "Berlin");
            var oldSearch = home.PendingSearch;
            await fake.Requested("Berlin");
            home.UpdateSearchText("Berlin", "London");
            await fake.Requested("London");
            var london = new City("Лондон", 51.5, -0.12, "Великобритания", "Англия");
            fake.Complete("London", [london]);
            await home.PendingSearch;
            fake.Complete("Berlin", [berlin]);
            await oldSearch;
            Check(home.GetItems().Any(i => i.Title == "Лондон") && home.GetItems().All(i => i.Title != "Берлин"), "Late results cannot replace a newer query");
            Check(uiStore.Snapshot().Length == 0 && dock.GetItems().Length == 0, "Searching alone never saves a city or adds a Dock timer");
            var result = home.GetItems().Single(i => i.Title == "Лондон");
            Check(result.Command is FairSkyPage && result.Subtitle.Contains("Великобритания", StringComparison.Ordinal), "Result opens existing forecast and disambiguates location");
            Check(result.MoreCommands.Length == 1, "Unsaved result offers explicit save action");
            var saveResult = ((IInvokableCommand)((ICommandItem)result.MoreCommands[0]).Command!).Invoke(null);
            Check(uiStore.Contains(london), "Native Add command persists city");
            Check(home.GetItems().Count(i => i.Title == "Лондон") == 1, "Localized result stays visible once saved under English query");
            Check(dock.GetItems().Length == 1 && dockChanges == 1, "Saving updates Dock without restarting");
            var retainedDock = dock.GetItems()[0];
            uiStore.Add(berlin); uiStore.Move(berlin, -1);
            Check(dock.GetItems()[0].Title == "Берлин" && ReferenceEquals(dock.GetItems()[1], retainedDock), "Reorder preserves existing Dock timers/items");
            uiStore.Remove(london);
            Check(dock.GetItems().Length == 1 && dock.GetItems()[0].Title == "Берлин", "Removing city updates Dock");
            home.UpdateSearchText("London", "Failure"); await fake.Requested("Failure");
            fake.Fail("Failure"); await home.PendingSearch;
            Check(!home.IsLoading && home.GetItems().Any(i => i.Title.Contains("недоступен", StringComparison.Ordinal)), "Search failure ends loading and shows a useful message");
            home.UpdateSearchText("Failure", "Pending"); await fake.Requested("Pending");
            var pending = home.PendingSearch;
            home.UpdateSearchText("Pending", "");
            fake.Complete("Pending", [london]); await pending;
            Check(home.GetItems().Any(i => i.Title == "Берлин") && home.GetItems().All(i => i.Title != "Лондон"), "Clearing search restores saved cities and invalidates pending results");
            home.UpdateSearchText("", "Dispose"); await fake.Requested("Dispose");
            var disposedSearch = home.PendingSearch;
            int lateEvents = 0; home.ItemsChanged += (_, _) => lateEvents++;
            home.Dispose(); fake.Complete("Dispose", [london]); await disposedSearch;
            Check(lateEvents == 0, "Disposed search page never publishes late results");
            dock.Dispose(); var before = dockChanges; uiStore.Add(london);
            Check(dockChanges == before, "Disposed Dock unsubscribes from saved-city changes");
        }
        finally { Directory.Delete(root, recursive: true); }
        Console.WriteLine($"PASS: {count} city management/search checks (isolated files and fake HTTP).");
        return count;
    }

    private sealed class SearchHandler : HttpMessageHandler
    {
        public int Calls;
        public Uri? LastUri;
        public string Json = "{}";
        public HttpStatusCode Code = HttpStatusCode.OK;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(Code) { Content = new StringContent(Json) });
        }
    }

    private sealed class ControlledSearch : ICitySearchService
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<City[]>> _results = new();
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _requested = new();
        public Task<City[]> SearchAsync(string query, CancellationToken cancellationToken)
        {
            var result = _results.GetOrAdd(query, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));
            _requested.GetOrAdd(query, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult(true);
            return result.Task; // Intentionally ignore cancellation to exercise generation guards.
        }
        public Task<bool> Requested(string query) => _requested.GetOrAdd(query, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task.WaitAsync(TimeSpan.FromSeconds(5));
        public void Complete(string query, City[] cities) => _results[query].SetResult(cities);
        public void Fail(string query) => _results[query].SetException(new HttpRequestException("offline"));
    }
}
