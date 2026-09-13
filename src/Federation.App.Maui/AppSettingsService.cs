using System.Text.Json;

namespace Federation.App.Maui;

/// <summary>Loads and saves AppSettings from the platform app-data directory.</summary>
public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _path;
    private AppSettings _current;

    public AppSettingsService()
    {
        _path = Path.Combine(FileSystem.AppDataDirectory, "federation-node.json");
        _current = Load();
    }

    public AppSettings Current => _current;

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? new AppSettings();
            }
        }
        catch { /* first-run: return defaults */ }
        return new AppSettings();
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(_current, JsonOpts);
        File.WriteAllText(_path, json);
    }

    /// <summary>Replaces current settings with the supplied instance and saves.</summary>
    public void Apply(AppSettings updated)
    {
        _current = updated;
        Save();
    }
}
