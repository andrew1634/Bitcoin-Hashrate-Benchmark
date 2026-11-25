using System.Text.Json;

namespace BitcoinHashrateBenchmark;

internal sealed record AppSettings
{
    public string Theme { get; init; } = "light";
    public string AccentColor { get; init; } = "green";
    public decimal FontSize { get; init; } = 10;
    public int CustomDurationSeconds { get; init; } = 60;
    public int CustomThreadCount { get; init; } = Math.Clamp(Environment.ProcessorCount, 1, 256);
    public bool CustomizationUnlocked { get; init; }
}

internal sealed class AppSettingsStore
{
    private readonly string filePath;

    public AppSettingsStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data folder is unavailable.");
        }

        filePath = Path.Combine(localAppData, "BitcoinHashrateBenchmark", "settings.json");
    }

    public AppSettings Load()
    {
        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            return new AppSettings();
        }
        catch (DirectoryNotFoundException)
        {
            return new AppSettings();
        }

        var settings = JsonSerializer.Deserialize<AppSettings>(json)
            ?? throw new InvalidDataException("The saved application settings are invalid.");
        if (settings.Theme is not ("light" or "dark")
            || settings.AccentColor is not ("green" or "blue" or "purple" or "orange")
            || settings.FontSize is < 8 or > 20
            || settings.CustomDurationSeconds is < 1 or > 86_400
            || settings.CustomThreadCount is < 1 or > 256)
        {
            throw new InvalidDataException("The saved application settings contain an invalid value.");
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The application settings path is invalid.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings));
        File.Move(temporaryPath, filePath, overwrite: true);
    }
}
