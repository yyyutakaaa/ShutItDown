## Building the installer

1) Publish the app from the repo root. This bundles the .NET runtime, so users don't have to install anything:
```
dotnet publish src\ShutItDown.Tray -c Release -r win-x64 --self-contained true -o publish
```

   For a much smaller download that needs the **.NET 9 Desktop Runtime** and the **ASP.NET Core 9 Runtime** on the PC:
```
dotnet publish src\ShutItDown.Tray -c Release -o publish --self-contained false
```

2) Build the installer with Inno Setup:
```
iscc installer\ShutdownServerApp.iss
```
The installer reads the `publish\` folder at the repo root and writes `installer\output\ShutdownServer-Setup.exe`.

It uses the same AppId as version 2.x, so it installs over an old version, removes the old exe and the app moves an old "start with Windows" shortcut to the new setting on first launch.
