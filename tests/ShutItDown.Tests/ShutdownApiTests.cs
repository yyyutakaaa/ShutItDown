using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using ShutItDown.Core.Monitoring;
using ShutItDown.Core.Pairing;
using ShutItDown.Core.Security;
using ShutItDown.Web;

namespace ShutItDown.Tests;

public sealed class ShutdownApiTests : IAsyncLifetime
{
    private readonly FakePowerController _power = new();
    private readonly List<ShutdownScheduledInfo> _notifications = new();
    private readonly List<string> _pairedNames = new();
    private readonly PinAttemptLimiter _limiter = new(maxFailures: 3);
    private readonly DeviceRegistry _devices = new(new List<PairedDevice>(), () => { });
    private readonly PairingCode _pairing = new();
    private readonly FakePcMonitor _monitor = new();
    private ShutdownWatcher _watcher = null!;
    private bool _allowScreen = true;
    private string? _pinHash;
    private TimeSpan _grace = TimeSpan.Zero;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _watcher = new ShutdownWatcher(_monitor, _power);
        var deps = new ShutdownApiDependencies(_power, () => _pinHash, _limiter, _devices, _pairing)
        {
            GetGracePeriod = () => _grace,
            OnShutdownScheduled = _notifications.Add,
            OnPaired = _pairedNames.Add,
            Monitor = _monitor,
            Watcher = _watcher,
            AllowScreenPreview = () => _allowScreen,
        };
        _app = ShutdownServer.Build(deps, port: 0, builder => builder.WebHost.UseTestServer());
        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    // --- Page ---------------------------------------------------------------

    [Fact]
    public async Task Phone_page_and_assets_are_served()
    {
        var html = await _client.GetStringAsync("/");
        Assert.Contains("<title>Shutdown</title>", html);

        foreach (var path in new[] { "/app.js", "/app.css", "/favicon.svg", "/icon-192.png", "/apple-touch-icon.png" })
            Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(path)).StatusCode);
    }

    // --- Pairing ------------------------------------------------------------

    [Fact]
    public async Task Unpaired_phone_without_pin_is_asked_to_pair()
    {
        var response = await _client.PostAsJsonAsync("/api/shutdown", new { delayMinutes = 0 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True((await Json(response)).GetProperty("needsPairing").GetBoolean());
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Pairing_gives_a_token_that_can_shut_down()
    {
        var token = await Pair("Pixel 8");

        var response = await Send(token, new { delayMinutes = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([TimeSpan.Zero], _power.Scheduled);
        Assert.Equal(["Pixel 8"], _pairedNames);
        Assert.NotNull(_devices.Devices.Single().LastUsedAt);
    }

    [Fact]
    public async Task Pairing_code_works_only_once()
    {
        var code = _pairing.Current;

        var first = await _client.PostAsJsonAsync("/api/pair", new { code, name = "a" });
        var second = await _client.PostAsJsonAsync("/api/pair", new { code, name = "b" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Single(_devices.Devices);
    }

    [Fact]
    public async Task Wrong_pairing_code_is_refused()
    {
        var response = await _client.PostAsJsonAsync("/api/pair", new { code = "not-the-code" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_devices.Devices);
    }

    [Fact]
    public async Task Removed_phone_can_no_longer_shut_down()
    {
        var token = await Pair("old phone");
        _devices.Remove(_devices.Devices.Single().Id);

        var response = await Send(token, new { delayMinutes = 0 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Status_tells_the_phone_whether_it_is_paired()
    {
        Assert.False((await GetStatus(null)).GetProperty("paired").GetBoolean());

        var token = await Pair("iPhone");
        var status = await GetStatus(token);

        Assert.True(status.GetProperty("paired").GetBoolean());
        Assert.Equal("iPhone", status.GetProperty("deviceName").GetString());
    }

    // --- Backup PIN ---------------------------------------------------------

    [Fact]
    public async Task Unpaired_phone_can_use_the_backup_pin()
    {
        _pinHash = PinHasher.Hash("1234");

        var wrong = await _client.PostAsJsonAsync("/api/shutdown", new { pin = "9999" });
        var right = await _client.PostAsJsonAsync("/api/shutdown", new { pin = "1234" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Single(_power.Scheduled);
    }

    [Fact]
    public async Task Paired_phone_does_not_need_the_pin()
    {
        _pinHash = PinHasher.Hash("1234");
        var token = await Pair("phone");

        var response = await Send(token, new { delayMinutes = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Too_many_wrong_pins_lock_out_even_the_right_pin()
    {
        _pinHash = PinHasher.Hash("1234");
        for (var i = 0; i < 3; i++)
            await _client.PostAsJsonAsync("/api/shutdown", new { pin = "0000" });

        var response = await _client.PostAsJsonAsync("/api/shutdown", new { pin = "1234" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Pin_limit_can_be_changed_while_running()
    {
        _pinHash = PinHasher.Hash("1234");
        _limiter.MaxFailures = 1;

        await _client.PostAsJsonAsync("/api/shutdown", new { pin = "0000" });
        var response = await _client.PostAsJsonAsync("/api/shutdown", new { pin = "1234" });

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    // --- Shutdown -----------------------------------------------------------

    [Fact]
    public async Task Shutdown_with_delay_schedules_it()
    {
        var token = await Pair("phone");

        await Send(token, new { delayMinutes = 30 });

        Assert.Equal([TimeSpan.FromMinutes(30)], _power.Scheduled);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(ShutdownApi.MaxDelayMinutes + 1)]
    public async Task Shutdown_rejects_an_out_of_range_delay(int minutes)
    {
        var token = await Pair("phone");

        var response = await Send(token, new { delayMinutes = minutes });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Grace_period_applies_to_now_but_not_to_timers()
    {
        var token = await Pair("phone");
        _grace = TimeSpan.FromSeconds(30);

        await Send(token, new { delayMinutes = 0 });
        await Send(token, new { delayMinutes = 15 });

        Assert.Equal([TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(15)], _power.Scheduled);
    }

    [Fact]
    public async Task Scheduled_shutdown_is_reported_but_refused_one_is_not()
    {
        var token = await Pair("phone");

        await _client.PostAsJsonAsync("/api/shutdown", new { delayMinutes = 0 }); // not paired
        await Send(token, new { delayMinutes = 0 });

        Assert.Single(_notifications);
    }

    [Fact]
    public async Task Get_request_never_shuts_down()
    {
        await _client.GetAsync("/api/shutdown");

        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Form_post_is_rejected()
    {
        // A plain HTML form on another website can only send this kind of body.
        var response = await _client.PostAsync("/api/shutdown",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["pin"] = "" }));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Cancel_needs_no_pairing()
    {
        var response = await _client.PostAsJsonAsync("/api/cancel", new { });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, _power.CancelCount);
    }

    // --- Live view, "when done", automations --------------------------------

    [Fact]
    public async Task Live_view_needs_a_paired_phone()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/pc")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync("/api/screenshot")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/watch", new { kind = "idle" })).StatusCode);
    }

    [Fact]
    public async Task Live_view_shows_stats_apps_and_unsaved_work()
    {
        var token = await Pair("phone");
        _monitor.Apps.Add(new("notepad", "*todo.txt - Notepad"));
        _monitor.Apps.Add(new("chrome", "News - Google Chrome"));

        var pc = await Json(await Get(token, "/api/pc"));

        Assert.Equal(2, pc.GetProperty("apps").GetArrayLength());
        Assert.Equal("notepad", pc.GetProperty("unsaved")[0].GetProperty("name").GetString());
        Assert.Equal(50, pc.GetProperty("cpuPercent").GetDouble());
    }

    [Fact]
    public async Task Screenshot_follows_the_setting()
    {
        var token = await Pair("phone");

        var allowed = await Get(token, "/api/screenshot");
        _allowScreen = false;
        var blocked = await Get(token, "/api/screenshot");

        Assert.Equal("image/jpeg", allowed.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task Phone_can_start_and_stop_a_watch()
    {
        var token = await Pair("phone");

        var started = await Post(token, "/api/watch", new { kind = "network", minutes = 5 });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
        Assert.Equal(WatchKind.Network, _watcher.Current?.Kind);

        await Post(token, "/api/watch/cancel", new { });
        Assert.Null(_watcher.Current);
    }

    [Fact]
    public async Task Watch_for_an_app_that_isnt_running_is_refused()
    {
        var token = await Pair("phone");

        var response = await Post(token, "/api/watch", new { kind = "app", process = "game" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("isn't running", (await Json(response)).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Automation_skips_the_shutdown_when_someone_uses_the_pc()
    {
        var token = await Pair("phone");
        _monitor.UserIdle = TimeSpan.FromMinutes(2);

        var response = await Post(token, "/api/shutdown", new { delayMinutes = 0, onlyIfIdleMinutes = 10 });

        Assert.True((await Json(response)).GetProperty("skipped").GetBoolean());
        Assert.Empty(_power.Scheduled);
    }

    [Fact]
    public async Task Automation_shuts_down_when_nobody_used_the_pc()
    {
        var token = await Pair("phone");
        _monitor.UserIdle = TimeSpan.FromMinutes(30);

        await Post(token, "/api/shutdown", new { delayMinutes = 0, onlyIfIdleMinutes = 10 });

        Assert.Single(_power.Scheduled);
    }

    // --- Helpers ------------------------------------------------------------

    private Task<HttpResponseMessage> Get(string token, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(ShutdownApi.TokenHeader, token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> Post(string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(ShutdownApi.TokenHeader, token);
        return _client.SendAsync(request);
    }

    private async Task<string> Pair(string name)
    {
        var response = await _client.PostAsJsonAsync("/api/pair", new { code = _pairing.Current, name });
        response.EnsureSuccessStatusCode();
        return (await Json(response)).GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> Send(string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/shutdown") { Content = JsonContent.Create(body) };
        request.Headers.Add(ShutdownApi.TokenHeader, token);
        return _client.SendAsync(request);
    }

    private async Task<JsonElement> GetStatus(string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        if (token is not null) request.Headers.Add(ShutdownApi.TokenHeader, token);
        return await Json(await _client.SendAsync(request));
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();
}
