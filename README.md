# Image Metadata Cleaner for Windows

## Install and open

1. Double-click **Image Metadata Cleaner Setup.exe**.
2. Click **Install**. The desktop shortcut is selected by default.
3. Open **Image Metadata Cleaner** from the desktop or Start Menu.

The installer works for the current Windows account and does not ask for administrator access. The app and its .NET runtime are included, so users do not need to install Python or .NET separately. It runs locally and does not send images to a website.

If Windows warns that the app is from an unknown publisher, that is because this build is not code-signed. Only install a copy you trust.

## Use the app

1. Add JPEG images or supported video files, or choose a folder. Folder selection includes subfolders. Supported video formats are MP4, MOV, AVI, M4V, M4A, 3GP, 3G2, WebM and MKV.
2. Choose whether to remove C2PA alone or C2PA plus all XMP.
3. Choose where to save the batch and select **Inspect and create cleaned copies**.

Supported videos are inspected automatically when they are added to the file list; when one video is added, its inspection report opens automatically. You can also select a video and choose **Inspect selected video**. The inspector supports MP4, MOV, AVI, M4V, M4A, 3GP, 3G2, WebM and MKV containers where FFprobe can read them. It reports container and stream properties, codecs, dimensions, frame rates, audio details, timestamps, recognized software evidence, AI-related metadata indicators, C2PA-related fields, and warnings. Use **Copy JSON** or **Export JSON** in the inspection window to save the structured report.

The installer bundles FFmpeg and, when present in the downloaded FFmpeg archive, FFprobe. ExifTool is optional: install it separately and put `exiftool.exe` on PATH (or beside the app) to inspect additional XMP/IPTC and application-specific tags. C2PA manifests are detected when exposed as metadata, but this build does not validate C2PA signatures or signer trust; it never treats missing credentials as proof that a video was not AI-generated or edited. Software names are reported only as metadata evidence and do not prove the complete editing history.

The app makes a new dated folder for each batch. Original files are never modified. It saves a CSV report, a JSON report, and a short explanation beside the cleaned copies. Unsupported files and damaged media are reported as skipped.

**Remove C2PA only** keeps XMP, EXIF, IPTC, ICC profiles, JFIF and other JPEG metadata. Provenance details that may also appear in XMP remain.

**Remove C2PA and all XMP** removes the whole XMP packet, including possible ordinary descriptions, dates, copyright details, software information and edit history. EXIF, IPTC, ICC and JFIF remain. Neither option recompresses the JPEG image scan.

C2PA values in the report are readable embedded claims. The app does not validate signatures, certificates or trust. ExifTool and Adobe's C2PA inspector are not bundled.

For MP4, MOV, M4V, M4A, 3GP and 3G2 files, the app uses the bundled FFmpeg engine to remove standard container metadata and chapters while copying the audio/video streams without re-encoding. Vendor-specific or signed provenance boxes are not guaranteed to be removed; the report calls out this limitation.

Video inspection and cleaning are local operations. The inspector does not upload media or metadata. FFprobe and optional ExifTool are invoked with argument arrays and read-only inputs; malformed or unsupported files produce an error or warning report rather than a clean result. Platform labels from services such as Instagram can also use signals beyond file metadata, so removing metadata cannot guarantee removal or prevention of an AI-related label.

## Uninstall

Use **Settings → Apps → Installed apps** (or **Apps & features** on older Windows versions), choose **Image Metadata Cleaner**, and select **Uninstall**. Your source images and saved reports are not removed.

## Build the installer from source

The source project is in `desktop/`, the setup program is in `installer/`, and `build-package.ps1` builds the x64 installer. Building requires the .NET 8 Windows Desktop SDK and the Windows .NET Framework compiler. The build bundles the .NET runtime so end users do not need to download it separately.

## Tests and validation

Build the desktop project with `dotnet build desktop/MetadataCleaner.csproj -c Release --no-restore`. This repository currently has no automated test project or committed media fixtures; inspect malformed files and files requiring FFprobe/ExifTool on a Windows machine with those tools available before release.

## Updates

The app quietly checks GitHub Releases when it opens and also has a **Check for updates** link. It asks before downloading or installing an update. Downloads use HTTPS, and the app checks GitHub's published SHA-256 digest when one is available. Installation replaces the app files only; source images and saved reports are kept.

To publish an update, push a version tag such as `v1.1.1`. The Windows build workflow creates the installer and attaches it to a GitHub release. The repository must be public for every installed copy to check releases without asking users to sign in. The workflow embeds the repository name in each build.

The first update-enabled build is version 1.1.0. Copies already installed before this update feature was added must be updated by running the new setup once; later versions can be offered from inside the app.
