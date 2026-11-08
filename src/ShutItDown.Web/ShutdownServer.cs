using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace ShutItDown.Web;

/// <summary>Starts and stops the web server that the phone talks to.</summary>
public sealed class ShutdownServer : IAsyncDisposable
{
    private readonly ShutdownApiDependencies _deps;
    private WebApplication? _app;

    public ShutdownServer(ShutdownApiDependencies deps)
    {
        _deps = deps;
    }

    public bool IsRunning => _app is not null;

    public int? Port { get; private set; }

    /// <summary>Builds the app without starting it. <paramref name="configure"/> lets tests swap in a test server.</summary>
    public static WebApplication Build(ShutdownApiDependencies deps, int port, Action<WebApplicationBuilder>? configure = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.ListenAnyIP(port));
        configure?.Invoke(builder);

        var app = builder.Build();

        var pages = new EmbeddedFileProvider(typeof(ShutdownServer).Assembly, "ShutItDown.Web.wwwroot");
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = pages });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = pages });
        app.MapShutdownApi(deps);

        return app;
    }

    public async Task StartAsync(int port)
    {
        if (_app is not null)
            return;

        var app = Build(_deps, port);
        try
        {
            await app.StartAsync();
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        _app = app;
        Port = port;
    }

    public async Task StopAsync()
    {
        if (_app is not { } app)
            return;

        _app = null;
        Port = null;
        await app.StopAsync();
        await app.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(StopAsync());
}
