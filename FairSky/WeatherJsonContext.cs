
using System.Text.Json.Serialization;

namespace FairSky;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(City[]))]
internal sealed partial class WeatherJsonContext : JsonSerializerContext
{
}
