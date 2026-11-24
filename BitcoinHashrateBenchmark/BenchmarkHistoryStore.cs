using System.Text.Json;

namespace BitcoinHashrateBenchmark;

internal sealed record BenchmarkHistoryEntry(
    DateTimeOffset CompletedAt,
    int ThreadCount,
    long Hashes,
    double ElapsedSeconds)
{
    public double HashesPerSecond => ElapsedSeconds > 0 ? Hashes / ElapsedSeconds : 0;
}

internal sealed class BenchmarkHistoryStore
{
    public const int MaxEntries = 10;

    private readonly string filePath;

    public BenchmarkHistoryStore()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("The local application data folder is unavailable.");
        }

        filePath = Path.Combine(localAppData, "BitcoinHashrateBenchmark", "history.json");
    }

    public List<BenchmarkHistoryEntry> Load()
    {
        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }

        var entries = JsonSerializer.Deserialize<List<BenchmarkHistoryEntry>>(json)
            ?? throw new InvalidDataException("The saved benchmark history is empty or invalid.");

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        foreach (var entry in entries)
        {
            if (entry is null
                || entry.CompletedAt == default
                || entry.ThreadCount < 1
                || entry.Hashes < 0
                || !double.IsFinite(entry.ElapsedSeconds)
                || entry.ElapsedSeconds <= 0)
            {
                throw new InvalidDataException("The saved benchmark history contains an invalid run.");
            }
        }

        return entries;
    }

    public List<BenchmarkHistoryEntry> Add(BenchmarkHistoryEntry entry)
    {
        var entries = Load();
        entries.Insert(0, entry);
        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }

        Save(entries);
        return entries;
    }

    public void Clear()
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The benchmark history path is invalid.");
        Directory.CreateDirectory(directory);
        File.Delete(filePath);
    }

    private void Save(List<BenchmarkHistoryEntry> entries)
    {
        var directory = Path.GetDirectoryName(filePath)
            ?? throw new InvalidOperationException("The benchmark history path is invalid.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
        File.Move(temporaryPath, filePath, overwrite: true);
    }
}
