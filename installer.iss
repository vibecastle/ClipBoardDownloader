[Setup]
AppName=CBDownloader
AppVersion=1.5.11
AppPublisher=ClipBoardDownloader
AppPublisherURL=https://github.com/vibecastle/ClipBoardDownloader
PrivilegesRequired=lowest
DefaultDirName={autopf}\CBDownloader
DefaultGroupName=CBDownloader
UninstallDisplayIcon={app}\CBDownloader.exe
SetupIconFile=CBDownloader\Assets\icon.ico
Compression=zip
SolidCompression=no
OutputDir=Output
OutputBaseFilename=CBDownloaderInstaller
ArchitecturesInstallIn64BitMode=x64
ArchitecturesAllowed=x64
VersionInfoVersion=1.5.11.0
VersionInfoDescription=CBDownloader - Lightweight Video and Audio Downloader
VersionInfoCompany=ClipBoardDownloader
VersionInfoCopyright=Copyright (C) 2026
VersionInfoProductName=CBDownloader
VersionInfoProductVersion=1.5.11
VersionInfoProductTextVersion=1.5.11

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
Filename: "{app}\CBDownloader.exe"; Parameters: "--show"; Description: "{cm:LaunchProgram,CBDownloader}"; Flags: nowait postinstall

[Code]
var
  DownloadPage: TDownloadWizardPage;

function HasDotNet8InRegKey(const SubKey: String): Boolean;
var
  Versions: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM, SubKey, Versions) then
  begin
    for I := 0 to GetArrayLength(Versions) - 1 do
    begin
      if Copy(Versions[I], 1, 2) = '8.' then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function IsDotNet8DesktopInstalled(): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;
  // Check 64-bit registry locations for .NET 8.x Windows Desktop Runtime (x64)
  if HasDotNet8InRegKey('SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') or
     HasDotNet8InRegKey('SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App') then
  begin
    Result := True;
    Exit;
  end;
  // Fallback check via Program Files directory for an 8.* runtime folder
  if FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*'), FindRec) then
  begin
    try
      repeat
        if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
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
          ShellExec('runas', InstallerPath, '/install /quiet /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode);
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
