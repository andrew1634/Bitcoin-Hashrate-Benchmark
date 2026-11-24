using System.Net.Http.Json;
using System.Text.Json;

namespace BitcoinHashrateBenchmark;

internal sealed record LeaderboardSubmission(
    string CpuModel,
    string Architecture,
    string Source,
    long Hashes,
    double ElapsedSeconds,
    int ThreadCount);

internal sealed record LeaderboardEntry(
    string CpuModel,
    string Architecture,
    string Source,
    long Hashes,
    double ElapsedSeconds,
    int ThreadCount,
    double HashesPerSecond,
    DateTimeOffset SubmittedAt,
    string VerificationStatus,
    int CategoryRank);

internal sealed record LeaderboardResponse(List<LeaderboardEntry> Entries);

internal static class LeaderboardClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<List<LeaderboardEntry>> GetAsync(
        string serviceUrl,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.GetAsync(
            BuildUri(serviceUrl, "leaderboard?sort=performance"),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<LeaderboardResponse>(
            JsonOptions,
            cancellationToken);
        return result?.Entries ?? throw new JsonException("The leaderboard response did not contain entries.");
    }

    public static async Task SubmitAsync(
        string serviceUrl,
        LeaderboardSubmission submission,
        CancellationToken cancellationToken = default)
    {
        using var response = await Http.PostAsJsonAsync(
            BuildUri(serviceUrl, "submissions"),
            submission,
            JsonOptions,
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static Uri BuildUri(string serviceUrl, string path)
    {
        if (!Uri.TryCreate(serviceUrl.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps
                && !(baseUri.Scheme == Uri.UriSchemeHttp && baseUri.IsLoopback))
            || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            throw new UriFormatException("Enter the HTTPS service URL or a local HTTP development URL.");
        }

        return new Uri($"{baseUri.GetLeftPart(UriPartial.Authority).TrimEnd('/')}/api/{path}");
    }
}
