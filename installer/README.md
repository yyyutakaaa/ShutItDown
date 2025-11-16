## Building the installer (framework-dependent, small download)

1) Publish the app (small download, requires .NET Desktop Runtime 6):
```
dotnet publish -c Release -o publish --self-contained false
```
This produces `publish\ShutdownServerApp.exe` and supporting files without bundling the runtime (~few MB instead of ±150 MB). If the runtime is not present, install ".NET Desktop Runtime 6.x".

2) Build the installer with Inno Setup:
```
iscc installer\ShutdownServerApp.iss
```
The installer expects the `publish\` folder next to the repo root and writes output to `installer\output\ShutdownServer-Setup.exe`.

3) Optional: create a self-contained single EXE (bigger download, no runtime needed):
```
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true
```
Then update `PublishDir` in `installer/ShutdownServerApp.iss` to point to that publish folder before building the installer.
