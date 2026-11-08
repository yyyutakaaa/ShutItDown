using ShutItDown.Core.Settings;

namespace ShutItDown.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ShutItDownTests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "settings.json");

    [Fact]
    public void Missing_file_gives_defaults()
    {
        var settings = new SettingsStore(FilePath).Load();

        Assert.Equal(AppSettings.DefaultPort, settings.Port);
        Assert.Null(settings.PinHash);
    }

    [Fact]
    public void Saved_settings_load_back()
    {
        var store = new SettingsStore(FilePath);
        store.Save(new AppSettings { Port = 6000, PinHash = "v1.1.AA==.AA==" });

        var loaded = store.Load();

        Assert.Equal(6000, loaded.Port);
        Assert.Equal("v1.1.AA==.AA==", loaded.PinHash);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, "{ not json");

        Assert.Equal(AppSettings.DefaultPort, new SettingsStore(FilePath).Load().Port);
    }

    [Fact]
    public void Invalid_port_falls_back_to_default_but_keeps_the_pin()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "Port": 80, "PinHash": "keep-me" }""");

        var loaded = new SettingsStore(FilePath).Load();

        Assert.Equal(AppSettings.DefaultPort, loaded.Port);
        Assert.Equal("keep-me", loaded.PinHash);
    }

    [Fact]
    public void Out_of_range_advanced_settings_fall_back_to_defaults()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(FilePath, """{ "GraceSeconds": -5, "MaxPinAttempts": 0, "NotifyOnRequest": false }""");

        var loaded = new SettingsStore(FilePath).Load();

        Assert.Equal(0, loaded.GraceSeconds);
        Assert.Equal(AppSettings.DefaultMaxPinAttempts, loaded.MaxPinAttempts);
        Assert.False(loaded.NotifyOnRequest);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, recursive: true);
    }
}
