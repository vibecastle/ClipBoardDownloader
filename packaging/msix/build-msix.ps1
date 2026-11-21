param(
    [string]$IdentityName = "vibecastle.CBDownloader",
    [string]$Publisher = "CN=vibecastle",
    [string]$PublisherDisplayName = "ClipBoardDownloader",
    [string]$Version = "",
    [string]$OutputDir = "Output",
    [switch]$ReuseLocalAppDataBin,
    [switch]$SelfSign
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-SdkTools {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    )

    foreach ($base in $candidates) {
        if (-not (Test-Path $base)) { continue }
        $versions = Get-ChildItem -Path $base -Directory -Filter "10.*" |
            Sort-Object { [version]$_.Name } -Descending

        foreach ($verDir in $versions) {
            $x64Dir = Join-Path $verDir.FullName "x64"
            $makeAppx = Join-Path $x64Dir "makeappx.exe"
            $makePri = Join-Path $x64Dir "makepri.exe"
            $signTool = Join-Path $x64Dir "signtool.exe"
            if ((Test-Path $makeAppx) -and (Test-Path $makePri)) {
                return @{
                    MakeAppx = $makeAppx
                    MakePri  = $makePri
                    SignTool = $signTool
                }
            }
        }
    }

    throw "Windows 10/11 SDK tools (makeappx.exe / makepri.exe) not found."
}

function Normalize-MsixVersion([string]$RawVersion) {
    $clean = $RawVersion.Trim().TrimStart('v', 'V')
    $parts = $clean -split '\.'
    if ($parts.Count -lt 2) {
        throw "Invalid version '$RawVersion'. Expected format Major.Minor.Build[.Revision]."
    }
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $build = if ($parts.Count -ge 3) { [int]$parts[2] } else { 0 }
    # Microsoft Store requires the 4th (revision) segment to be 0.
    return "$major.$minor.$build.0"
}

function Ensure-BundledTools([string]$CacheDir, [switch]$UseLocal) {
    New-Item -ItemType Directory -Force -Path $CacheDir | Out-Null

    $ytDlpPath = Join-Path $CacheDir "yt-dlp.exe"
    $denoPath = Join-Path $CacheDir "deno.exe"
    $ffmpegPath = Join-Path $CacheDir "ffmpeg.exe"
    $ffprobePath = Join-Path $CacheDir "ffprobe.exe"

    if ($UseLocal) {
        $localBin = Join-Path $env:APPDATA "CBDownloader\bin"
        foreach ($req in @("yt-dlp.exe", "deno.exe", "ffmpeg.exe")) {
            $src = Join-Path $localBin $req
            if (-not (Test-Path $src)) {
                throw "Required tool '$src' not found for -ReuseLocalAppDataBin."
            }
            Copy-Item -Path $src -Destination (Join-Path $CacheDir $req) -Force
        }
        $localProbe = Join-Path $localBin "ffprobe.exe"
        if (Test-Path $localProbe) {
            Copy-Item -Path $localProbe -Destination $ffprobePath -Force
        }
        return @{
            YtDlp   = $ytDlpPath
            Deno    = $denoPath
            FFmpeg  = $ffmpegPath
            FFprobe = $ffprobePath
        }
    }

    if (-not (Test-Path $ytDlpPath)) {
        Write-Host "Downloading official yt-dlp.exe..."
        Invoke-WebRequest -Uri "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe" -OutFile $ytDlpPath -UseBasicParsing
    }

    if (-not (Test-Path $denoPath)) {
        Write-Host "Downloading official deno.exe..."
        $denoZip = Join-Path $CacheDir "deno.zip"
        $denoTmp = Join-Path $CacheDir "_deno_tmp"
        if (Test-Path $denoTmp) { Remove-Item -Recurse -Force $denoTmp }
        Invoke-WebRequest -Uri "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip" -OutFile $denoZip -UseBasicParsing
        Expand-Archive -Path $denoZip -DestinationPath $denoTmp -Force
        $foundDeno = Get-ChildItem -Path $denoTmp -Recurse -Filter "deno.exe" | Select-Object -First 1
        if (-not $foundDeno) { throw "deno.exe not found in downloaded archive." }
        Copy-Item -Path $foundDeno.FullName -Destination $denoPath -Force
        Remove-Item -Recurse -Force $denoTmp, $denoZip -ErrorAction SilentlyContinue
    }

    if (-not (Test-Path $ffmpegPath)) {
        Write-Host "Downloading official BtbN FFmpeg LGPL x64 build..."
        $ffZip = Join-Path $CacheDir "ffmpeg-lgpl.zip"
        $ffTmp = Join-Path $CacheDir "_ff_tmp"
        if (Test-Path $ffTmp) { Remove-Item -Recurse -Force $ffTmp }
        Invoke-WebRequest -Uri "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-lgpl.zip" -OutFile $ffZip -UseBasicParsing
        Expand-Archive -Path $ffZip -DestinationPath $ffTmp -Force
        $foundFfmpeg = Get-ChildItem -Path $ffTmp -Recurse -Filter "ffmpeg.exe" | Select-Object -First 1
        if (-not $foundFfmpeg) { throw "ffmpeg.exe not found in downloaded archive." }
        Copy-Item -Path $foundFfmpeg.FullName -Destination $ffmpegPath -Force
        $foundFfprobe = Get-ChildItem -Path $ffTmp -Recurse -Filter "ffprobe.exe" | Select-Object -First 1
        if ($foundFfprobe) {
            Copy-Item -Path $foundFfprobe.FullName -Destination $ffprobePath -Force
        }
        Remove-Item -Recurse -Force $ffTmp, $ffZip -ErrorAction SilentlyContinue
    }

    return @{
        YtDlp   = $ytDlpPath
        Deno    = $denoPath
        FFmpeg  = $ffmpegPath
        FFprobe = $ffprobePath
    }
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ([string]::IsNullOrWhiteSpace($Version)) {
    $issPath = Join-Path $repoRoot "installer.iss"
    $match = Select-String -Path $issPath -Pattern '^AppVersion=(.+)$' | Select-Object -First 1
    if (-not $match) { throw "AppVersion not found in $issPath." }
    $Version = $match.Matches[0].Groups[1].Value.Trim()
}

$msixVersion = Normalize-MsixVersion $Version
$sdk = Resolve-SdkTools
$resolvedOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDir)) { $OutputDir } else { Join-Path $repoRoot $OutputDir }
New-Item -ItemType Directory -Force -Path $resolvedOutputDir | Out-Null

$cacheDir = Join-Path $repoRoot ".msix-cache"
$stagingDir = Join-Path $repoRoot ".msix-staging"
$priConfig = Join-Path $env:TEMP ("cbdownloader-priconfig-" + [Guid]::NewGuid().ToString("N") + ".xml")
$msixPath = Join-Path $resolvedOutputDir "CBDownloader_${msixVersion}_x64.msix"

try {
    if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir }
    New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

    $tools = Ensure-BundledTools -CacheDir $cacheDir -UseLocal:$ReuseLocalAppDataBin

    Write-Host "Publishing CBDownloader for MSIX ($msixVersion)..."
    dotnet publish (Join-Path $repoRoot "CBDownloader\CBDownloader.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:MsixBuild=true `
        -p:DebugType=none `
        -p:DebugSymbols=false `
        -o $stagingDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }

    # Remove any stray PDB symbols brought by self-contained runtime files
    Get-ChildItem -Path $stagingDir -Filter "*.pdb" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

    Copy-Item -Path $tools.YtDlp -Destination (Join-Path $stagingDir "yt-dlp.exe") -Force
    Copy-Item -Path $tools.Deno -Destination (Join-Path $stagingDir "deno.exe") -Force
    Copy-Item -Path $tools.FFmpeg -Destination (Join-Path $stagingDir "ffmpeg.exe") -Force
    if (Test-Path $tools.FFprobe) {
        Copy-Item -Path $tools.FFprobe -Destination (Join-Path $stagingDir "ffprobe.exe") -Force
    }

    $assetsDest = Join-Path $stagingDir "Assets"
    New-Item -ItemType Directory -Force -Path $assetsDest | Out-Null
    Copy-Item -Path (Join-Path $PSScriptRoot "Assets\*") -Destination $assetsDest -Recurse -Force
    Copy-Item -Path (Join-Path $PSScriptRoot "THIRD_PARTY_NOTICES.txt") -Destination (Join-Path $stagingDir "THIRD_PARTY_NOTICES.txt") -Force

    $manifestTemplate = Get-Content -Raw (Join-Path $PSScriptRoot "AppxManifest.template.xml")
    $esc = { param([string]$v) [System.Security.SecurityElement]::Escape($v) }
    $manifestXml = $manifestTemplate `
        -replace '\{\{PACKAGE_IDENTITY_NAME\}\}', (& $esc $IdentityName) `
        -replace '\{\{PACKAGE_PUBLISHER\}\}', (& $esc $Publisher) `
        -replace '\{\{PACKAGE_VERSION\}\}', (& $esc $msixVersion) `
        -replace '\{\{PACKAGE_ARCHITECTURE\}\}', 'x64' `
        -replace '\{\{PUBLISHER_DISPLAY_NAME\}\}', (& $esc $PublisherDisplayName)

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText((Join-Path $stagingDir "AppxManifest.xml"), $manifestXml, $utf8NoBom)

    Write-Host "Generating resources.pri..."
    & $sdk.MakePri createconfig /cf "$priConfig" /dq en-US /pv 10.0.0 /o
    if ($LASTEXITCODE -ne 0) { throw "makepri createconfig failed with exit code $LASTEXITCODE." }

    & $sdk.MakePri new /pr "$stagingDir" /cf "$priConfig" /mn "$stagingDir\AppxManifest.xml" /of "$stagingDir\resources.pri" /o
    if ($LASTEXITCODE -ne 0) { throw "makepri new failed with exit code $LASTEXITCODE." }

    Write-Host "Packing MSIX -> $msixPath..."
    & $sdk.MakeAppx pack /d "$stagingDir" /p "$msixPath" /o
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed with exit code $LASTEXITCODE." }

    if ($SelfSign) {
        if (-not (Test-Path $sdk.SignTool)) { throw "signtool.exe not found at $($sdk.SignTool)." }
        $existingCert = Get-ChildItem Cert:\CurrentUser\My |
            Where-Object { $_.Subject -eq $Publisher -and $_.EnhancedKeyUsageList.ObjectId -contains "1.3.6.1.5.5.7.3.3" } |
            Select-Object -First 1

        if (-not $existingCert) {
            $existingCert = New-SelfSignedCertificate `
                -Type Custom `
                -Subject $Publisher `
                -KeyUsage DigitalSignature `
                -CertStoreLocation "Cert:\CurrentUser\My" `
                -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
        }

        & $sdk.SignTool sign /fd SHA256 /sha1 $existingCert.Thumbprint "$msixPath"
        if ($LASTEXITCODE -ne 0) { throw "signtool sign failed with exit code $LASTEXITCODE." }
    }

    Write-Host "MSIX package ready: $msixPath"
}
finally {
    if (Test-Path $stagingDir) { Remove-Item -Recurse -Force $stagingDir -ErrorAction SilentlyContinue }
    if (Test-Path $priConfig) { Remove-Item -Force $priConfig -ErrorAction SilentlyContinue }
}
