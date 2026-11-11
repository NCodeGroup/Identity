# 1500. Every package names one capability under the NCode.Identity prefix

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group
- **Supersedes:** [ADR-1188](1188-general-time-helpers-live-in-a-dependency-free-package.md),
  [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md)
- **Refines:** [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1056](1056-abstractions-split-into-framework-free-tiered-packages.md)

## Context

Every project of the family versions in lockstep from one `version.json`, and a package ID is permanent once it is on
nuget.org. A name without the `NCode.Identity` prefix claims a scope of its own, which lockstep versioning from this
repository does not give it.

The family is for developers who tweak and extend it. Each capability therefore publishes its contracts in an
`.Abstractions` package and a default implementation beside it
([ADR-1056](1056-abstractions-split-into-framework-free-tiered-packages.md),
[ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), so a consumer implements, decorates or replaces one
capability by taking its pair. A package that mixes capabilities makes a consumer take several to replace one.

Some building blocks serve one technology for every package: JSON values, time periods and ASP.NET Core helpers. They
are public packages of their own, because shared code that is not in a public package is copied into each consumer.

## Decision

**Every package carries the `NCode.Identity` prefix and names one capability; the building blocks on one technology are
foundation packages that any package may reference.**

1. **Prefix.** `NCode.Identity.Registration`, `NCode.Identity.Registration.AspNetCore`, `NCode.Identity.Json`,
   `NCode.Identity.Time` and `NCode.Identity.AspNetCore` carry the prefix in project, folder, assembly, root namespace
   (and sub-namespaces such as `.Options`), test project, `InternalsVisibleTo`, public API files and solution.
2. **Persistence.** `NCode.Identity.Persistence.Abstractions` holds every persistence contract, in the namespaces
   `NCode.Identity.Persistence` and `NCode.Identity.Persistence.Stores`. It references `NCode.Identity.Registration`
   for `StoreManagerLibrary`.
3. **The AspNetCore pair.** `NCode.Identity.AspNetCore.Abstractions` holds the contracts `IResultExecutor<T>`,
   `IResultProvider`, `ISupportStatusCode`, `ISupportResponseHeaders`, `HttpResultException`, `UriDescriptor` and
   `IFrontChannelEndpointMetadata`, with `HttpResultExtensions` beside the exception. Its namespace is
   `NCode.Identity.AspNetCore`, the same as the implementation's, so a pair shares one namespace.
   `NCode.Identity.AspNetCore` references its abstractions.
4. **Host hardening is composed by each host.** `AddIdentityServer()` and `AddIdentityBff()` register no HSTS,
   forwarded headers or probes ([ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md));
   any host composes them from the public `NCode.Identity.AspNetCore`.
5. **`TimerPeriod` and `TimePeriod` are two types.** `TimerPeriod` is the range of periods a timer accepts;
   `TimePeriod` is the span between two instants, such as a token's lifetime. Both live in `NCode.Identity.Time`,
   namespace `NCode.Identity.Time`.
6. **`ISupportClone` is message cloning.** It lives in `NCode.Identity.OpenId.Abstractions`, file
   `Messages/ISupportClone.cs`, namespace `NCode.Identity.OpenId.Messages`, beside `IOpenIdMessage`.
7. **Capability pairs.** `NCode.Identity.Settings(.Abstractions)` (marker `SettingsLibrary`, `AddSettingsLibrary()`),
   `NCode.Identity.Claims(.Abstractions)` (`ClaimsLibrary`, `AddClaimsLibrary()`) and
   `NCode.Identity.Cryptography(.Abstractions)` (`CryptographyLibrary`, `AddCryptographyLibrary()`) are flat,
   single-purpose packages, each with one namespace across its pair. Each `Add…Library()` extends a bare
   `IServiceCollection`, mints its marker and registers with `TryAdd*`. `AddSettingsLibrary()` also calls
   `AddCollectionProviders()`. `CryptoServiceExtensions` stays in `NCode.Identity.Cryptography.Abstractions`.
8. **The OpenID composition.** `AddOpenIdCoreLibrary(claimsBuilder, cryptographyBuilder)` extends
   `IServiceBuilder<SettingsLibrary>`; `AddOpenIdAuthenticationLibrary(joseBuilder)` and `AddOpenIdManagementLibrary()`
   extend `IServiceBuilder<OpenIdCoreLibrary>`. `IIdentityServerBuilder` exposes `SettingsLibraryBuilder`,
   `ClaimsLibraryBuilder` and `CryptographyLibraryBuilder`.
9. **Foundation packages.** `NCode.Identity.Json`, `NCode.Identity.Time` and `NCode.Identity.AspNetCore(.Abstractions)`
   may be referenced by any package, refining
   [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md). A foundation package references only
   `NCode.Identity.Registration` and other foundation packages.
10. **A package takes an `NCode.*` name of its own only when a second product consumes it**, and then lives in a
    repository with its own versioning.
11. **No package is named for who uses it** (`.Common`, `.Shared`, `.Utilities`, `.Helpers`); a package is named for
    its capability.
12. **Shared code lives in a public package**, never in linked shared source.
13. **A type lives in the package whose capability it is, and a public type stays public.**

## Options considered

- **Unprefixed names.** Rejected: they claim a scope of their own that lockstep versioning does not give.
- **Linked shared source.** Rejected: every consumer compiles its own copy.
- **A `.Common` package.** Rejected: it is named for who uses it, not for a capability.
- **One type for timer periods and time periods.** Rejected: they are different concepts, a range of accepted timer
  periods and the span between two instants.
- **Claims and cryptography contracts in `NCode.Identity.OpenId.Abstractions`.** Rejected: they are not OpenID
  capabilities, and a consumer of one would take the whole OpenID contract set.
- **An umbrella `NCode.Identity` package.** Rejected: it holds two capabilities in one.

## Consequences

- A consumer replaces settings, claims or cryptography by taking one pair, and the OpenID libraries compose on the
  three builders.
- The package set is settled before the first nuget.org publish, so no package ID needs to change afterwards.
- The `src/` folders are `NCode.Identity.Settings(.Abstractions)`, `NCode.Identity.Claims(.Abstractions)`,
  `NCode.Identity.Cryptography(.Abstractions)`, `NCode.Identity.AspNetCore(.Abstractions)`, `NCode.Identity.Json`,
  `NCode.Identity.Time` and `NCode.Identity.Registration(.AspNetCore)`.
- A new foundation package needs a reason that a second package of the family consumes it; a capability that only one
  package consumes belongs in that package.

## References

- Issue [#758](https://github.com/NCodeGroup/identity/issues/758).
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1056](1056-abstractions-split-into-framework-free-tiered-packages.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1188](1188-general-time-helpers-live-in-a-dependency-free-package.md),
  [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md),
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)
