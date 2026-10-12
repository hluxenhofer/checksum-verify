# ChecksumVerify — Design Spec

**Date:** 2026-09-04  
**Status:** Approved

---

## 1. Problem & Goal

Users downloading files from the internet need a frictionless way to verify checksums. Existing options require opening a terminal, copying commands, and manually comparing hex strings. ChecksumVerify reduces this to a single right-click with a clear pass/fail result.

**Success criteria:**
- Right-click any file → verify a hash in under 5 seconds of user interaction
- Support typed/pasted hash, clipboard auto-fill, and hash file (sidecar or GNU format)
- Zero idle resource usage (no background process, no startup entry)
- Works on Windows 10 22H2+ and Windows 11

---

## 2. Architecture

### Core principle: zero idle footprint

No tray process, no background service, no startup registration. The context menu registry verb launches `ChecksumVerify.exe` with the target file path as its sole argument. The process lives only for the duration of one verification session.

```
User right-clicks file
        │
        ▼
Registry shell verb (HKLM)
  runs ChecksumVerify.exe "<filepath>"
        │
        ▼
 WinUI 3 input popup (Mica/Acrylic backdrop)
  (algorithm selector + hash input OR hash file picker)
  (clipboard auto-fill on open if hash string detected)
        │
        ▼
  Streaming hash computation
  (64 KB chunks — no full file in RAM)
  (progress bar fills as bytes are read)
        │
        ▼
  In-window result banner
  ✓ Match — green banner, success message
  ✗ Mismatch — red banner, computed hash displayed
              + "Copy hash" button + clipboard copy
        │
        ▼
  User clicks Close → process exits
```

### Process lifetime

| Phase (estimates, not yet measured) | Duration | Memory |
|---|---|---|
| Idle | 0 ms | 0 MB |
| Popup shown (before Verify) | ~100 ms startup | ~30 MB |
| Computing | seconds (file-size dependent) | ~30 MB + 64 KB buffer |
| Result shown (waiting for user) | until Close clicked | ~30 MB |
| After Close | ~0 ms | 0 MB |

---

## 3. Tech Stack

| Component | Choice | Version |
|---|---|---|
| Runtime | .NET 10 LTS | stable (Nov 2025) |
| UI | Windows App SDK / WinUI 3 (unpackaged) | 2.5.1 |
| Windows App SDK runtime | Self-contained — bundled next to the exe by `WindowsAppSDKSelfContained` when publishing with a runtime identifier | — |
| Window backdrop | Mica (Win 11) / Acrylic (Win 10) | via `MicaBackdrop` / `DesktopAcrylicBackdrop` |
| SHA-256, SHA-512 | `System.Security.Cryptography` | BCL |
| SHA3-256, SHA3-512 | `System.Security.Cryptography.SHA3_256/512` | BCL (.NET 10) |
| BLAKE3 | `Blake3` (xoofx/Blake3.NET) | 3.0.2 |
| BLAKE2b | `SauceControl.Blake2Fast` | 2.0.0 |
| Installer | Inno Setup | 7.1.0 |
| Package versioning | NuGet Central Package Management | `Directory.Packages.props` |
| Code signing | SignPath Foundation (free OSS) | Planned — not yet implemented |

SHA3 requires **Windows 11 24H2+** (OS build 25324 or later; Windows 11 22H2/23H2 and Windows 10 lack CNG SHA-3 support, per the [.NET cross-platform cryptography table](https://learn.microsoft.com/dotnet/standard/security/cross-platform-cryptography#hash-and-message-authentication-algorithms)). The app checks `SHA3_256.IsSupported` / `SHA3_512.IsSupported` at runtime and disables the SHA3 options when unavailable.

The Windows App SDK runtime is bundled with the app (self-contained, ~230 MB published; the installer compresses it) so a clean machine needs no separate runtime installer. Plain `dotnet build` / `dotnet run` stay framework-dependent: `WindowsAppSDKSelfContained` is only enabled when a runtime identifier is given.

---

## 4. Hash Algorithms Exposed

| UI label | Algorithm | Auto-detect on hex length |
|---|---|---|
| SHA-256 | SHA-256 | 64 chars |
| SHA-512 | SHA-512 | 128 chars |
| SHA3-256 | SHA3-256 | 64 chars (disambiguated by dropdown) |
| SHA3-512 | SHA3-512 | 128 chars |
| BLAKE3 | BLAKE3 | 64 chars |
| BLAKE2b-256 | BLAKE2b-256 | 64 chars |
| BLAKE2b-512 | BLAKE2b-512 | 128 chars |
| MD5 | MD5 | 32 chars (legacy) |
| SHA-1 | SHA-1 | 40 chars (legacy) |

Auto-detection sets the algorithm dropdown when the user pastes or types a hash. An algorithm that already fits the hash length is kept — including one the user picked by hand. When the length is ambiguous (64 chars could be SHA-256, SHA3-256, BLAKE3, or BLAKE2b-256; 128 chars SHA-512, SHA3-512 or BLAKE2b-512), the dropdown selects the most common choice (SHA-256 / SHA-512) and a note under the hash field lists the candidates. An explicit `algo:` prefix (e.g. `blake3:…`) always selects that algorithm.

---

## 5. UX Flow

### Input popup

```
┌─ Verify Checksum ──────────────────────────────────┐
│ FILE                                               │
│ installer.zip                                      │
│                                                    │
│ Algorithm                                          │
│ [SHA-256                              ▾]           │
│                                                    │
│ ◉ Enter hash   ○ Hash file                         │
│                                                    │
│ Expected hash                                      │
│ [________________________________] [Paste]         │
│                                                    │
│ ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░ (progress, hidden)  │
│                                                    │
│                    [Cancel]  [Verify]              │
└────────────────────────────────────────────────────┘
```

- Always-on-top, 520 DIP wide client area (scaled by the monitor DPI), centered on the monitor under the cursor, Mica/Acrylic backdrop. The height is not fixed: the window is measured and resized to its content whenever it changes (note shown, result shown, progress bar shown), and kept inside the work area, so nothing is clipped at any DPI or text scale
- If the clipboard contains a plausible hash on open (hex string of 32/40/64/128 characters, or an `algo:`-prefixed one) → pre-fill hash field, select "Enter hash" mode, auto-select algorithm
- SHA3 options are disabled on Windows versions without SHA-3 (Windows 10, Windows 11 before 24H2); the label names the requirement and a tooltip explains it
- Inputs are validated **before** the file is read (empty or non-hex hash, hash length not matching the algorithm, no hash file selected, no entry for the file in the hash file), so errors appear immediately even for multi-GB files

### During computation

A progress bar fills as bytes are read (file-size maximum; updated about once per MiB). The Verify button is disabled; Cancel remains active and stops the computation without closing the window — the button then reads Close, and the user can adjust the inputs and verify again.

### Result — Match

```
┌─ Verify Checksum ──────────────────────────────────┐
│ FILE                                               │
│ installer.zip                                      │
│ ...                                                │
│ ┌──────────────────────────────────────────────┐  │
│ │ ✓ Checksum verified — the file matches the   │  │
│ │   expected hash.                             │  │
│ └──────────────────────────────────────────────┘  │
│                              [Close]  [Verify]    │
└────────────────────────────────────────────────────┘
```

### Result — Mismatch

```
┌─ Verify Checksum ──────────────────────────────────┐
│ FILE                                               │
│ installer.zip                                      │
│ ...                                                │
│ ┌──────────────────────────────────────────────┐  │
│ │ ✗ Checksum mismatch — the file does not      │  │
│ │   match the expected hash.                   │  │
│ │                                              │  │
│ │ Computed hash — copied to clipboard          │  │
│ │ [80ce3ecd...a37c15ff              ]          │  │
│ └──────────────────────────────────────────────┘  │
│                [Copy hash]  [Close]  [Verify]     │
└────────────────────────────────────────────────────┘
```

On mismatch, the computed hash is displayed, automatically copied to clipboard, and a "Copy hash" button lets the user re-copy it. The user can then correct the expected hash and re-verify without reopening the window.

---

## 6. Hash File Formats

### GNU coreutils (sha256sum output)
```
2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824  hello.bin
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855 *empty.bin
```
Parser searches for the target filename in any line. Binary mode (`*`) treated identically to text mode. Also supported:

- **BSD style** (`sha256sum --tag`): `SHA256 (hello.bin) = <hash>` — the tag selects the algorithm
- **Escaped names**: a leading `\` marks a name containing `\` (written `\\`) or a newline (written `\n`)
- Blank lines and `#` comments are ignored

The algorithm is taken from, in order: the BSD tag, the file extension, the hash length. The UI only overrides the user's dropdown choice when the algorithm is certain (tag or extension), or when the choice cannot match the hash length.

### Sidecar files
| Extension | Algorithm |
|---|---|
| `.sha256` | SHA-256 |
| `.sha512` | SHA-512 |
| `.sha3-256` | SHA3-256 |
| `.sha3-512` | SHA3-512 |
| `.b2`, `.blake2`, `.blake2b` | BLAKE2b — 512-bit by default (`b2sum`); 256-bit when the hash has 64 hex chars |
| `.b3`, `.blake3` | BLAKE3 |
| `.md5` | MD5 |
| `.sha1` | SHA-1 |

Sidecar files contain either a bare hex string or a GNU/BSD line. Both are handled; a line naming a *different* file is reported as "no entry for this file" rather than used. `.sha256sum` and `.sha512sum` files are parsed as GNU coreutils lists.

When the user browses to a sidecar file, the algorithm dropdown auto-selects based on the extension.

---

## 7. Context Menu Registration

Set by Inno Setup; requires elevation (installer runs as admin once):

```
HKLM\SOFTWARE\Classes\*\shell\ChecksumVerify
  (Default)    = "Verify Checksum"
  Icon         = "C:\Program Files\ChecksumVerify\ChecksumVerify.exe,0"

HKLM\SOFTWARE\Classes\*\shell\ChecksumVerify\command
  (Default)    = "\"C:\Program Files\ChecksumVerify\ChecksumVerify.exe\" \"%1\""
```

Appears under "Show more options" on Windows 11 (legacy expanded menu). Top-level Windows 11 placement requires SparsePackage identity — planned for v2.

---

## 8. Project Structure

```
checksum-verify/
├── .github/
│   ├── workflows/
│   │   ├── build.yml              # CI: build + test + publish smoke test on push/PR
│   │   ├── release-prep.yml       # Manual: promote changelog, bump version, tag
│   │   ├── release.yml            # Release: publish installer + SHA256SUMS
│   │   ├── codeql.yml             # Code scanning
│   │   ├── dependency-review.yml  # Dependency review on PRs
│   │   └── scorecard.yml          # OpenSSF Scorecard
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug_report.yml
│   │   ├── feature_request.yml
│   │   └── config.yml
│   ├── CODEOWNERS
│   ├── dependabot.yml
│   └── PULL_REQUEST_TEMPLATE.md
├── src/
│   ├── ChecksumVerify.Core/
│   │   ├── ChecksumVerify.Core.csproj
│   │   ├── HashAlgorithmId.cs      # Enum of supported algorithms
│   │   ├── HashAlgorithmNames.cs   # Display names, SHA3 OS requirement text
│   │   ├── HashEngine.cs           # Algorithm dispatch + streaming compute
│   │   ├── HashFileParser.cs       # GNU, BSD and sidecar format parsing
│   │   ├── ClipboardDetector.cs    # Hex string detection + length→algorithm
│   │   ├── AlgorithmSelector.cs    # Dropdown auto-selection rules + ambiguity note
│   │   └── VerifyInputResolver.cs  # Pre-hash input validation
│   └── ChecksumVerify/
│       ├── ChecksumVerify.csproj
│       ├── Program.cs              # Custom Main (unpackaged WinUI 3 startup)
│       ├── App.xaml / App.xaml.cs
│       ├── MainWindow.xaml / MainWindow.xaml.cs
│       ├── NativeMethods.cs        # GetDpiForWindow / GetCursorPos
│       ├── app.manifest            # Per-monitor DPI v2
│       ├── Properties/
│       │   └── launchSettings.json # `dotnet run` / debugger arguments
│       └── Assets/
│           └── checksumverify.ico
├── tests/
│   └── ChecksumVerify.Tests/
│       ├── ChecksumVerify.Tests.csproj
│       ├── HashEngineTests.cs
│       ├── HashFileParserTests.cs
│       ├── ClipboardDetectorTests.cs
│       ├── AlgorithmSelectorTests.cs
│       ├── VerifyInputResolverTests.cs
│       └── Fixtures/               # Small files with known hashes
├── installer/
│   └── setup.iss                   # Inno Setup 7 script (context menu + uninstaller)
├── docs/
│   ├── DESIGN.md
│   └── assets/                     # README demo GIFs
├── Directory.Packages.props        # Central Package Management — all NuGet versions here
├── Directory.Build.props           # Shared MSBuild properties (target framework, version)
├── global.json                     # Pins .NET 10 SDK, selects the Microsoft.Testing.Platform runner
├── ChecksumVerify.slnx
├── .editorconfig
├── .gitattributes
├── .gitignore
├── README.md
├── CHANGELOG.md
├── CONTRIBUTING.md
├── CODE_OF_CONDUCT.md
├── SECURITY.md
├── SUPPORT.md
├── THIRD-PARTY-NOTICES.md          # Licenses of bundled dependencies (shipped with the installer)
├── LICENSE                         # MIT
└── dev-setup.ps1                   # One-time developer machine setup
```

---

## 9. Local Developer Build

No Visual Studio required. Prerequisites: .NET 10 SDK.

```powershell
# One-time setup
.\dev-setup.ps1

# Build
dotnet build

# Run (launches popup with a test file)
dotnet run --project src/ChecksumVerify -- "C:\path\to\testfile.zip"

# Run tests
dotnet test

# Build installer (requires Inno Setup 7 installed)
dotnet publish src/ChecksumVerify/ChecksumVerify.csproj -c Release -r win-x64 --self-contained -o publish/win-x64/
iscc installer\setup.iss
# Output: installer\Output\ChecksumVerify-x.y.z-Setup.exe
```

`dev-setup.ps1` automates the one-time steps via winget and guided prompts.

---

## 10. Testing Strategy

- **Unit tests** (`ChecksumVerify.Tests`): `HashEngine`, `HashFileParser`, `ClipboardDetector`, `AlgorithmSelector`, `VerifyInputResolver` — no UI; they read small fixture or temp files but need no mocks. The window's decisions (auto-selection, validation, error text) are pure Core functions, so only control wiring, DPI sizing and window placement need manual testing
- **Known-hash fixtures**: small test files with pre-computed hashes committed to `tests/ChecksumVerify.Tests/Fixtures/`. SHA3 tests skip themselves where the OS lacks SHA-3 support
- **Manual UI tests**: developer runs against real downloaded files before each release
- **CI** (`build.yml`, on push to `main` and on PRs): Release build, `dotnet test`, and a self-contained publish smoke test that checks `resources.pri` and `Microsoft.WinUI.dll` are in the output

No mocking of the hash libraries — tests use real algorithm implementations against known inputs.

---

## 11. Release & Distribution

1. Maintainer runs the **Prepare Release** workflow (`release-prep.yml`), which promotes `[Unreleased]` in the changelog, bumps the version, and pushes tag `vX.Y.Z`
2. Tag push triggers `release.yml`
3. `dotnet publish -c Release -r win-x64 --self-contained`, then verify the output contains `resources.pri` and `Microsoft.WinUI.dll` (the bundled runtime)
4. `iscc installer\setup.iss` → produces `ChecksumVerify-X.Y.Z-Setup.exe`
5. `SHA256SUMS` is generated for the installer
6. Installer and `SHA256SUMS` are uploaded to GitHub Releases, with notes taken from the changelog

### Planned

These steps are not implemented yet:

- **Code signing** — SignPath Foundation signs the published binaries and installer via its GitHub Actions integration (between publish and installer build). Until then the installer is unsigned and SmartScreen may warn.
- **winget** — `winget-create` generates a manifest from the release installer URL and submits a PR to `microsoft/winget-pkgs`.

---

## 12. Future — v2 SparsePackage

Adding SparsePackage identity to an existing unpackaged EXE install gives access to the top-level Windows 11 context menu without converting to full MSIX packaging. Requires:
- A signing certificate (Azure Trusted Signing, ~$10/month)
- A minimal `AppxManifest.xml` + `identity/` folder in the repo
- Inno Setup script updated to call `Add-AppxPackage -ExternalLocation`

Deferred to v2 — no architecture changes required to v1 code.
