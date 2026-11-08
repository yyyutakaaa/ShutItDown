using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace ShutItDown.Core.Updates;

/// <summary>A newer version published as a GitHub release.</summary>
public sealed record UpdateInfo(
    Version Version,
    string Notes,
    string InstallerUrl,
    string? ChecksumUrl,
    long InstallerSize,
    string ReleasePageUrl);

/// <summary>
/// Looks for a newer release on GitHub and downloads its installer. Releases are made by the
/// "Release" GitHub Actions workflow, which uploads ShutdownServer-Setup.exe and its SHA-256 checksum.
/// </summary>
public sealed class UpdateChecker
{
    public const string InstallerName = "ShutdownServer-Setup.exe";
    public const string ChecksumName = InstallerName + ".sha256";

    private readonly HttpClient _http;
    private readonly string _repository;

    /// <param name="repository">"owner/name" on GitHub.</param>
    public UpdateChecker(HttpClient http, string repository)
    {
        _http = http;
        _repository = repository;
    }

    /// <summary>Returns the latest release when it is newer than <paramref name="current"/>, otherwise null.</summary>
    public async Task<UpdateInfo?> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{_repository}/releases/latest");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ShutdownServer", Normalize(current).ToString()));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null; // no releases yet
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        return Parse(json.RootElement, current);
    }

    public static UpdateInfo? Parse(JsonElement release, Version current)
    {
        if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;
        if (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;

        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!TryParseVersion(tag, out var version) || version <= Normalize(current))
            return null;

        string? installerUrl = null, checksumUrl = null;
        long size = 0;
        if (release.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                var url = asset.GetProperty("browser_download_url").GetString();
                if (string.Equals(name, InstallerName, StringComparison.OrdinalIgnoreCase))
                {
                    installerUrl = url;
                    size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                }
                else if (string.Equals(name, ChecksumName, StringComparison.OrdinalIgnoreCase))
                {
                    checksumUrl = url;
                }
            }
        }

        // A release without the installer can't be installed from the app.
        if (installerUrl is null || !installerUrl.StartsWith("https://", StringComparison.Ordinal))
            return null;

        var notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "";
        var page = release.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
        return new UpdateInfo(version, notes.Trim(), installerUrl, checksumUrl, size, page);
    }

    /// <summary>Downloads the installer to a temp folder, checks its SHA-256 and returns its path.</summary>
    public async Task<string> DownloadAsync(UpdateInfo update, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(Path.GetTempPath(), "ShutdownServer-Update");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"ShutdownServer-Setup-{update.Version}.exe");

        using (var response = await _http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? update.InstallerSize;

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(path);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                done += read;
                if (total > 0) progress?.Report((double)done / total);
            }
        }

        if (update.ChecksumUrl is not null)
        {
            var expected = ParseChecksum(await _http.GetStringAsync(update.ChecksumUrl, cancellationToken));
            if (!await MatchesChecksumAsync(path, expected, cancellationToken))
            {
                File.Delete(path);
                throw new InvalidDataException("The downloaded update is damaged (checksum mismatch). Try again later.");
            }
        }

        progress?.Report(1);
        return path;
    }

    /// <summary>Accepts "abc123" as well as the sha256sum format "abc123  file.exe".</summary>
    public static string ParseChecksum(string content) =>
        content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.ToLowerInvariant() ?? "";

    public static async Task<bool> MatchesChecksumAsync(string path, string expectedHex, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
        return hash == expectedHex;
    }

    public static bool TryParseVersion(string tag, out Version version)
    {
        var text = tag.Trim().TrimStart('v', 'V');
        var plus = text.IndexOfAny(['-', '+']);
        if (plus >= 0) text = text[..plus];

        if (Version.TryParse(text, out var parsed))
        {
            version = Normalize(parsed);
            return true;
        }
        version = new Version(0, 0, 0);
        return false;
    }

    /// <summary>3.1 and 3.1.0.0 both become 3.1.0, so they compare as equal.</summary>
    public static Version Normalize(Version v) => new(v.Major, Math.Max(v.Minor, 0), Math.Max(v.Build, 0));
}
