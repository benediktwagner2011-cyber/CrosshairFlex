# CrosshairFlex

CrosshairFlex is a lightweight Windows overlay crosshair utility.

## Updated Folder Structure

```text
CrosshairFlex/
|- src/
|  |- CrosshairFlex.Desktop/
|  |  |- Interop/
|  |  |- Models/
|  |  |- Resources/
|  |  |  |- Languages/
|  |  |     |- en.json
|  |  |     |- de.json
|  |  |- Services/
|  |  |- App.xaml
|  |  |- MainWindow.xaml
|  |  |- OverlayWindow.xaml
|- installer/
|  |- CrosshairFlex.iss
|- scripts/
|  |- build-desktop.ps1
|  |- build-installer.ps1
|  |- update-version.ps1
|- web/
|  |- index.html
|  |- styles.css
|  |- script.js
|  |- CrosshairFlex_Setup.exe
|  |- assets/
|     |- logo.svg
|     |- overlay-preview.svg
|     |- profile-preview.svg
|     |- settings-preview.svg
|- artifacts/
|- CrosshairFlex.sln
```

## Desktop Architecture

- Native stack only: C# + WPF.
- Overlay is a separate transparent click-through always-on-top window.
- Passive keyboard handling via `WH_KEYBOARD_LL`.
- Hook always calls `CallNextHookEx()` and never suppresses input.
- Hook does no heavy work: it only checks state, enqueues profile id, signals worker.
- Profile switching and redraw run in a background/UI path, not inside the hook.

## Performance Notes

- No polling loops.
- No timers for profile switching.
- Event-driven switching and rendering.
- Overlay redraw is skipped when profile/render state is unchanged.
- Redraw occurs only on profile/settings changes or display/bounds changes.
- Profiles are loaded once at startup from config.

## Anti-Cheat Safety Design

CrosshairFlex intentionally does **not**:
- inject into game processes,
- read game memory,
- simulate input,
- modify kernel state or use drivers,
- attach debugger handles to game processes.

Safe Mode:
- Optional `Safe Mode` enforces modifier-based switching (`Ctrl/Alt/Shift/Win + Key`).
- UI warning: `Single-key switching may not work in some anti-cheat protected games.`

## Build Instructions

Prerequisites:
- Windows 10/11
- .NET SDK 8+

Build:

```powershell
dotnet build .\src\CrosshairFlex.Desktop\CrosshairFlex.Desktop.csproj -c Release
```

Publish single-file desktop app:

```powershell
.\scripts\build-desktop.ps1
```

Output:
- `artifacts/publish/win-x64/CrosshairFlex.exe`

## Installer Instructions

Prerequisites:
- Inno Setup 6 (`ISCC.exe`)

Build installer:

```powershell
.\scripts\build-installer.ps1
```

Output:
- `artifacts/installer/CrosshairFlex_Setup.exe`

If Inno Setup is in a custom location:

```powershell
.\scripts\build-installer.ps1 -InnoSetupCompiler "D:\Tools\Inno Setup 6\ISCC.exe"
```

## Website (Pure Static)

The landing site is now framework-free:
- `web/index.html`
- `web/styles.css`
- `web/script.js`
- `web/assets/*`

Download CTA links directly to:
- `web/CrosshairFlex_Setup.exe`

Deploy `web/` as static files on any host (Cloudflare Pages static, GitHub Pages, Netlify static, S3, etc.).
