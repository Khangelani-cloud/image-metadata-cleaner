# Image Metadata Cleaner for Windows

## Install and open

1. Double-click **Image Metadata Cleaner Setup.exe**.
2. Click **Install**. The desktop shortcut is selected by default.
3. Open **Image Metadata Cleaner** from the desktop or Start Menu.

The installer works for the current Windows account and does not ask for administrator access. The app and its .NET runtime are included, so users do not need to install Python or .NET separately. It runs locally and does not send images to a website.

If Windows warns that the app is from an unknown publisher, that is because this build is not code-signed. Only install a copy you trust.

## Use the app

1. Add JPEG images or choose a folder. Folder selection includes subfolders.
2. Choose whether to remove C2PA alone or C2PA plus all XMP.
3. Choose where to save the batch and select **Inspect and create cleaned copies**.

The app makes a new dated folder for each batch. Original images are never modified. It saves a CSV report, a JSON report, and a short explanation beside the cleaned copies. Non-JPEG files and damaged JPEGs are reported as skipped.

**Remove C2PA only** keeps XMP, EXIF, IPTC, ICC profiles, JFIF and other JPEG metadata. Provenance details that may also appear in XMP remain.

**Remove C2PA and all XMP** removes the whole XMP packet, including possible ordinary descriptions, dates, copyright details, software information and edit history. EXIF, IPTC, ICC and JFIF remain. Neither option recompresses the JPEG image scan.

C2PA values in the report are readable embedded claims. The app does not validate signatures, certificates or trust. ExifTool and Adobe's C2PA inspector are not bundled.

## Uninstall

Use **Settings → Apps → Installed apps** (or **Apps & features** on older Windows versions), choose **Image Metadata Cleaner**, and select **Uninstall**. Your source images and saved reports are not removed.

## Build the installer from source

The source project is in `desktop/`, the setup program is in `installer/`, and `build-package.ps1` builds the x64 installer. Building requires the .NET 8 Windows Desktop SDK and the Windows .NET Framework compiler. The build bundles the .NET runtime so end users do not need to download it separately.

## Updates

The app quietly checks GitHub Releases when it opens and also has a **Check for updates** link. It asks before downloading or installing an update. Downloads use HTTPS, and the app checks GitHub's published SHA-256 digest when one is available. Installation replaces the app files only; source images and saved reports are kept.

To publish an update, push a version tag such as `v1.2.0`. The Windows build workflow creates the installer and attaches it to a GitHub release. The repository must be public for every installed copy to check releases without asking users to sign in. The workflow embeds the repository name in each build.

The first update-enabled build is version 1.1.0. Copies already installed before this update feature was added must be updated by running the new setup once; later versions can be offered from inside the app.
