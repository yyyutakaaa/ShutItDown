# Shutdown Server

Turn off your Windows PC from your phone. Shutdown Server runs in the system tray and serves a small web page for your phone.

1. Scan the QR code in the app once. This pairs your phone.
2. Add the page to your home screen.
3. From then on: tap the icon, tap the power button twice, done.

With [Tailscale](https://tailscale.com) on both the PC and the phone, the QR code uses the PC's Tailscale address, so the icon works at home and away. Without Tailscale it works on the same Wi-Fi.

---

## Features

- **Shut down from your phone** – right away, or in 15 min, 30 min or 1 hour. A scheduled shutdown can be cancelled from the phone or the PC.
- **Pair once** – the QR code holds a one-time code (valid 10 minutes). The phone trades it for its own secret token, so it never needs a PIN. Paired phones are listed in the app and can be removed one by one.
- **Only paired phones** – a phone that isn't paired can't do anything, unless you set a backup PIN (4–8 digits, stored as a salted hash; after 5 wrong tries a phone waits a minute).
- **System tray app** – closing the window keeps the server running; a second launch just opens the window.
- **Start with Windows** – one checkbox, no admin rights needed.
- **Live view** – CPU, memory, download speed, how long nobody touched the PC, the open apps, and a screenshot on request (can be turned off).
- **Shut down when it's done** – when the PC is idle (renders, updates), when downloads stop, or when a chosen app closes. It never fires while someone uses the PC and always gives a minute to cancel.
- **Unsaved work warning** – before shutting down, the phone lists windows that look unsaved (`*file`, `● file`, `Untitled`, `Document1`…).
- **Shut down when you leave home** – a step-by-step recipe for an iPhone Shortcuts automation. It only turns the PC off if nobody used it in the last 10 minutes.
- **Advanced settings** – port, a short delay for "now" so you can still cancel on the PC, a notification on the PC when a phone starts a shutdown, the wrong-PIN limit, and a reset.
- **Settings are saved** – they survive a restart (`%APPDATA%\ShutItDown\settings.json`).

---

## Project structure

```
src/
  ShutItDown.Core/   shutdown, PIN hashing, lockout, settings, network address (no UI)
  ShutItDown.Web/    HTTP API + the phone page (wwwroot/, embedded in the DLL)
  ShutItDown.Tray/   Windows tray app and settings window
tests/
  ShutItDown.Tests/  unit tests and API tests (with a fake shutdown, so tests never turn your PC off)
installer/           Inno Setup script
tools/               generate-logo.ps1 draws the app icon (ICO + PNG) from code
```

### API

| Method | Path            | Body                                   | What it does                         |
|--------|-----------------|----------------------------------------|--------------------------------------|
| GET    | `/api/status`   | –                                      | PC name, whether this phone is paired, pending shutdown |
| POST   | `/api/pair`     | `{ "code": "...", "name": "Pixel 8" }` | Trades the QR code's one-time code for a device token |
| POST   | `/api/shutdown` | `{ "delayMinutes": 0, "pin": "1234" }` | Shuts down now or after the delay. Needs the `X-Device-Token` header, or the backup PIN |
| POST   | `/api/cancel`   | –                                      | Cancels a pending shutdown           |

Shutdown only accepts `POST` with a JSON body. A link preview, a bookmark or another website can't trigger it.

---

## Build and run

Needs the .NET 9 SDK.

```
dotnet build
dotnet test
dotnet run --project src/ShutItDown.Tray
```

See [installer/README.md](installer/README.md) to build `ShutdownServer-Setup.exe`.

---

## Releasing a new version

The app checks GitHub for a newer release every time it starts and offers it in its own update window ("Update now", "Later", "Skip this version"). It downloads the installer, checks its SHA-256, installs quietly and starts again. Settings and paired phones stay.

To publish a version, either:

```
git tag v3.1.0
git push origin v3.1.0
```

or open **Actions → Release → Run workflow** on GitHub and enter `3.1.0`.

The [Release workflow](.github/workflows/release.yml) runs the tests, builds the installer with that version number and creates the GitHub release with `ShutdownServer-Setup.exe` and `ShutdownServer-Setup.exe.sha256`. Release notes are generated from the commits and shown in the update window.

Versions 2.x don't have the updater, so they need to install 3.x once by hand.

---

## Security

- The server listens on port 5050 (changeable) on all network adapters. Windows asks once whether to allow it through the firewall: allow it for **private** networks only.
- Only paired phones can shut down the PC. The phone keeps its token in the page address (`#k=...`), so the home screen icon carries it; treat that shortcut like a key and remove the phone in the app if you lose it.
- Traffic is plain HTTP on your LAN. Don't expose the port to the internet.

---

## Disclaimer

A shutdown closes open programs without asking, so unsaved work is lost. The creator is not responsible for any loss or damage caused by using this application. Use at your own risk.

Made by Mehdi
