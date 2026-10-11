# 69. DI registration nests per package and folder through compile-time marker builders

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The family ships many independently-published packages, and each registers its own services. Several
are **opt-in bolt-on capabilities** that only make sense layered on another package's services — the
resource-owner password grant over the OpenId Authentication library, local-account management over
the Management slice, the interactive login/consent surface over the OpenID server
([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
[ADR-0061](0061-local-account-management-is-an-opt-in-capability.md),
[ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md)). Because an implementation
package depends only on abstractions ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)),
no package can see another's concrete registration to know whether its prerequisites are present. A
bolt-on added to a host that never registered the underlying library is a silent misconfiguration
that surfaces far later as a confusing missing-handler, null, or "unsupported" response.

`NCode.Registration` exists to make these prerequisites explicit. It is a thin layer over
`IServiceCollection`: a **marker** type per library (a `readonly struct` implementing
`IRegistrationMarker<TMarker>`, carrying a `DisplayName` and the `ConfigureMethod` name) and
`IServiceBuilder<TMarker>` — a wrapper that holds the collection plus a compile-time marker
discriminator. Constructing a `ServiceBuilder<TMarker>` registers the marker as a singleton
(`AddRegistrationMarker`), so a library's presence is both a **compile-time type** and a
**runtime-verifiable fact** (`VerifyIsRegistered<TMarker>` throws, naming the missing library's
`DisplayName` + `ConfigureMethod`). The builder's "hierarchy" (`NewBuilder<T>()`) is a
registration-time organizational device only — at runtime there is one flat `IServiceCollection`
([ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md)).

This mechanism is applied consistently to the always-on **library layers** (`AddJoseLibrary`,
`AddOpenIdCoreLibrary`, `AddOpenIdAuthenticationLibrary`, `AddOpenIdManagementLibrary`, …) but **not**
to the opt-in capability packages, which extend a **naked `IServiceCollection`**
(`AddLocalAccountAuthentication`, `AddLocalAccountManagement`, `AddAspNetIdentityLocalAccountStore`,
the interactive surface's `AddInteractiveAuthentication`). On a naked collection the compiler cannot
enforce that the underlying library is registered — which is exactly the guarantee the marker exists
to provide.

## Decision

**All DI registration is organized through `NCode.Registration`'s `IServiceBuilder<TMarker>`, and
every registration entry point — a library layer _and_ an opt-in bolt-on capability — is an extension
on the `IServiceBuilder<TMarker>` of the library it requires, never on a naked `IServiceCollection`.
The receiver marker is the compile-time proof that the prerequisite library is registered: a bolt-on
that augments library `X` is un-addable unless the caller holds the `IServiceBuilder<XLibrary>` that
only `X`'s `AddXLibrary()` hands back.** Wrong ordering or a missing library is a **compile error**,
not a runtime surprise.

### One marker per library, living in that library's `.Abstractions` package

Each library owns a `readonly struct` marker. The marker lives in the library's **`.Abstractions`**
package so any consumer — including a bolt-on that may reference only abstractions
([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)) — can name
`IServiceBuilder<ThatLibrary>` as a receiver without taking a forbidden implementation reference. A
marker still residing in an implementation package (`OpenIdCoreLibrary`, `OpenIdManagementLibrary`,
`JsonWebTokensLibrary`) is to move into its abstractions package; `OpenIdManagementLibrary` already
carries a `// TODO: move into abstractions`.

### Two gating mechanisms, compile-time preferred

- **Compile-time (receiver marker).** The single most-important prerequisite is the receiver type:
  `extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder) { … AddLocalAccountAuthentication() }`.
  The host cannot call it without an `IServiceBuilder<OpenIdAuthenticationLibrary>`, which only
  `AddOpenIdAuthenticationLibrary()` produces.
- **Run-time (`VerifyIsRegistered<T>`).** Additional cross-package prerequisites that cannot all be
  the single receiver type are asserted in the method body with
  `serviceCollection.VerifyIsRegistered<OtherLibrary>()`, which throws at composition (e.g. Jose
  verifies Secrets).

### Nesting per package and per folder

- **Class name.** Every registration file is `DefaultRegistration` (one per folder); there is never a
  feature-named registration class.
- **Package root.** `AddXLibrary()` extends `IServiceBuilder<Prereq>`, runs any `VerifyIsRegistered<…>`
  checks, creates the library's own builder via `builder.NewBuilder<XLibrary>()`, fans out to the
  folder-level registrations, and returns `IServiceBuilder<XLibrary>`.
- **Folder level.** A subfolder's `DefaultRegistration` extends `IServiceBuilder<XLibrary>` and exposes
  `AddYServices()` returning the same builder; a folder warranting its own sub-scope creates a finer
  marker (e.g. `Endpoints` → `NewBuilder<OpenIdAuthenticationEndpoints>()`). Method naming is
  `AddXLibrary()` at the package root (returns a new-marker builder) and `AddYServices()` at folder
  level (returns the receiver builder). As a package grows and is organized into more subfolders, it
  introduces **as many additional registration markers as it has sub-scopes to gate** — one per nested
  folder it wants to compartmentalize. Such finer sub-markers are **most often `internal`** (nothing
  outside the package names them, so they need not live in `.Abstractions`), but may be `public` when a
  consumer legitimately targets the sub-scope; the real-world `OpenIdAuthenticationEndpoints` is one such
  nested-folder marker.
- **Bolt-on capability packages.** A bolt-on extends the marker of the library it augments (ROPC →
  `OpenIdAuthenticationLibrary`; local-account management → `OpenIdManagementLibrary`; the interactive
  surface → `OpenIdAuthenticationLibrary`) and returns that same builder — it adds to an existing
  library, it does not introduce a new library layer.

### What stays on a naked `IServiceCollection` — by design, not oversight

The marker receiver expresses a **framework prerequisite**. Three kinds of registration have none and
therefore correctly take a naked `IServiceCollection`:

- **Chain roots.** The first link that establishes a marker and has no prerequisite library —
  `AddIdentityLibrary()`, `AddSecretsLibrary()` — bootstraps from the bare collection and returns the
  new builder.
- **Dependency-inverted implementations.** A swappable implementation registered _behind_ an
  abstraction seam — the local-account stores (`AddLocalAccountEntityFramework`,
  `AddAspNetIdentityLocalAccountStore`) and the Entity Framework persistence providers
  (`AddEntityFrameworkCorePersistence`) — has **no** framework prerequisite; being free of one is the
  entire point of the inversion ([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
  [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)). The _capability_ that
  consumes the seam is the marker-gated bolt-on; the _implementation_ it binds is not. Forcing a marker
  here would invent a speculative library layer for no gain.
- **Generic utility helpers.** A within-library convenience that registers one service
  (`AddAuthorizationHandler<T>`, `AddDefaultPasswordHasher`, `AddEventHandler<T>`, `AddEndpointGroupRouting`)
  is a helper, not a capability entry point, and stays a bare-collection extension.

### Composition root

`AddIdentityServer()` (the `NCode.Identity.Server` composition root) is the one place that starts the
chain and references implementation packages; it returns `IIdentityServerBuilder : IServiceBuilder<IdentityServer>`,
off which every library and bolt-on is chained. Hosts compose by **chaining on that builder**, never by
calling capability methods on the bare `IServiceCollection`.

## Options considered

- **Naked `IServiceCollection` for capabilities (the prior state of the Accounts/interactive
  bolt-ons).** Rejected: no compile-time prerequisite; a capability silently binds against an absent
  library and fails confusingly at runtime.
- **Runtime-only `VerifyIsRegistered` for everything.** Rejected: it defers an avoidable mistake to
  startup. The receiver-marker makes the common single-prerequisite case a compile error; the runtime
  check is retained only for the secondary cross-package prerequisites a single receiver type cannot
  express.
- **A DI-container feature or source generator to express ordering.** Rejected: heavier and
  container-coupled. The marker wrapper is a few lines, container-agnostic, and AOT-friendly.

## Consequences

- Adding a framework capability to a host that lacks the underlying library **fails to compile** — the
  intended guarantee.
- The opt-in **framework capabilities** extend their prerequisite marker builder:
  `AddLocalAccountAuthentication` and `AddLocalAccountInteractiveAuthentication` →
  `IServiceBuilder<OpenIdAuthenticationLibrary>`; `AddInteractiveAuthentication` →
  `IServiceBuilder<OpenIdAuthenticationLibrary>`; `AddLocalAccountManagement` →
  `IServiceBuilder<OpenIdManagementLibrary>`. The `OpenIdManagementLibrary` marker moves into
  `NCode.Identity.OpenId.Management.Abstractions` so a capability referencing only abstractions can name
  it. These are breaking surface changes, acceptable pre-release
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- Host wiring moves from a flat list of `serviceCollection.AddX()` calls to chaining off the builders
  the composition root exposes (`AddIdentityServer()` → `IIdentityServerBuilder`, whose
  `OpenIdAuthenticationLibraryBuilder` / `OpenIdManagementLibraryBuilder` properties carry the capability
  bolt-ons), which also documents the dependency order at the call site.
- One registration vocabulary (`IServiceBuilder`) spans packages and folders; the registration class
  is always `DefaultRegistration`.
- **Remaining polish (tracked as backlog `R1`):** the foundational standalone libraries `AddJoseLibrary`
  / `AddJsonWebTokensLibrary` still express their single prerequisite through a runtime
  `VerifyIsRegistered<SecretsLibrary>` / `VerifyIsRegistered<JoseLibrary>` rather than a compile-time
  receiver; upgrading them (and relocating the `OpenIdCoreLibrary` / `JsonWebTokensLibrary` markers into
  abstractions) is a consistency follow-up, not a correctness gap.

## References

- `NCode.Registration`: `IServiceBuilder` / `IServiceBuilder<TMarker>`, `IRegistrationMarker<TMarker>`,
  `ServiceCollectionExtensions` (`VerifyIsRegistered` / `NewBuilder` / `AddRegistrationMarker`).
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — implementation packages
  depend only on abstractions (why markers must live in `.Abstractions`).
- [ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) — the builder's
  hierarchy is registration-time only; the runtime routing tree is a separate mechanism.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy /
  mediator extensibility model these registrations wire up.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — pre-release posture permitting the
  breaking migration.
