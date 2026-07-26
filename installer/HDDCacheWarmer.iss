; Inno Setup script for HDD Cache Warmer.
; Build with Inno Setup (https://jrsoftware.org/isinfo.php) after publishing the app:
;   dotnet publish src\HDDCacheWarmer.App -c Release -r win-x64 --self-contained false -o publish
; then compile this script (ISCC.exe HDDCacheWarmer.iss).

[Setup]
AppId={{7C7A6C7E-6C1E-4B7C-9C36-HDDCACHEWARM}}
AppName=HDD Cache Warmer
AppVersion=1.0.0
DefaultDirName={autopf}\HDDCacheWarmer
DefaultGroupName=HDD Cache Warmer
; No admin required for normal operation; install still needs to write to Program Files,
; which does require elevation for install-only. Use PrivilegesRequired=lowest + a
; per-user directory instead if you want a fully admin-free install.
PrivilegesRequired=lowest
DefaultDirName={autopf}\HDDCacheWarmer
OutputBaseFilename=HDDCacheWarmerSetup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: recursesubdirs

[Icons]
Name: "{group}\HDD Cache Warmer"; Filename: "{app}\HDDCacheWarmer.exe"
Name: "{autodesktop}\HDD Cache Warmer"; Filename: "{app}\HDDCacheWarmer.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop icon"; GroupDescription: "Additional icons:"

[Run]
; Register the Explorer context menu entry (HKCU, no elevation needed) right after install.
Filename: "{app}\HDDCacheWarmer.exe"; Parameters: "--register-context-menu"; Flags: runhidden nowait
Filename: "{app}\HDDCacheWarmer.exe"; Description: "Launch HDD Cache Warmer"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{app}\HDDCacheWarmer.exe"; Parameters: "--unregister-context-menu"; Flags: runhidden

; NOTE: wire up "--register-context-menu" / "--unregister-context-menu" as command-line
; switches in App.xaml.cs OnStartup that call ContextMenuRegistrar.Register()/Unregister()
; and then immediately Shutdown() -- not yet included in App.xaml.cs, add before shipping.
