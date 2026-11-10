# 1493. global.json requires the newest .NET SDK release past the cooldown

- **Status:** Accepted (amends [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md) decision 2)
- **Refines:** [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

An SDK of the 10.0.1xx band, such as 10.0.114 (the one Ubuntu 24.04's `dotnet-sdk-10.0` package installs), cannot
compile the solution: its compiler reports 52 `CS8620` errors at calls that pass one `string` to a
`params IEnumerable<string>` member of an extension block, such as the extension `ValidateIssuer` of
`JwtValidationParameters`, in code the change did not touch. The calls are correct C#; the compiler is wrong.
`global.json` states the lowest SDK the build accepts, with `rollForward` `latestMinor`, so a floor in the 10.0.1xx band
admits that SDK.

- **The compiler bug is fixed in a later band.** It is
  [dotnet/roslyn#80024](https://github.com/dotnet/roslyn/issues/80024), reported again as #80320, #81205 and #81394, and
  fixed by [dotnet/roslyn#80433](https://github.com/dotnet/roslyn/pull/80433), merged on 2025-09-24 as commit
  `41f7a773e3bc`. A Roslyn maintainer wrote on the pull request that its 18.3 milestone "corresponds to SDK 10.0.200"
  and that a backport to 10.0.1xx would be considered only if it meets the servicing bar
  ([comment](https://github.com/dotnet/roslyn/pull/80433#issuecomment-3798934789)). The source manifest of the .NET
  virtual monolithic repository shows the fix is absent from 10.0.1xx: the Roslyn commit of tags `v10.0.100` and
  `v10.0.114` diverges from the fix commit, and that of `v10.0.200` (Roslyn 5.3.0), `v10.0.204`, `v10.0.300` and
  `v10.0.403` (Roslyn 5.9.0) contains it.
- **The reproduction is exact.** With `global.json` at 10.0.114,
  `dotnet build src/NCode.Identity.Jose.SdJwt/NCode.Identity.Jose.SdJwt.csproj` fails with two `CS8620` errors
  (`DefaultSdJwtVerifier.cs`, in `ValidateTokenType` and `ValidateAudience`). At 10.0.403 it builds with no warnings
  and no errors.
- **Only the newest bands are serviced.** The release metadata
  (<https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json>) lists SDKs 10.0.403 and 10.0.114
  for the release of 2026-10-25 and 10.0.402 and 10.0.113 for that of 2026-10-13. The last releases of the 10.0.2xx and
  10.0.3xx bands are 10.0.204 (2026-05-12) and 10.0.303 (2026-08-11), so a floor of 10.0.200 would admit SDKs of bands
  that no release has serviced since those dates.
- **The template pins the SDK and waits a cooldown.**
  [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md) pins the SDK exactly
  and keeps it current, and
  [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md) adopts a release three
  days after it ships. SDK 10.0.403 shipped on 2026-10-25, the newest release in the release metadata past that
  cooldown.
- **The pinned image already carries it.** The SDK image of both Dockerfiles, pinned by digest, has
  `DOTNET_SDK_VERSION=10.0.403`, so the image builds, which read `global.json`, keep working.
- **CI installs the newest SDK.** `actions/setup-dotnet` reads `latestMinor` as the newest 10.x SDK with `global.json`'s
  version as the minimum, and `./build/Install-Dotnet.ps1` does the same, so a floor change needs no workflow change.
- **The analyzers' Roslyn is held by EF Core Design.** `NCode.Identity.Analyzers`
  ([ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md)) builds against
  `Microsoft.CodeAnalysis.CSharp` 5.0.0. EF Core Design 10.0.14 depends on `Microsoft.CodeAnalysis.CSharp.Workspaces`
  5.0.0, which requires `Microsoft.CodeAnalysis.CSharp` exactly 5.0.0, so central transitive pinning holds the
  analyzers at 5.0.0 whatever the floor is.

## Decision

**`global.json` names the newest .NET SDK release that is at least three days old, 10.0.403, with
`rollForward: latestMinor`; the analyzers build against the Roslyn that EF Core Design pins.**

1. **The floor is 10.0.403, not 10.0.200.** 10.0.200 is the lowest band that compiles the solution, but no release has
   serviced the 10.0.2xx band since 2026-05-12. 10.0.403 is the newest release past the cooldown, and `latestMinor`
   keeps rolling to the newest 10.x SDK installed. An older SDK stops `dotnet` at once with the version it needs,
   instead of failing later with `CS8620` in code the change did not touch.
2. **A change that moves the floor never passes the SDK of the pinned SDK image.** The image builds from the same
   `global.json`, so a floor above the image's SDK breaks them.
3. **The analyzers stay on Roslyn 5.0.0.** This amends
   [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md) decision 2: the
   analyzers build against the `Microsoft.CodeAnalysis.CSharp` version EF Core Design requires exactly, 5.0.0, which is
   older than the Roslyn of every SDK `global.json` admits (10.0.403 ships 5.9.0), so every admitted SDK loads them. The
   version does not wait for the floor. Dependabot's ignore of the minor and major updates of
   `Microsoft.CodeAnalysis.CSharp` stays, because it moves with EF Core Design.
4. **Dependabot's dotnet-sdk stream raises the floor.** The `dotnet-sdk` entry of `.github/dependabot.yml` proposes each
   new SDK release three days after it ships, as project-template ADR-0008 item 3 expects, and ignores major updates
   (item 7). It runs while issue #36 pauses the other ecosystems. A proposed floor above the SDK of the pinned SDK image
   fails the host and console image builds until the docker stream's digest update lands, which is decision 2 working as
   intended.

## Options considered

- **A floor of 10.0.200.** Rejected: no release has serviced the 10.0.2xx band since 2026-05-12, so the floor would
  admit SDKs of a band that receives no fixes, and it is not the release the template's cooldown adopts.
- **Keep 10.0.100.** Rejected: an SDK of the 10.0.1xx band satisfies it and then fails with `CS8620` in code the change
  did not touch.
- **Rewrite the calls.** Rejected: the calls are correct C#, and a rewrite that satisfies one compiler's bug leaves the
  next call it miscompiles.
- **`rollForward` `disable` or `latestPatch`.** Rejected: either fixes the build to one SDK band, so every newer SDK
  and every servicing release of another band is refused, against the template's rule to keep the SDK current.
- **Raise the floor by hand.** Rejected: a floor that lags admits SDKs that miscompile the solution, and
  project-template ADR-0008 item 3 expects the SDK's own entry.

## Consequences

- An SDK of the 10.0.1xx band, such as Ubuntu 24.04's `dotnet-sdk-10.0`, needs a newer SDK, which
  `./build/Install-Dotnet.ps1` installs.
- CI keeps installing the newest SDK.
- The analyzers load in every admitted SDK, because their Roslyn is older than each one's.
- A new SDK release arrives as a Dependabot pull request that changes `global.json`.

## References

- Issue [#738](https://github.com/NCodeGroup/Identity/issues/738)
- Issue [#746](https://github.com/NCodeGroup/Identity/issues/746)
- [dotnet/roslyn#80024](https://github.com/dotnet/roslyn/issues/80024)
- [dotnet/roslyn#80433](https://github.com/dotnet/roslyn/pull/80433)
- [dotnet/roslyn#80433 maintainer comment](https://github.com/dotnet/roslyn/pull/80433#issuecomment-3798934789)
- <https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json>
- [ADR-1486](1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md)
- [project-template ADR-0008](project-template/0008-external-inputs-are-pinned-and-kept-current.md)
- [project-template ADR-0009](project-template/0009-new-dependency-versions-wait-a-cooldown.md)
