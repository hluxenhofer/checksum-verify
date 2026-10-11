# Security Policy

## Supported Versions

Only the latest release receives security fixes.

## Reporting a Vulnerability

Please **do not** open a public issue for security problems.

Report privately via GitHub: [Report a vulnerability](https://github.com/hluxenhofer/checksum-verify/security/advisories/new).

Include the affected version, reproduction steps, and impact. Expect an initial reply within 7 days. Fixes are released as soon as practical, and reporters are credited unless they prefer otherwise.

## Scope

ChecksumVerify runs locally, makes no network connections, and collects no telemetry. Relevant areas include hash computation correctness (a wrong "match"), hash-file parsing, the installer, and the registry context-menu entry.

## Verifying Releases

Each release publishes a `SHA256SUMS` file next to the installer. Verify the download before running it:

```powershell
(Get-FileHash .\ChecksumVerify-x.y.z-Setup.exe -Algorithm SHA256).Hash
```

Compare the result with the matching line in `SHA256SUMS`.

Installers also have a build provenance attestation tying them to this repository's release workflow:

```powershell
gh attestation verify .\ChecksumVerify-x.y.z-Setup.exe --repo hluxenhofer/checksum-verify
```
