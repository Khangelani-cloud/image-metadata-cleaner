# Security Review

## Application map

- **Stack:** .NET 8 Windows Forms desktop application, with a Windows installer and launcher.
- **Entry points:** Desktop UI file/folder selection and drag-and-drop; installer `/upgrade` and `/uninstall` command-line modes.
- **Network entry points:** No server, webhook, or deep-link handler; the updater requests GitHub Releases metadata and downloads an installer.
- **Untrusted input:** User-selected paths and JPEG/MP4/M4V/MOV bytes, embedded metadata and names, installer arguments, and remote release metadata/assets.
- **Authentication:** No application accounts or sessions; the app operates as the current Windows user.
- **Access control:** No shared service or multi-user data-access layer; files are read from user-selected locations and written to a selected output folder.
- **Sensitive data at rest:** Cleaned copies and CSV/JSON reports are written locally; reports include source paths and parsed metadata, which may contain identifying details or location data.
- **Logging and transmission:** No persistent application logging was found; image contents are processed locally, while update checks/downloads contact GitHub.

## Findings

### Medium

#### Update installer integrity is not required

- **Confidence:** Confirmed
- **File and line:** `desktop/UpdateService.cs:87-93` (fixed)
- **What is wrong:** The updater verifies the downloaded installer only when GitHub's release response includes a recognized SHA-256 digest. If the digest is absent or not recognized, it still launches the downloaded installer after the user approves the update. A compromised or incorrectly published release asset could therefore run code as the current user without an independent integrity check.
- **Attack path:** A user accepts an update whose release asset is replaced or supplied without a usable digest; the updater then starts that executable.
- **Fix:** Fixed on this branch. The updater now requires a well-formed SHA-256 digest and refuses to launch an installer whose downloaded bytes do not match it. For stronger protection against compromised release metadata, verify a signed manifest or installer signature against a separately trusted publisher identity.

## Scope and limits

- Review covered the tracked application, installer, launcher, tests, build workflow, and dependency manifests on the `security-review` branch.
- No Critical or High findings were identified. Regression tests were added for the Medium updater-integrity finding and passed after the fix.
- The full test suite passed locally after the fix: 38 passed, 0 failed.
- The NuGet vulnerability-audit command could not complete: the configured `build/NuGet.Config` clears package sources, and the audit command failed with “Attempted to divide by zero.” No external audit feed was contacted, so dependency vulnerability status remains unverified.
- Git history checks found no credential-named files or hits for common private-key/token markers; this is not a guarantee that every credential format or historical secret was detected.
- Git-ignored local Python files (`app.py` and `metadata-cleaner-batch/`) are not part of the tracked repository and were excluded from this review.
- Release-account security, GitHub's infrastructure, Windows runtime behavior, and installer signatures were not independently verified.
