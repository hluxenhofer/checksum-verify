# Changelog

All notable changes to ChecksumVerify are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.2.0] - 2026-10-12

### Fixed

- Installed app failed to start on machines without the Windows App SDK runtime: the runtime is now bundled (self-contained), and CI verifies it is in the publish output
- `.b2`/`.blake2`/`.blake2b` hash files were treated as BLAKE2b-256, so normal `b2sum` (512-bit) files always reported a mismatch; the variant is now chosen by hash length
- SHA3 requirement corrected to Windows 11 24H2+ (was shown as 22H2+)
- Hash-file problems (no entry for the file, no hash entered, hash length not matching the algorithm) are reported before hashing starts instead of after reading the whole file
- Algorithm from a hash file is now applied (including `.sha256sum`/`.sha512sum`) instead of using whatever the dropdown showed
- A sidecar file containing a line for a different file is no longer used as if it were the hash for this file
- Window is sized to its content at the display DPI instead of using fixed heights, so nothing is clipped at 125–200% scaling or with long messages, and there is no dead space; it stays inside the work area when it grows to show a result
- Window opens on the monitor under the cursor instead of always the primary one
- Clipboard auto-fill ignores hex strings that cannot be a hash (e.g. `2026`, `deadbeef`)
- Auto-detection no longer overrides an algorithm you picked when the hash length still fits it
- Cancel during hashing now stops the computation and keeps the window open instead of closing it
- Hash file picker now also lists `.sha1` and `.blake2b`

### Added

- Note listing the candidate algorithms when a pasted hash length is ambiguous (64 or 128 characters)
- BSD-style hash lines (`sha256sum --tag`: `SHA256 (file) = hash`) and backslash-escaped file names in hash files
- Tooltip on disabled SHA3 options

### Changed

- Progress is reported about once per MiB instead of once per 64 KB chunk
- Files are opened with `FileShare.ReadWrite | FileShare.Delete`, so files that another program holds open can still be verified

## [0.1.0] - 2026-10-11

### Added

- Right-click context menu integration for all files via "Show more options" → "Verify Checksum"
- Custom shield-checkmark icon in the context menu entry
- WinUI 3 popup window — centered on the primary display, always on top, with Mica backdrop on Windows 11 and Acrylic on Windows 10
- Streaming hash computation in 64 KB chunks — multi-GB files do not spike memory usage
- Supported algorithms: SHA-256, SHA-512, SHA3-256, SHA3-512, BLAKE3, BLAKE2b-256, BLAKE2b-512, MD5, SHA-1
- SHA3-256 and SHA3-512 require Windows 11 22H2+; options are disabled with a descriptive tooltip on Windows 10
- Real-time progress bar during computation, scaled to file size
- In-window result banner — green on match, red on mismatch; window stays open until explicitly closed
- Computed hash displayed inline on mismatch and automatically copied to clipboard, with a one-click copy button
- Cancel button to abort an in-progress computation
- Clipboard auto-fill on launch: detects hex strings and auto-selects the algorithm by hash length (32 chars → MD5, 40 → SHA-1, 64 → SHA-256, 128 → SHA-512)
- Algo-prefixed hash input support (e.g. `sha256:abc123`) — prefix is stripped and the matching algorithm is auto-selected
- Hash file verification against GNU coreutils format (`<hash>  <filename>` text mode and `<hash> *<filename>` binary mode)
- Sidecar hash file support: `.sha256`, `.sha512`, `.sha3-256`, `.sha3-512`, `.b2`, `.b3`, `.blake2`, `.blake3`, `.blake2b`, `.sha1`, `.md5`, `.sha256sum`, `.sha512sum`
- File picker pre-filtered to all recognized hash file extensions; algorithm auto-selected from the file extension
- Inno Setup 7 installer — registers the context menu verb under `HKLM\SOFTWARE\Classes\*\shell\` with a clean uninstaller
- `SHA256SUMS` file published with each release
- Unit test suite covering `HashEngine`, `HashFileParser`, and `ClipboardDetector` against real algorithm implementations
- GitHub Actions CI pipeline (build + test) on every push and pull request
- Build provenance attestation for release installers (verify with `gh attestation verify`)
- Installer now ships `LICENSE` and `THIRD-PARTY-NOTICES.md` with full third-party license texts
- CodeQL, dependency review and OpenSSF Scorecard workflows; `SUPPORT.md`
- Open source project files: contributing guide, code of conduct, security policy, third-party notices

[Unreleased]: https://github.com/hluxenhofer/checksum-verify/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/hluxenhofer/checksum-verify/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/hluxenhofer/checksum-verify/releases/tag/v0.1.0
