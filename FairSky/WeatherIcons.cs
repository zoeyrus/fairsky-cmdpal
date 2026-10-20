using System;
using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace FairSky;

// Original outlined artwork is shipped locally: no web requests or emoji-font dependency.
internal static class WeatherIcons
{
    public static string Source(int code)
    {
        string name = code switch
        {
            0 or 1 => "sun",
            2 => "partly-cloudy",
            3 => "cloud",
            45 or 48 => "fog",
            51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 => "rain",
            56 or 57 or 66 or 67 => "sleet",
            71 or 73 or 75 or 77 or 85 or 86 => "snow",
            95 or 96 or 99 => "storm",
            _ => "unknown"
        };
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Weather", name + ".png");
    }

    public static IconInfo Create(int code) => new(Source(code));

    public static string Markdown(int code) =>
        $"![{WeatherService.Description(code)}]({new Uri(Source(code)).AbsoluteUri}?--x-cmdpal-maxwidth=32&--x-cmdpal-maxheight=32)";
}
