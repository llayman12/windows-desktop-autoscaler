using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

namespace DisplayScalingManager.Core.Configuration;

public sealed class AppConfig
{
    [JsonPropertyName("PortablePercent")]
    public int PortablePercent { get; set; } = 100;

    [JsonPropertyName("DesktopPercent")]
    public int DesktopPercent { get; set; } = 125;

    [JsonPropertyName("DebounceMilliseconds")]
    public int DebounceMilliseconds { get; set; } = 500;

    public static AppConfig LoadOrCreateDefault(string? path = null, ILogger? logger = null)
    {
        path ??= AppPaths.ConfigFilePath;
        var log = logger ?? Log.Logger;

        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json);
                if (loaded is not null)
                {
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning(ex, "AppConfig: failed to load {Path}, falling back to defaults", path);
        }

        var config = new AppConfig();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            log.Warning(ex, "AppConfig: failed to write default config to {Path}", path);
        }

        return config;
    }
}
