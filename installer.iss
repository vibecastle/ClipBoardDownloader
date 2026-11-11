[Setup]
AppName=CBDownloader
AppVersion=1.5.5
DefaultDirName={autopf}\CBDownloader
DefaultGroupName=CBDownloader
UninstallDisplayIcon={app}\CBDownloader.exe
SetupIconFile=CBDownloader\Assets\icon.ico
Compression=lzma2/max
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=CBDownloaderInstaller
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
VersionInfoVersion=1.5.5.0
VersionInfoDescription=CBDownloader - Lightweight Video and Audio Downloader
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

[Code]
var
  DownloadPage: TDownloadWizardPage;

function IsDotNet8DesktopInstalled(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  // Check common registry locations for .NET 8 Windows Desktop Runtime (x64)
  if RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  if RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  // Fallback check via Program Files directory
  if DirExists(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App')) then
  begin
    Result := True;
    Exit;
  end;
  // Fallback check via dotnet CLI
  if Exec('cmd.exe', '/c dotnet --list-runtimes | findstr /I "Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if ResultCode = 0 then
      Result := True;
  end;
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  if Progress = ProgressMax then
    Log(Format('Successfully downloaded file to {tmp}: %s', [FileName]));
  Result := True;
end;

procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(SetupMessage(msgWizardPreparing), SetupMessage(msgPreparingDesc), @OnDownloadProgress);
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  ResultCode: Integer;
  InstallerPath: String;
begin
  if (CurPageID = wpReady) and (not IsDotNet8DesktopInstalled()) then
  begin
    DownloadPage.Clear;
    DownloadPage.Add('https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe', 'windowsdesktop-runtime-win-x64.exe', '');
    DownloadPage.Show;
    try
      try
        DownloadPage.Download;
        InstallerPath := ExpandConstant('{tmp}\windowsdesktop-runtime-win-x64.exe');
        if FileExists(InstallerPath) then
        begin
          Exec(InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
        end;
        Result := True;
      except
        if DownloadPage.AbortedByUser then
          Log('Aborted by user.')
        else
          SuppressibleMsgBox(AddPeriod(GetExceptionMessage), mbCriticalError, MB_OK, IDOK);
        Result := False;
      end;
    finally
      DownloadPage.Hide;
    end;
  end
  else
    Result := True;
end;
