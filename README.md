# HDD Cache Warmer

A Windows utility that warms the OS file cache for a folder or drive by reading every file
sequentially, so repeated access to the same files is served from RAM instead of the disk.

This repo implements the PRD end-to-end:

- **HDDCacheWarmer.Core** — the engine: recursive file walker, sequential reader, pause/resume/
  cancel, progress + result models. Pure .NET, no UI dependency, unit-testable on its own.
- **HDDCacheWarmer.App** — the WPF front end: progress window, settings window, JSON-persisted
  settings, and Explorer "Warm Windows Cache" context menu registration.
- **installer/** — an Inno Setup script skeleton for a standard installer, plus the
  `--register-context-menu` / `--unregister-context-menu` CLI hooks it calls into.

## Requirements to build

- Windows 10/11
- Visual Studio 2022 (17.8+) or the .NET 8 SDK
- Workload: ".NET desktop development"

## Build & run

```
git clone <this repo>
cd HDDCacheWarmer
dotnet build
dotnet run --project src\HDDCacheWarmer.App
```

Or open `HDDCacheWarmer.sln` in Visual Studio and hit F5.

## Enabling the Explorer context menu

Open the app → **Settings** → check "Add 'Warm Windows Cache' to the Explorer right-click menu".
This writes to `HKEY_CURRENT_USER\Software\Classes`, so **no administrator rights are needed**.
Right-click any folder or drive afterward to see the new entry; it launches the app with that
path pre-filled and immediately starts warming.

## Design notes / how it maps to the PRD

- **Sequential reads**: each file is opened with `FileOptions.SequentialScan` and read in a
  configurable-size buffer (default 4 MB), never loaded fully into memory — this satisfies the
  ">100 GB file" and "avoid loading entire files into memory" requirements.
- **Two-phase run**: a lightweight metadata-only *scan* phase first counts total files/bytes so
  the progress bar, percentage, and ETA are accurate; the *warm* phase does the actual sequential
  reads. This is what most other "prefetch" tools skip, and why this one can show a real ETA.
- **Pause/resume/cancel**: implemented with a `ManualResetEventSlim` gate + `CancellationToken`,
  checked between every file (and mid-scan) so pausing is near-instant.
- **Error resilience**: `FileSystemWalker` and `CacheWarmerEngine` both catch
  `UnauthorizedAccessException` / `IOException` per file or per directory and keep going; nothing
  in the walk or read path can abort the whole run because of one bad file.
- **No admin required**: the app manifest requests `asInvoker`, and the context-menu registration
  targets `HKCU`, not `HKLM`.

## Known limitations (flagged honestly, not swept under the rug)

- **Scan-phase memory**: the current implementation buffers the file listing in memory during the
  scan phase (a `List<FileInfo>`) so it can support `LargestFirst`/`SmallestFirst`/`Alphabetical`
  ordering and give an accurate byte-based ETA. For truly extreme trees (tens of millions of
  files) this list can grow to a few hundred MB. If you need to support that scale with strictly
  bounded memory, add a **streaming mode** that skips the scan phase (no total/ETA, `TreeOrder`
  only) — the walker already supports this via `FileSystemWalker.Walk` directly.
- **Not yet implemented from the PRD's "Smart Features" / "Future Enhancements" lists**: system
  tray icon, scheduled warming, auto-warm-on-drive-connect, SMART/temperature monitoring, live
  throughput graphs, and a CLI for scripting. `AppSettings` already has the data model for
  favorites/recents/schedules/auto-warm so these are additive, not architectural changes.
- **Folder picker** uses the classic WinForms `FolderBrowserDialog` to avoid an extra NuGet
  dependency; swap in `Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog` for a more
  modern look if desired.
- **Installer** is a skeleton Inno Setup script, not a signed, tested MSI/EXE — treat it as a
  starting point, not something to ship as-is.
- This was written and reviewed as text (no Windows machine available to compile it), so treat
  the first build as a normal "build and fix any typos/API mismatches" pass rather than assuming
  zero errors.

## Suggested next steps

1. Build it once in Visual Studio and fix anything the compiler flags (I was not able to compile
   this in the sandbox I wrote it in).
2. Wire up a `NotifyIcon`-based tray mode for the "Hide" button and startup-in-tray setting.
3. Add a CLI mode (`HDDCacheWarmer.exe --path X --no-ui`) for the scripting/automation future
   enhancement — the engine already has everything needed; it just needs an entry point that
   skips WPF.
