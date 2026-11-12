# 1493. global.json requires the newest .NET SDK release past the cooldown

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)

## Context

An SDK of the 10.0.1xx band, such as the `dotnet-sdk-10.0` package of Ubuntu 24.04, miscompiles the solution. Its
compiler reports `CS8620` at correct calls that pass one `string` to a `params IEnumerable<string>` member of an
extension block, such as `ValidateIssuer` of `JwtValidationParameters`, in code a change did not touch. The calls are
correct C#; the compiler is wrong. `global.json` states the lowest SDK the build accepts, with `rollForward`
`latestMinor`, so a floor in the 10.0.1xx band admits that SDK.

- **The compiler bug is fixed in a later band.** It is
  [dotnet/roslyn#80024](https://github.com/dotnet/roslyn/issues/80024), fixed by
  [dotnet/roslyn#80433](https://github.com/dotnet/roslyn/pull/80433). The fix reaches SDK 10.0.200 and later and does
  not reach the 10.0.1xx band.
- **Only the newest bands are serviced.** A floor at the lowest band that compiles would admit SDKs of bands that no
  servicing release has fixed since.
- **The template pins the SDK and waits a cooldown.**
  [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md) pins the SDK and
  keeps it current, and
  [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md) adopts a release three
  days after it ships.
- **The image builds read the same file.** Both Dockerfiles (`hosts/NCode.Identity.OpenId.Host` and
  `hosts/NCode.Identity.ManagementConsole.Host`) build from a digest-pinned SDK image that reads `global.json`.
- **CI installs the newest SDK.** `actions/setup-dotnet` with `global-json-file` reads `latestMinor` as the newest 10.x
  SDK with the file's version as the minimum, and `./build/Install-Dotnet.ps1` does the same elsewhere.

## Decision

**`global.json` names the newest .NET SDK release that is at least three days old, with `rollForward: latestMinor` and
`allowPrerelease: false`.**

1. **The floor is the newest release past the cooldown.** The cooldown is project-template ADR-0009's three days. The
   floor is the newest serviced release, not the lowest band that compiles. `latestMinor` rolls the build to the newest
   10.x SDK installed, and an older SDK stops `dotnet` at once with the version it needs, instead of failing later with
   `CS8620`.
2. **A change that moves the floor never passes the SDK of the pinned SDK image.** The image builds of both
   Dockerfiles read the same `global.json`, so a floor above the image's SDK breaks them.
3. **CI and local installs follow the floor.** CI installs the SDK through `.github/actions/install-dotnet`
   (`actions/setup-dotnet` with `global-json-file`), and `./build/Install-Dotnet.ps1` does the same elsewhere. Each
   installs the newest 10.x SDK at or above the floor, so a floor change needs no workflow change.
4. **Dependabot's dotnet-sdk stream raises the floor.** The `dotnet-sdk` entry of `.github/dependabot.yml` proposes each
   new SDK release with a three-day cooldown and ignores major updates, because a major is a planned migration
   (project-template ADR-0008). It keeps its pull-request limit while issue
   [#36](https://github.com/NCodeGroup/identity/issues/36) pauses the other ecosystems' version updates.
5. **A floor above the image's SDK fails the image builds.** A proposed floor above the pinned image's SDK fails the
   host and console image builds until the docker stream's digest update lands, which is decision 2 working as
   intended.

## Options considered

- **A floor at the lowest compiling band (10.0.200).** Rejected: it admits SDKs of bands that receive no servicing
  releases, and it is not the release the template's cooldown adopts.
- **Keep a 10.0.1xx floor.** Rejected: an SDK of that band satisfies it and then miscompiles the solution.
- **Rewrite the correct calls.** Rejected: the calls are correct C#, and a rewrite that satisfies one compiler's bug
  leaves the next call it miscompiles.
- **`rollForward` `disable` or `latestPatch`.** Rejected: either fixes the build to one SDK band, so every newer SDK
  and every servicing release of another band is refused, against the template's rule to keep the SDK current.
- **Raise the floor by hand.** Rejected: a floor that lags admits SDKs that miscompile the solution, and
  project-template ADR-0008 expects the SDK to have its own Dependabot entry.
- **The newest release past the cooldown, rolled forward by `latestMinor`, kept current by Dependabot.** Chosen.

## Consequences

- An SDK of the 10.0.1xx band, such as Ubuntu 24.04's `dotnet-sdk-10.0`, needs a newer SDK, which
  `./build/Install-Dotnet.ps1` installs.
- CI keeps installing the newest SDK.
- A new SDK release arrives as a Dependabot pull request that changes `global.json`.
- An older SDK fails at once with the version it needs, not later with a misleading compiler error.

## References

- [dotnet/roslyn#80024](https://github.com/dotnet/roslyn/issues/80024)
- [dotnet/roslyn#80433](https://github.com/dotnet/roslyn/pull/80433)
- Issue [#36](https://github.com/NCodeGroup/identity/issues/36)
- `global.json`, `.github/dependabot.yml`, `.github/actions/install-dotnet`, `build/Install-Dotnet.ps1`
- [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)
- [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md)
