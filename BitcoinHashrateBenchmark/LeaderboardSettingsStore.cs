namespace BitcoinHashrateBenchmark;

internal sealed class LeaderboardSettingsStore
{
    private readonly string filePath;

    public LeaderboardSettingsStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data folder is unavailable.");
        }

        filePath = Path.Combine(localAppData, "BitcoinHashrateBenchmark", "leaderboard-url.txt");
    }

    public string Load()
    {
        try
        {
            return File.ReadAllText(filePath).Trim();
        }
        catch (FileNotFoundException)
        {
            return Environment.GetEnvironmentVariable("BITCOIN_HASHRATE_LEADERBOARD_URL")?.Trim() ?? "";
        }
        catch (DirectoryNotFoundException)
        {
            return Environment.GetEnvironmentVariable("BITCOIN_HASHRATE_LEADERBOARD_URL")?.Trim() ?? "";
        }
    }

    public void Save(string serviceUrl)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The leaderboard settings path is invalid.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, serviceUrl.Trim());
    }
}
