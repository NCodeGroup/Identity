# 1069. DI registration proves every prerequisite at compile time through marker builders

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode Group

## Context

The family ships many independently-published packages, and each registers its own services. Several are opt-in
bolt-on capabilities that only make sense layered on another package's services: the resource-owner password grant over
the OpenId Authentication library, local-account management over the Management slice, and the interactive
login/consent surface over the OpenID server
([ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md),
[ADR-1057](1057-local-account-management-is-an-opt-in-capability-that-provisions-the-principal-eagerly-and-authorizes-per-tenant.md),
[ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md)). Because an implementation package depends only
on abstractions ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)), no package can see another's
concrete registration to know whether its prerequisites are present. A bolt-on added to a host that never registered the
underlying library is a silent misconfiguration that surfaces far later as a confusing missing handler, a null, or an
"unsupported" response.

`NCode.Identity.Registration` makes these prerequisites explicit. It is a thin layer over `IServiceCollection`: a marker
type per library and `IServiceBuilder<TMarker>`, a wrapper that holds the collection plus a compile-time marker
discriminator. A library's presence is a compile-time type carried by the builder value. A registration on a naked
`IServiceCollection` gives the compiler nothing to enforce, which is exactly the guarantee the marker exists to
provide. A registration that needs a second prerequisite needs a way to name it that no caller can forge.

## Decision

**All DI registration goes through `NCode.Identity.Registration`'s `IServiceBuilder<TMarker>`. Every library layer and
every opt-in bolt-on capability is an extension on the `IServiceBuilder<TMarker>` of the library it requires, so a
missing prerequisite or a wrong order is a compile error, never a runtime check.** The receiver marker is the
compile-time proof that the prerequisite library is registered: a bolt-on that augments library `X` cannot be added
unless the caller holds the `IServiceBuilder<XLibrary>` that only `X`'s `AddXLibrary()` hands back. The builder
hierarchy (`NewBuilder<T>()`) is a registration-time organizational device only: at runtime there is one flat
`IServiceCollection`
([ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)).

### Markers

A marker is an empty `readonly struct` implementing `IRegistrationMarker<TMarker>`. It carries no data; its type
identity is the whole signal. Constructing a `ServiceBuilder<TMarker>` registers the marker as a singleton
(`AddRegistrationMarker`, checked by `IsRegistered<TMarker>`), which also guards against accidental double
registration.

A library's marker lives in its `.Abstractions` package, so an abstractions-only consumer can name
`IServiceBuilder<ThatLibrary>` as a receiver without a forbidden implementation reference
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)). The markers are `JwtLibrary`,
`OpenIdCoreLibrary` (in `NCode.Identity.OpenId.Abstractions`), `OpenIdManagementLibrary` (in
`NCode.Identity.OpenId.Management.Abstractions`), `StoreManagerLibrary`, `EventsLibrary` and `LocalAccountLibrary`.

Markers belong to library layers and to dependency-inverted seams that are registered before their dependents:

- `AddEntityFrameworkCorePersistence<TDbContext>()` mints `StoreManagerLibrary`
  (`NCode.Identity.Persistence.Abstractions`).
- `AddLocalAccountEntityFramework` and `AddAspNetIdentityLocalAccountStore<TUser>`, and any custom store, take
  `IServiceBuilder<StoreManagerLibrary>` and mint and return `IServiceBuilder<LocalAccountLibrary>`
  (`NCode.Identity.OpenId.Accounts.Abstractions`). A capability cannot be written without a store; the cost is that a
  custom store mints and returns the marker too.
- Contract-only packages, cross-library helpers and `IInteractiveCredentialVerifier` own no marker. The verifier is
  registered after its consumer, so there is no builder to thread and a missing verifier fails at resolution. A seam
  marker applies exactly when the seam is registered before its dependent.

### Prerequisites are proven by the receiver and by required builder parameters

A registration encodes all its prerequisites in the type system and checks nothing at runtime.

- **The primary prerequisite is the receiver.** `AddOpenIdAuthenticationLibrary(IServiceBuilder<JoseLibrary>)` extends
  `IServiceBuilder<OpenIdCoreLibrary>`, and the host cannot call it without the builder that `AddOpenIdCoreLibrary`
  returned.
- **A second prerequisite is a required `IServiceBuilder<…>` parameter,** the builder that prerequisite's registration
  returned, used only as a compile-time witness. `AddOpenIdCoreLibrary(claimsBuilder, cryptographyBuilder)` extends
  `IServiceBuilder<SettingsLibrary>`. `AddLocalAccountAuthentication`, `AddLocalAccountInteractiveAuthentication` and
  `AddLocalAccountManagement` take `IServiceBuilder<LocalAccountLibrary>`; the first two extend
  `IServiceBuilder<OpenIdAuthenticationLibrary>` and the third extends `IServiceBuilder<OpenIdManagementLibrary>`. The
  method is uncallable without the argument, and it returns its receiver builder so the chain continues on the primary
  library.
- **A marker value is never the witness,** because anyone can `default` an empty struct and a marker instance proves
  nothing. Only a minted builder is unforgeable, so every prerequisite registration returns its builder, even a
  dependency-inverted store.

### Which entry points take a bare collection

The litmus is "would this registration do the right thing if it were the only thing registered?" A registration that
hooks or consumes another library's services is a bolt-on and gates on that library's builder. Only a registration that
stands alone takes a bare `IServiceCollection`:

- **Chain roots** bootstrap a marker with no prerequisite library: `AddSettingsLibrary`, `AddClaimsLibrary`,
  `AddCryptographyLibrary`, `AddSecretsLibrary`, `AddEventServices` and `AddEntityFrameworkCorePersistence`. The next
  links gate on them: `AddJoseLibrary` extends `IServiceBuilder<SecretsLibrary>` and `AddJwtLibrary` extends
  `IServiceBuilder<JoseLibrary>`.
- **One-service helpers** such as `AddAuthorizationHandler<T>`, `AddDefaultPasswordHasher` and
  `AddEndpointGroupRouting` are not capability entry points.
- **Marker-less cross-cutting infrastructure** such as `AddPropertyBag` and `AddDataProtection` is registered inline on
  the bare collection before the gated chain, because it has no presence another package must prove.

Cross-library helpers that run from more than one library's builder take the non-generic `IServiceBuilder`:
`AddMessageFactory<TMessage>`, `AddEndpointProvider<T>` and `AddEndpointGroup<T>`. That is stronger than a bare
collection, since the caller must hold some library builder, and library-agnostic.

The event bus is a small library of its own. `AddEventServices()` is a chain root on the bare collection that returns
`IServiceBuilder<EventsLibrary>`, and the opt-ins `AddEventHandler<TEvent, THandler>` and `AddBackgroundEventDelivery`
extend `IServiceBuilder<EventsLibrary>`. The two receiver shapes live in separate classes, `DefaultRegistration` and the
sibling `EventLibraryRegistration`, because one static class holds one `extension(...)` receiver (CA1708).

### Nesting and naming

- `AddXLibrary()` at a package root extends the prerequisite's builder, creates the library's builder with
  `NewBuilder<XLibrary>()`, fans out to the folder-level registrations and returns `IServiceBuilder<XLibrary>`.
- `AddYServices()` at folder level extends `IServiceBuilder<XLibrary>` and returns its receiver. A folder that
  compartmentalizes a sub-scope mints a finer marker, usually `internal`; `OpenIdAuthenticationEndpoints` is public
  because a consumer targets it.
- A bolt-on returns the builder it augments: it adds to an existing library and introduces no new library layer.
- A package's main registration class is `DefaultRegistration`; another receiver shape or opt-in set goes in a sibling
  `…Registration` class.

### Development helpers and the composition root

Development helpers that hook the composed server extend `IServiceBuilder<IdentityServer>`
(`AddDeveloperKeyPruning`, `AddDeveloperInteractiveSeed`), because they are meaningless unless `AddIdentityServer()`
composed the server first. `AddDeveloperCors`, a plain CORS policy that augments no NCode library, stays a
bare-collection extension.

`AddIdentityServer()` is the one composition root and the one place that references implementation packages. It returns
`IIdentityServerBuilder : IServiceBuilder<IdentityServer>`, whose `…LibraryBuilder` properties (shared state, secrets,
secret persistence, JOSE, JWT, settings, claims, cryptography, OpenID core, authentication, management) and
`DataProtectionBuilder` hosts chain bolt-ons off. Hosts compose by chaining on that builder, never by calling capability
methods on the bare `IServiceCollection`.

## Options considered

- **Naked `IServiceCollection` for capabilities.** Rejected: there is no compile-time prerequisite, so a capability
  silently binds against an absent library and fails confusingly at runtime.
- **Runtime prerequisite verification.** Rejected: it defers an avoidable mistake to startup and is order-sensitive.
- **A composite `IServiceBuilder<T1, T2>` receiver combined with an `And` combinator.** Rejected as over-built: it needs
  a second builder interface and implementation, an `And` combinator and a `.Primary` accessor, and the call site reads
  awkwardly (`a.And(b).AddX()`). A required parameter proves exactly the same thing at compile time with none of that
  machinery and a cleaner call site (`a.AddX(b)`).
- **A marker value or a dedicated proof type as the parameter.** Rejected: a marker is an empty `readonly struct` any
  caller can `default`, so a marker instance witnesses nothing, and a bespoke proof type would be a narrower builder
  with more surface for no gain.
- **A container feature or source generator to express ordering.** Rejected: it is heavier and container-coupled.
- **Marker-gated builders over a flat collection (chosen).** The wrapper is a few lines, container-agnostic and
  AOT-friendly, and the compiler enforces every prerequisite.

## Consequences

- Adding a capability to a host that lacks the underlying library fails to compile.
- Host wiring chains off the builders the composition root exposes, which documents the dependency order at the call
  site.
- One registration vocabulary, `IServiceBuilder`, spans packages and folders.
- A custom local-account store mints and returns `IServiceBuilder<LocalAccountLibrary>` so the account capabilities can
  require it.
- A seam registered after its dependent, such as the interactive credential verifier, is not compile-time checked and
  fails at resolution.
- A marker carries no per-marker data, so there is nothing to keep in sync.

## References

- `NCode.Identity.Registration`: `IServiceBuilder`, `IServiceBuilder<TMarker>`, `IRegistrationMarker<TMarker>` and
  `ServiceCollectionExtensions` (`NewBuilder`, `AddRegistrationMarker`, `IsRegistered`).
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md): implementation packages depend only on
  abstractions, which is why markers live in `.Abstractions`.
- [ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md): the builder hierarchy is
  registration-time only; the runtime routing tree is a separate mechanism.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the service,
  strategy and mediator model these registrations wire up.
- [ADR-1051](1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md),
  [ADR-1057](1057-local-account-management-is-an-opt-in-capability-that-provisions-the-principal-eagerly-and-authorizes-per-tenant.md) and
  [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md): the opt-in capabilities that gate on these
  builders.
