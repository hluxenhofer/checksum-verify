# Contributing to ChecksumVerify

Thanks for helping. Bug reports, feature ideas, docs fixes, and pull requests are all welcome.

## Before You Start

- Search [existing issues](https://github.com/hluxenhofer/checksum-verify/issues) first.
- For larger changes (new algorithm, UI rework, new hash-file format), open an issue to discuss before writing code.
- Questions: use [Discussions](https://github.com/hluxenhofer/checksum-verify/discussions) or see [SUPPORT.md](SUPPORT.md).
- Security problems: see [SECURITY.md](SECURITY.md), not a public issue.
- By participating you agree to the [Code of Conduct](CODE_OF_CONDUCT.md).

## Development Setup

Requirements: Windows 10 22H2+ or Windows 11, [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and optionally Inno Setup 7 for the installer.

```powershell
.\dev-setup.ps1                # guided one-time setup
dotnet build
dotnet test
dotnet run --project src/ChecksumVerify -- "C:\path\to\file.zip"
iscc installer\setup.iss       # optional: build installer
```

## Project Layout

- `src/ChecksumVerify.Core` — UI-free logic: hashing, hash-file parsing, clipboard detection. Keep new logic here so it is testable.
- `src/ChecksumVerify` — WinUI 3 popup and startup.
- `tests/ChecksumVerify.Tests` — unit tests against real algorithm implementations (no mocks, no UI). Fixtures in `tests/ChecksumVerify.Tests/Fixtures/`.
- `installer/setup.iss` — Inno Setup script.

## Pull Requests

1. Fork and branch from `main`.
2. Keep changes focused; one concern per PR.
3. Add or update tests for any change in hashing, parsing, or detection logic.
4. `dotnet build` must succeed (warnings are errors) and `dotnet test` must pass.
5. Add a line under `## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) for every user-facing change (Added / Changed / Fixed / Removed).
6. Use [Conventional Commits](https://www.conventionalcommits.org/) style messages (`feat:`, `fix:`, `docs:`, `chore:` …).
7. Fill in the pull request template.

Code style is enforced through `.editorconfig`. Match the surrounding code.

## Design Principles

- **No persistent process.** No tray icon, no startup entry. Launch, verify, exit.
- **Streaming hash.** Files are read in chunks; memory use must not scale with file size.
- **No network, no telemetry.**

## Releases

Maintainers cut releases with the **Prepare Release** GitHub Actions workflow. Contributors do not need to bump versions.

## License

Contributions are licensed under the project's [MIT License](LICENSE).
