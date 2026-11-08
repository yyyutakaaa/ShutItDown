using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShutItDown.Core.Updates;

namespace ShutItDown.Tests;

public class UpdateCheckerTests
{
    private const string InstallerUrl = "https://github.com/o/r/releases/download/v3.1.0/ShutdownServer-Setup.exe";
    private const string ChecksumUrl = InstallerUrl + ".sha256";
    private static readonly Version Current = new(3, 0, 0);

    private static string Release(string tag, bool withInstaller = true, bool prerelease = false, string body = "Fixes") => $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": {{(prerelease ? "true" : "false")}},
          "body": "{{body}}",
          "html_url": "https://github.com/o/r/releases/tag/{{tag}}",
          "assets": [
            {{(withInstaller ? $$"""{ "name": "ShutdownServer-Setup.exe", "size": 4, "browser_download_url": "{{InstallerUrl}}" },""" : "")}}
            { "name": "ShutdownServer-Setup.exe.sha256", "size": 80, "browser_download_url": "{{ChecksumUrl}}" }
          ]
        }
        """;

    private static UpdateInfo? Parse(string json) => UpdateChecker.Parse(JsonDocument.Parse(json).RootElement, Current);

    [Fact]
    public void Newer_release_with_an_installer_is_offered()
    {
        var update = Parse(Release("v3.1.0"));

        Assert.NotNull(update);
        Assert.Equal(new Version(3, 1, 0), update.Version);
        Assert.Equal(InstallerUrl, update.InstallerUrl);
        Assert.Equal(ChecksumUrl, update.ChecksumUrl);
        Assert.Equal("Fixes", update.Notes);
    }

    [Theory]
    [InlineData("v3.0.0")]
    [InlineData("3.0")]
    [InlineData("v2.9.9")]
    [InlineData("not-a-version")]
    public void Same_older_or_odd_versions_are_not_offered(string tag) =>
        Assert.Null(Parse(Release(tag)));

    [Fact]
    public void Prereleases_and_releases_without_installer_are_ignored()
    {
        Assert.Null(Parse(Release("v4.0.0", prerelease: true)));
        Assert.Null(Parse(Release("v4.0.0", withInstaller: false)));
    }

    [Theory]
    [InlineData("v3.1.0", "3.1.0")]
    [InlineData("3.1", "3.1.0")]
    [InlineData("V10.2.3-beta", "10.2.3")]
    [InlineData("v3.1.0.7", "3.1.0")]
    public void Tags_are_read_as_versions(string tag, string expected)
    {
        Assert.True(UpdateChecker.TryParseVersion(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData("ABC123  ShutdownServer-Setup.exe", "abc123")]
    [InlineData("abc123\n", "abc123")]
    [InlineData("", "")]
    public void Checksum_files_in_both_formats_are_read(string content, string expected) =>
        Assert.Equal(expected, UpdateChecker.ParseChecksum(content));

    [Fact]
    public async Task No_releases_yet_means_no_update()
    {
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound))), "o/r");

        Assert.Null(await checker.CheckAsync(Current));
    }

    [Fact]
    public async Task Check_asks_github_for_the_latest_release_with_a_user_agent()
    {
        HttpRequestMessage? seen = null;
        var checker = new UpdateChecker(new HttpClient(new FakeHandler(request =>
        {
            seen = request;
            return Json(Release("v3.2.0"));
        })), "owner/repo");

        var update = await checker.CheckAsync(Current);

        Assert.Equal("https://api.github.com/repos/owner/repo/releases/latest", seen!.RequestUri!.ToString());
        Assert.NotEmpty(seen.Headers.UserAgent);
        Assert.Equal(new Version(3, 2, 0), update!.Version);
    }

    [Fact]
    public async Task Download_keeps_a_file_with_the_right_checksum()
    {
        var installer = Encoding.ASCII.GetBytes("MZ..");
        var hash = Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant();
        var checker = Downloader(installer, $"{hash}  ShutdownServer-Setup.exe");
        var reported = new List<double>();

        var path = await checker.DownloadAsync(Update(), new SyncProgress(reported.Add));

        Assert.Equal(installer, await File.ReadAllBytesAsync(path));
        Assert.Equal(1, reported[^1]);
        File.Delete(path);
    }

    [Fact]
    public async Task Download_with_a_wrong_checksum_is_thrown_away()
    {
        var checker = Downloader(Encoding.ASCII.GetBytes("MZ.."), new string('0', 64));

        await Assert.ThrowsAsync<InvalidDataException>(() => checker.DownloadAsync(Update()));
        Assert.False(File.Exists(Path.Combine(Path.GetTempPath(), "ShutdownServer-Update", "ShutdownServer-Setup-3.1.0.exe")));
    }

    private static UpdateInfo Update() => new(new Version(3, 1, 0), "", InstallerUrl, ChecksumUrl, 4, "");

    private static UpdateChecker Downloader(byte[] installer, string checksum) =>
        new(new HttpClient(new FakeHandler(request => request.RequestUri!.ToString() == ChecksumUrl
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(checksum) }
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installer) })), "o/r");

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    // Progress<T> posts to a sync context; this reports right away, which keeps the test simple.
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
