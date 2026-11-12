# 1500. Every package names one capability under the NCode.Identity prefix, as an abstractions and implementation pair

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group

## Context

Every project of the family versions in lockstep from one `version.json`, and a package ID is permanent once it is on
nuget.org. A name without the `NCode.Identity` prefix claims a scope of its own, which lockstep versioning from this
repository does not give it.

The family is for developers who tweak and extend it
([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). A consumer implements, decorates or replaces one
capability by taking one package pair, so a package that mixes capabilities forces a consumer to take several to replace
one. Contracts that carry a heavy framework dependency force every consumer of the contract to take that framework too.

Some building blocks serve one technology for every package: JSON values, time periods and ASP.NET Core helpers. Two
packages that cannot reference each other need a shared home, and shared code that is not in a public package is copied
into each consumer. Abstractions hold no logic, so a helper with logic does not belong in an `.Abstractions` package.

## Decision

**Every package carries the `NCode.Identity` prefix and names one capability; each capability ships an `.Abstractions`
package of contracts and an implementation beside it; the building blocks on one technology are foundation packages that
any package may reference.**

1. **Prefix.** Every package carries `NCode.Identity` in its project, folder, assembly, root namespace (and
   sub-namespaces such as `.Options`), test project, `InternalsVisibleTo`, public API files and solution entry. A package
   takes an `NCode.*` name of its own only when a second product consumes it, and then lives in a repository with its
   own versioning.
2. **Pairs.** A capability's `.Abstractions` package holds contracts and data, with no logic. The implementation shares
   its namespace, so a pair is one namespace across two assemblies.
3. **Implementations depend on abstractions.** An implementation references `*.Abstractions` and foundation packages and
   collaborates with other capabilities through abstractions resolved from DI. It registers with `TryAdd*` from its
   `DefaultRegistration`, and its default type is named per
   [project-template ADR-0105](project-template/0105-the-canonical-implementation-is-named-defaultfoo.md). The
   composition roots (`AddIdentityServer()` in `NCode.Identity.Server`, `NCode.Identity.Bff.Server` and
   `NCode.Identity.ManagementConsole`) reference and wire the implementations. The DI shapes of a seam are
   [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)'s.
4. **Layered references the code holds.** Beside that rule, the Entity Framework slices reference the EF framework
   ([ADR-1052](1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md)),
   `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts` references `NCode.Identity.OpenId.Accounts`,
   `NCode.Identity.OpenId.Authentication` references `NCode.Identity.OpenId.Core`, and `NCode.Identity.OpenId.Core`
   references `NCode.Identity.Jwt`.
5. **Foundation packages.** `NCode.Identity.Json`, `NCode.Identity.Time` and `NCode.Identity.AspNetCore(.Abstractions)`
   may be referenced by any package. A foundation package references only `NCode.Identity.Registration` and other
   foundation packages, and a new one needs a second package of the family that consumes it; a capability that only one
   package consumes belongs in that package.
6. **JSON.** `NCode.Identity.Json.JsonElements` is the one JSON helper home: `TryGetPropertyValue<T>`,
   `IsNullOrUndefined`, `OrEmptyObject` and `Parse`.
7. **Time.** `NCode.Identity.Time` holds `TimerPeriod`, the range of periods a timer accepts, and `TimePeriod`, the span
   between two instants such as a token's lifetime, as two types. It also holds the whole-second helpers
   `WithPrecisionInSeconds()` on `DateTime` and `DateTimeOffset`, and `GetUtcNowWithPrecisionInSeconds()` and
   `GetTimestampWithPrecisionInSeconds()` on `TimeProvider`, so a stored time equals the `NumericDate` it round-trips
   through a JWT.
8. **ASP.NET Core.** `NCode.Identity.AspNetCore.Abstractions` holds `IResultExecutor<T>`, `IResultProvider`,
   `ISupportStatusCode`, `ISupportResponseHeaders`, `HttpResultException`, `HttpResultExtensions`, `UriDescriptor` and
   `IFrontChannelEndpointMetadata`, in namespace `NCode.Identity.AspNetCore`. `NCode.Identity.AspNetCore` holds HTTP
   helpers that name no identity concept, such as the no-store helpers (`NoStoreEndpointFilter`, `WithNoStore()`,
   `SetNoStore()`) that the authentication and management packages share. `AddIdentityServer()` and `AddIdentityBff()`
   register no host hardening: a host composes its own from `NCode.Identity.AspNetCore`
   ([ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)).
9. **Framework-free low tier.** Low-tier contract packages (settings, claims, cryptography, JOSE, secrets, persistence)
   carry no ASP.NET Core. The `Microsoft.AspNetCore.App` `FrameworkReference` sits only on abstractions whose contracts
   are HTTP types.
10. **Persistence.** `NCode.Identity.Persistence.Abstractions` holds every persistence contract, in the namespaces
    `NCode.Identity.Persistence` and `NCode.Identity.Persistence.Stores`, and `StoreManagerLibrary`. `ISupportClone`
    lives in `NCode.Identity.OpenId.Abstractions`, namespace `NCode.Identity.OpenId.Messages`, beside `IOpenIdMessage`.
11. **Settings, claims and cryptography.** `NCode.Identity.Settings`, `NCode.Identity.Claims` and
    `NCode.Identity.Cryptography`, each with its `.Abstractions` package, are flat, single-purpose pairs with the markers
    `SettingsLibrary`, `ClaimsLibrary` and `CryptographyLibrary`. Each `Add…Library()` extends a bare
    `IServiceCollection`, mints its marker and registers with `TryAdd*`; `AddSettingsLibrary()` also calls
    `AddCollectionProviders()`. `CryptoServiceExtensions` lives in `NCode.Identity.Cryptography.Abstractions`.
12. **The OpenID composition.** `AddOpenIdCoreLibrary(claimsBuilder, cryptographyBuilder)` extends
    `IServiceBuilder<SettingsLibrary>`; `AddOpenIdAuthenticationLibrary(joseBuilder)` and
    `AddOpenIdManagementLibrary()` extend `IServiceBuilder<OpenIdCoreLibrary>`. `IIdentityServerBuilder` exposes
    `SettingsLibraryBuilder`, `ClaimsLibraryBuilder` and `CryptographyLibraryBuilder`.
13. **Names and placement.** No package is named for who uses it (`.Common`, `.Shared`, `.Utilities`, `.Helpers`); it is
    named for its capability. Shared code lives in a public package, never in linked source. A type lives in the package
    whose capability it is, and a public type stays public. Where a package's repository and artifacts live is
    [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)'s.

## Options considered

- **Unprefixed names.** Rejected: they claim a scope of their own that lockstep versioning does not give.
- **Linked shared source.** Rejected: every consumer compiles its own copy.
- **A `.Common` package.** Rejected: it is named for who uses it, not for a capability.
- **One type for timer periods and time periods.** Rejected: they are different concepts, a range of accepted timer
  periods and the span between two instants.
- **Claims and cryptography contracts in `NCode.Identity.OpenId.Abstractions`.** Rejected: they are not OpenID
  capabilities, and a consumer of one would take the whole OpenID contract set.
- **An umbrella `NCode.Identity` package.** Rejected: it holds several capabilities in one, so a consumer cannot take
  one.
- **Implementation-to-implementation collaboration.** Rejected: it brings transitive dependencies, a meaningless package
  boundary and the loss of independent replaceability.
- **Contract and implementation in one package.** Rejected: any consumer of the contract must also take the
  implementation and its dependencies.
- **Inlining the whole-second truncation at each call site.** Rejected: the copies drift, and a stored time then differs
  from the `NumericDate` it round-trips through.
- **Abstractions and implementation pairs under the `NCode.Identity` prefix, with foundation packages.** Chosen: each
  capability is replaced by taking one pair, and shared code has a public home.

## Consequences

- A consumer replaces settings, claims or cryptography by taking one pair, and the OpenID libraries compose on the three
  builders.
- The package set is settled before the first nuget.org publish, so no package ID needs to change afterwards.
- A host that needs HSTS, forwarded headers or probes composes them from the public `NCode.Identity.AspNetCore`.
- A smell that the rule was chosen wrong: a foundation package only one package consumes, a package named for its user,
  or an implementation `.csproj` that references another implementation without a layered reason in decision 4.

## References

- Issue [#758](https://github.com/NCodeGroup/identity/issues/758).
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1052](1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md),
  [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)
- [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md),
  [project-template ADR-0105](project-template/0105-the-canonical-implementation-is-named-defaultfoo.md)
- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2, §4.
