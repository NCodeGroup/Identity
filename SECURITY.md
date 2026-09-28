# Security Policy

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues.**

Report privately via GitHub's **[Security Advisories](https://github.com/NCodeGroup/Identity/security/advisories/new)**
("Report a vulnerability"). Include a description, affected package(s) and version(s), reproduction steps, and impact.
You will receive an acknowledgement, and we will coordinate a fix and disclosure timeline with you.

Because this is a security-relevant identity library, please give us a reasonable window to release a fix before any
public disclosure.

## Supported versions

Until the first stable (`1.0.0`) release the project is pre-release (`-alpha`); only the latest published version is
supported. Once GA, security fixes target the newest patch of each supported `major.minor` line; see
[GOVERNANCE.md](GOVERNANCE.md#releases-branches--tags) for the release-line model.

## Verifying packages

Packages are built deterministically and source-linked to this public repository
([ADR-0007](docs/adr/0007-provenance-sourcelink-and-symbols.md)); released packages should be signed and RFC-3161
timestamped. Verify a downloaded package with:

```shell
dotnet nuget verify path/to/NCode.Identity.<version>.nupkg
```

## Scope

The correct-by-default posture is a security feature: there is no public API to disable issuer / audience / signature /
lifetime / algorithm validation, or to weaken a security control below its safe baseline. A report that such a control
can be bypassed through the public surface is in scope.
