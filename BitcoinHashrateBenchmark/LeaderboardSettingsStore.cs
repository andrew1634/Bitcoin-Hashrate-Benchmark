namespace BitcoinHashrateBenchmark;

internal sealed class LeaderboardSettingsStore
{
    public const string DefaultServiceUrl =
        "https://bitcoin-hashrate-benchmark.azgasgiant220.workers.dev";

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
            var savedUrl = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrWhiteSpace(savedUrl))
            {
                return savedUrl;
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }

        var configuredUrl = Environment.GetEnvironmentVariable("BITCOIN_HASHRATE_LEADERBOARD_URL")?.Trim();
        return string.IsNullOrWhiteSpace(configuredUrl) ? DefaultServiceUrl : configuredUrl;
    }

    public void Save(string serviceUrl)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The leaderboard settings path is invalid.");
        Directory.CreateDirectory(directory);
        File.WriteAllText(filePath, serviceUrl.Trim());
    }
}
