[Setup]
AppName=CBDownloader
AppVersion=1.5.1
DefaultDirName={autopf}\CBDownloader
DefaultGroupName=CBDownloader
UninstallDisplayIcon={app}\CBDownloader.exe
SetupIconFile=CBDownloader\Assets\icon.ico
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=CBDownloaderInstaller
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
VersionInfoVersion=1.5.1.0
VersionInfoDescription=CBDownloader - Standalone Video and Audio Downloader
VersionInfoCompany=ClipBoardDownloader
VersionInfoCopyright=Copyright (C) 2026
VersionInfoProductName=CBDownloader

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start CBDownloader with Windows (Recommended)"; GroupDescription: "System Startup:"

[Files]
Source: "CBDownloader\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\CBDownloader"; Filename: "{app}\CBDownloader.exe"
Name: "{autodesktop}\CBDownloader"; Filename: "{app}\CBDownloader.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "CBDownloader"; ValueData: """{app}\CBDownloader.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\CBDownloader.exe"; Description: "{cm:LaunchProgram,CBDownloader}"; Flags: nowait postinstall skipifsilent
