# CBDownloader ⚡📥

**CBDownloader** is a modern, standalone Windows application with a sleek **Black & Yellow** aesthetic that lets you download video or audio with **1 click**. Just copy any link, and a smart corner popup appears with video thumbnail and instant **MP4** or **MP3** download buttons.

No browser extensions required. No external dependencies needed. 100% standalone.

---

## ✨ Features

- **⚡ Smart 5-Second Toast Popup**: Copy any video link anywhere (browser, Discord, Telegram, etc.) and a sleek non-intrusive card pops up in the corner of your screen showing the video's thumbnail and quick **MP4** and **MP3** buttons.
- **🖤 Black & Yellow Cyber Aesthetic**: Modern, stylish dark theme with vibrant golden-yellow accents and high contrast.
- **🚀 100% Standalone & Zero Dependencies**: Fully self-contained installer for Windows. No need to install .NET runtime, browser extensions, or command-line tools manually.
- **🛠️ Self-Maintaining Dependencies**: Automatically manages and updates media engines (`yt-dlp` & `ffmpeg`) in the background.
- **🌐 1700+ Supported Platforms**: Works with **YouTube**, **Instagram Reels**, **TikTok**, **Twitter/X**, **Reddit**, **Facebook**, **Twitch**, **Vimeo**, **SoundCloud**, and [hundreds of others](https://github.com/yt-dlp/yt-dlp/blob/master/supportedsites.md).
- **📋 Playlist Support**: Easily preview and pick individual videos or audios from full playlists.
- **🏠 Background Tray Mode**: Sits quietly in the Windows system tray and does not interrupt your workflow.

---

## 🚀 Getting Started

### Installation
1. Download `CBDownloaderInstaller.exe` from the [Releases](https://github.com/vibecastle/ClipBoardDownloader/releases) page.
2. Run the installer and follow the quick setup.
3. Done! Whenever you copy a link, the quick download popup will appear in the bottom-right corner of your screen.

### Uninstallation
CBDownloader can be uninstalled cleanly at any time via **Windows Settings → Apps → Installed apps** (or **Control Panel → Programs and Features**).

---

## 💡 How It Works

1. **Copy**: Copy any supported video/audio link (`Ctrl + C`).
2. **Popup**: A toast notification appears in the corner with the video preview.
3. **Download**: Click **⚡ MP4** for video or **🎵 MP3** for audio. It downloads in the background while you keep working.

---

## 🔐 Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).

### Team roles and members

- **Authors / Committers:** [@vibecastle](https://github.com/vibecastle)
- **Reviewers:** [@vibecastle](https://github.com/vibecastle)
- **Approvers:** [@vibecastle](https://github.com/vibecastle)

### Scope & build provenance

Only `CBDownloader.exe`, `CBDownloader.dll`, and `CBDownloaderInstaller.exe` built from this public repository via GitHub-hosted runners in [`.github/workflows/release.yml`](.github/workflows/release.yml) are signed. Third-party open-source dependencies downloaded at runtime (`yt-dlp`, `ffmpeg`, `deno`) are never re-signed by this project.

### Privacy policy

> This program will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it.

Network requests are performed only upon explicit user action or operation:

- **Media metadata and downloads:** Fetching video metadata/thumbnails (including oEmbed endpoints for YouTube, TikTok, Vimeo, and SoundCloud) and downloading video/audio streams strictly for URLs copied or submitted by the user.
- **Media engine dependencies:** Downloading and updating open-source media tools (`yt-dlp`, `ffmpeg`, `deno`) from their official GitHub releases when required to execute a user-initiated download.
- **Manual update check:** Querying the GitHub Releases API (`https://api.github.com/repos/vibecastle/ClipBoardDownloader/releases/latest`) only when the user manually clicks **Check for Updates** in Settings.
- **Runtime prerequisite during setup:** Downloading the official Microsoft .NET 8 Windows Desktop Runtime installer (`https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe`) during installation only if the .NET 8 Desktop Runtime is not already installed.

CBDownloader collects no telemetry, analytics, or personal data. For external services contacted during those user-requested operations, please review the privacy policies of [GitHub](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement) and [Microsoft](https://privacy.microsoft.com/en-us/privacystatement).

---

## 📄 License

MIT License - see [LICENSE](LICENSE) for details.