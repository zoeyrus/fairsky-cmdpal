
using System.Text.Json.Serialization;

namespace WorldWeather;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(City[]))]
internal sealed partial class WeatherJsonContext : JsonSerializerContext
{
}
