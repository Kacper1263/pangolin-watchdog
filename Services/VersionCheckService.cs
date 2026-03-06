using System.Text.Json;
using System.Text.RegularExpressions;

namespace PangolinWatchdog.Services;

public sealed record VersionCheckResult(
    string CurrentVersion,
    string? LatestVersion,
    bool IsDevVersion,
    bool UpdateAvailable);

public class VersionCheckService
{
    private const string DockerTagsUrl = "https://hub.docker.com/v2/repositories/kacper1263/pangolin-watchdog/tags?page_size=100";
    private static readonly Regex StableVersionRegex = new(@"^v?(?<version>\d+(?:\.\d+){1,3})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex CurrentVersionRegex = new(@"^v?(?<version>\d+(?:\.\d+){1,3})(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly ILogger<VersionCheckService> _logger;

    public VersionCheckService(HttpClient httpClient, ILogger<VersionCheckService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<VersionCheckResult> GetVersionStatusAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = (Environment.GetEnvironmentVariable("APP_VERSION") ?? "dev").Trim();
        var isDevVersion = currentVersion.Equals("dev", StringComparison.OrdinalIgnoreCase);
        var latestVersion = await TryGetLatestVersionAsync(cancellationToken);

        var updateAvailable = !isDevVersion
                              && latestVersion is not null
                              && TryParseCurrentVersion(currentVersion, out var current)
                              && TryParseStableVersion(latestVersion, out var latest)
                              && latest > current;
        
        // log results 
        _logger.LogDebug("Version Check: Current Version = {CurrentVersion}, Latest Version = {LatestVersion}, Is Dev Version = {IsDevVersion}, Update Available = {UpdateAvailable}",
            currentVersion, latestVersion ?? "unknown", isDevVersion, updateAvailable);

        return new VersionCheckResult(currentVersion, latestVersion, isDevVersion, updateAvailable);
    }

    private async Task<string?> TryGetLatestVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(DockerTagsUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning("Docker Hub response did not include tags list.");
                return null;
            }

            Version? latestVersion = null;
            string? latestTag = null;

            foreach (var tagElement in results.EnumerateArray())
            {
                if (!tagElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var tagName = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(tagName) || !TryParseStableVersion(tagName, out var version))
                {
                    continue;
                }

                if (latestVersion is null || version > latestVersion)
                {
                    latestVersion = version;
                    latestTag = tagName;
                }
            }

            return latestTag;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to fetch latest Docker image version from Docker Hub.");
            return null;
        }
    }

    private static bool TryParseStableVersion(string input, out Version version)
    {
        version = new Version();

        if (!StableVersionRegex.IsMatch(input))
        {
            return false;
        }

        var normalized = input.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? input[1..] : input;
        if (!Version.TryParse(normalized, out var parsed) || parsed is null)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    private static bool TryParseCurrentVersion(string input, out Version version)
    {
        version = new Version();

        var match = CurrentVersionRegex.Match(input);
        if (!match.Success)
        {
            return false;
        }

        var normalized = match.Groups["version"].Value;
        if (!Version.TryParse(normalized, out var parsed) || parsed is null)
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
