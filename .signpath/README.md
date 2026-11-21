# SignPath Foundation Code Signing Setup

This guide explains the manual steps required to activate free open-source code signing for `CBDownloader` via the [SignPath Foundation](https://signpath.org/).

## 1. Prerequisites

1. Ensure Multi-Factor Authentication (MFA / 2FA) is enabled on your GitHub account (`vibecastle`) — mandatory per [SignPath Foundation terms](https://signpath.org/terms).
2. Keep the **Code signing policy** and **Privacy policy** sections visible in the root [README.md](../README.md) on `main`.

## 2. Apply to SignPath Foundation

1. Submit an application at <https://signpath.org/apply>.
2. Specify the public repository: `https://github.com/vibecastle/ClipBoardDownloader` (MIT License).
3. Wait for manual approval from the SignPath Foundation review team.

## 3. Configure SignPath After Approval

1. Enable MFA/2FA on your SignPath.io account.
2. Install the **SignPath GitHub App** on `vibecastle/ClipBoardDownloader` and link **GitHub.com** as the Trusted Build System in your SignPath Organization and Project.
3. Create a SignPath Project with slug `CBDownloader` (or configure `SIGNPATH_PROJECT_SLUG` in GitHub Variables if using a different slug).
4. Create two Artifact Configurations in the SignPath Project:
   - Slug `app-exe`: upload/paste the XML from [app-exe.xml](app-exe.xml).
   - Slug `installer-exe`: upload/paste the XML from [installer-exe.xml](installer-exe.xml).
   > **Note on Inno Setup PE padding:** Inno Setup compiles installers from a fixed-size stub buffer that may leave trailing spaces in `ProductName` / `ProductVersion` version resource strings. If `installer-exe` ever rejects an installer with a metadata mismatch, inspect the generated `CBDownloaderInstaller.exe` with SignPath's Artifact Configuration test/inspector tool and adjust `installer-exe.xml` if needed.

## 4. Configure GitHub Repository Secrets & Variables

In `https://github.com/vibecastle/ClipBoardDownloader/settings`:

### Secrets (`Secrets and variables -> Actions -> Secrets`)
- `SIGNPATH_API_TOKEN` — API token of a SignPath user with submitter permissions on the project/signing policy.

### Variables (`Secrets and variables -> Actions -> Variables`)
- `SIGNPATH_ORGANIZATION_ID` — Your SignPath Organization ID (GUID).
- Optional overrides (only needed if your SignPath slugs differ from the workflow defaults):
  - `SIGNPATH_PROJECT_SLUG` (default: `CBDownloader`)
  - `SIGNPATH_SIGNING_POLICY_SLUG` (default: `release-signing`)
  - `SIGNPATH_APP_ARTIFACT_CONFIG_SLUG` (default: `app-exe`)
  - `SIGNPATH_INSTALLER_ARTIFACT_CONFIG_SLUG` (default: `installer-exe`)

## 5. Testing & Publishing Releases

1. **Test run (`test-signing`):**
   - Go to **Actions → Build and Release Installer → Run workflow**.
   - Keep `signing_policy` as `test-signing` and run it.
   - SignPath signs both `CBDownloader.exe` and `CBDownloaderInstaller.exe` with the test certificate and uploads `CBDownloaderInstaller` as a workflow artifact (without creating a GitHub Release).
2. **Production release (`release-signing`):**
   - Bump versions in `CBDownloader/CBDownloader.csproj` and `installer.iss`, commit, and push a version tag (`vX.Y.Z`).
   - Monitor the workflow and **manually approve both signing requests** in your SignPath dashboard (`CBDownloader.exe` first, then `CBDownloaderInstaller.exe` after Inno Setup compiles it) within the 1-hour (`3600` seconds) timeout window.
