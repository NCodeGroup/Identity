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
`IServiceCollection`: a **marker** type per library (an empty `readonly struct` implementing
`IRegistrationMarker<TMarker>` — it carries no data, its _type identity_ is the whole signal) and
`IServiceBuilder<TMarker>` — a wrapper that holds the collection plus a compile-time marker
discriminator. Constructing a `ServiceBuilder<TMarker>` registers the marker as a singleton
(`AddRegistrationMarker`), so a library's presence is a **compile-time type** carried by the builder value
(and a registered singleton that guards against accidental double-registration). A library hands its
builder back from `AddXLibrary()`, and a dependent **requires** that builder as its
`extension(IServiceBuilder<…>)` receiver. A registration that needs a **second** prerequisite takes that
prerequisite's builder as a **required `IServiceBuilder<…>` parameter** — the builder its own registration
returned — so a missing prerequisite is a compile error (the method is uncallable without it), not a runtime
check. Every prerequisite is proven at **compile time**, never asserted at runtime. The
builder's "hierarchy" (`NewBuilder<T>()`) is a registration-time organizational device only — at runtime
there is one flat `IServiceCollection`
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
`IServiceBuilder<ThatLibrary>` as a receiver without taking a forbidden implementation reference. Every library
marker now lives in its abstractions package (`OpenIdManagementLibrary` in
`NCode.Identity.OpenId.Management.Abstractions`, `OpenIdCoreLibrary` in `NCode.Identity.OpenId.Abstractions`,
`JsonWebTokensLibrary` in `NCode.Identity.JsonWebTokens.Abstractions`).

**Which abstractions packages own a marker — the litmus is "is this a library _layer_ or framework _seam_ that other
registrations must prove is present?"** A library layer (an always-on `AddXLibrary()` whose presence a dependent must be
able to require) owns a marker minted by its `AddXLibrary()`. **Additionally, a dependency-inverted _seam_ that other
registrations build on owns a marker, minted by _every_ implementation registration — including a custom one — which each
registration _returns_ as an `IServiceBuilder<…>` so dependents can require it, either as their receiver or as a required
`IServiceBuilder<…>` parameter.** Two seams own a marker today:

- **The persistence framework owns `StoreManagerLibrary`** (`NCode.Persistence.Abstractions`): `AddEntityFrameworkCorePersistence`
  mints it and returns `IServiceBuilder<StoreManagerLibrary>`, and the store slices that register concrete stores against
  it — `AddLocalAccountEntityFramework` — take that builder as their receiver, so a store slice **cannot be written**
  without the persistence framework.
- **The local-account store owns `LocalAccountLibrary`** (`…OpenId.Accounts.Abstractions`): `AddLocalAccountEntityFramework`,
  `AddAspNetIdentityLocalAccountStore`, and any custom store **mint and return** `IServiceBuilder<LocalAccountLibrary>`, and
  `AddLocalAccountAuthentication` / `AddLocalAccountInteractiveAuthentication` / `AddLocalAccountManagement` take it as a
  **required `IServiceBuilder<LocalAccountLibrary>` parameter** (their receiver stays the library they extend), so a
  capability **cannot even be written** without a store (the accepted cost is that a custom store must mint/return the marker
  too).

A package that is neither a layer nor such a seam owns **none**, and that is correct, not an omission:

- **Pure contract / DTO abstractions** (`…OpenId.Accounts.Management.Abstractions` wire contracts, `…Settings.Abstractions`,
  `…AspNetCore.Abstractions` result helpers) register nothing and have no presence to prove.
- **Cross-library helpers** (`…OpenId.Messages.Abstractions`'s `AddMessageFactory`) mint no library marker because they run
  from more than one library's builder (see the non-generic-`IServiceBuilder` section below).
- **The interactive credential verifier** (`IInteractiveCredentialVerifier`), registered by the host _after_ the interactive
  surface it serves. The consumer registers before the verifier exists, so there is no builder to thread; a missing verifier
  surfaces at resolution. **A compile-time seam marker applies exactly when the seam is registered _before_ its dependent**
  (host registers a store, then adds the account capabilities); otherwise the gap is a resolution-time failure.

So a bolt-on that augments a seam gates its **framework** prerequisite as its receiver (e.g. `AddLocalAccountManagement`
on `IServiceBuilder<OpenIdManagementLibrary>`) and, when it also requires a seam registered before it, takes the seam's
builder as a required `IServiceBuilder<…>` parameter.

### Every prerequisite is compile-time — the receiver plus required `IServiceBuilder<…>` parameters

A registration encodes **all** its prerequisites in the type system; none is a runtime check.

- **Primary prerequisite — the receiver.** A registration's main prerequisite is its receiver type:
  `extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder) { … AddInteractiveAuthentication(…) }`.
  The host cannot call it without an `IServiceBuilder<OpenIdAuthenticationLibrary>`, which only
  `AddOpenIdAuthenticationLibrary(…)` produces.
- **A second prerequisite — a required `IServiceBuilder<…>` parameter.** A registration that needs a second
  prerequisite takes it as a **required parameter**: `AddLocalAccountAuthentication(IServiceBuilder<LocalAccountLibrary>)`
  and `AddOpenIdAuthenticationLibrary(IServiceBuilder<JoseLibrary>)`. The parameter is the builder the prerequisite's own
  registration returned, so the method is **uncallable** without it — a missing prerequisite is a compile error — and the
  receiver builder is returned directly so the chain simply continues on the primary library. A prerequisite registration
  therefore always **returns its builder** — even a dependency-inverted store returns `IServiceBuilder<LocalAccountLibrary>`
  — so it can be passed as proof. The parameter is a pure compile-time **witness** (its service collection is the
  receiver's, so it is otherwise unused in the body); it is **not** a marker _value_, because a marker is an empty
  `readonly struct` any caller could `default`/`new`, so a marker instance would prove nothing — only a minted builder is
  an unforgeable handle, and a dedicated proof type would just be a renamed builder. (There is no `VerifyIsRegistered`
  runtime check; it was removed, as were the marker's `DisplayName` and `ConfigureMethod` members and the `IMarker<TMarker>`
  base — a marker's type identity is its whole signal.)

### Nesting per package and per folder

- **Class name.** Every registration file is `DefaultRegistration` (one per folder); there is never a
  feature-named registration class. The one exception is a package that needs a second _receiver shape_ in the
  same folder (a chain-root on `IServiceCollection` _and_ opt-ins on its own library builder): because one static
  class may hold only one `extension(...)` receiver block (a second trips CA1708), the opt-ins move to a sibling
  `…Registration` class in the same folder (e.g. `NCode.Identity.Events` keeps `DefaultRegistration.AddEventServices`
  and puts `AddEventHandler` / `AddBackgroundEventDelivery` on `EventLibraryRegistration`).
- **Package root.** `AddXLibrary()` extends `IServiceBuilder<Prereq>` (taking any second prerequisite as a required
  `IServiceBuilder<Prereq2>` parameter), creates the library's own builder via `builder.NewBuilder<XLibrary>()`,
  fans out to the folder-level registrations, and returns `IServiceBuilder<XLibrary>`.
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
- **Bolt-on capability packages.** A bolt-on extends the marker of the library it augments (local-account
  management → `OpenIdManagementLibrary`; the interactive surface → `OpenIdAuthenticationLibrary`), taking a
  second prerequisite as a required `IServiceBuilder<…>` parameter when it also requires a seam (the account
  capabilities take `IServiceBuilder<LocalAccountLibrary>`), and returns that library builder — it adds to an
  existing library, it does not introduce a new library layer.

### What stays on a naked `IServiceCollection` — by design, not oversight

**The litmus test is "would this registration do the right thing if it were the only thing registered?"** If a
registration **replaces, hooks, or consumes another library's services** — so that calling it _without_ that library
leaves a broken or meaningless configuration — it is a **bolt-on** and must gate on that library's
`IServiceBuilder<TMarker>`, never a naked collection. Only registrations that genuinely stand alone take a bare
collection, and they fall into exactly these kinds:

The marker receiver expresses a **framework prerequisite**. Two kinds of registration have none and
therefore correctly take a naked `IServiceCollection`:

- **Chain roots.** The first link that establishes a marker and has no prerequisite library —
  `AddIdentityLibrary()`, `AddSecretsLibrary()`, `AddEventServices()`, and the persistence-framework root
  `AddEntityFrameworkCorePersistence<TDbContext>()` (which mints `StoreManagerLibrary`) — bootstraps from the
  bare collection and returns the new builder.
- **Generic utility helpers.** A within-library convenience that registers one service
  (`AddAuthorizationHandler<T>`, `AddDefaultPasswordHasher`, `AddEndpointGroupRouting`)
  is a helper, not a capability entry point, and stays a bare-collection extension.

The **dependency-inverted local-account stores** (`AddLocalAccountEntityFramework`, `AddAspNetIdentityLocalAccountStore`)
used to sit here as naked-collection registrations, but they build on the persistence framework seam, so they now receive
`IServiceBuilder<StoreManagerLibrary>` (proving `AddEntityFrameworkCorePersistence` ran) and mint/return
`IServiceBuilder<LocalAccountLibrary>`. Being free of a _library_ prerequisite is still the point of the inversion
([ADR-0060](0060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
[ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)) — they gate on a _framework seam_, not a library
layer — so they are not bolt-ons; they hand back proof of their own presence for the account capabilities to pass as a
parameter.

### Cross-library helpers receive the non-generic `IServiceBuilder`

A few registration helpers are invoked from **more than one** library's builder, so they cannot name a single
`IServiceBuilder<TMarker>` receiver, yet they still must run **inside a library-builder scope** (they need the builder,
not merely the collection). These take the **non-generic `IServiceBuilder`** receiver — strictly stronger than a naked
`IServiceCollection` (the caller must hold _some_ library builder) but library-agnostic. The real-world cases are
`AddMessageFactory<TMessage>` (`NCode.Identity.OpenId.Messages.Abstractions`, called from both the Core-library builder
and endpoint-group builders) and `AddEndpointProvider<T>` / `AddEndpointGroup<T>`
(`NCode.Registration.AspNetCore`, called from every library that maps endpoints). This is the correct shape for a
cross-cutting helper; do **not** pin it to one marker (that would break the other callers) and do **not** drop to a
naked collection (that would lose the in-a-builder guarantee).

### Cross-cutting infrastructure registered inline

Foundational **cross-cutting infrastructure** that every layer consumes and that mints **no** marker — property bags and
data protection (`AddPropertyBag`, `AddDataProtection`) — is registered **inline on the bare collection** before the
marker-gated library chain. It has no presence another package must prove, so there is nothing for a marker to carry.

The **event bus** was once in this inline-infrastructure bucket, but it is in fact a small library of its own: it has a
standalone root and two opt-ins that are meaningless without it. It therefore **mints `EventsLibrary`** (in
`NCode.Identity.Events.Abstractions`) — `AddEventServices()` is a chain root on the bare collection that returns
`IServiceBuilder<EventsLibrary>`, and its opt-ins `AddEventHandler<TEvent, THandler>()` / `AddBackgroundEventDelivery()`
receive `IServiceBuilder<EventsLibrary>`, so a handler or background-delivery registration **cannot compile** without the
event services. (Because the root's receiver is the bare collection and the opt-ins' receiver is the library builder, the
two receiver shapes live in separate classes — `DefaultRegistration` and `EventLibraryRegistration` — per the CA1708
one-receiver-per-class rule.)

### Dev-host convenience registrations gate on the server marker

The development-only helpers in `NCode.Identity.Server` that **replace** a composed server service or **hook** a server
command handler — `AddDeveloperKeys` / `AddEphemeralDeveloperKeys` / `AddDeveloperSigningKey` (which `Replace` the
`IOpenIdTenantFactory` and add a `SeedTenantCommand` handler) and `AddDeveloperInteractiveSeed` (which adds
`SeedTenantCommand` + `GetAccessTokenSubjectClaimsCommand` handlers) — **fail the litmus**: they are meaningless unless
`AddIdentityServer()` composed the server first. They therefore extend `IServiceBuilder<IdentityServer>` and are chained
off the composition root (`AddIdentityServer().AddDeveloperKeys(…)`), not called on the bare collection. The genuinely
standalone dev helper `AddDeveloperCors` (a plain ASP.NET Core CORS policy that augments no NCode library — it is even
registered _before_ `AddIdentityServer()`) stays a bare-collection extension.

### Composition root

`AddIdentityServer()` (the `NCode.Identity.Server` composition root) is the one place that starts the
chain and references implementation packages; it returns `IIdentityServerBuilder : IServiceBuilder<IdentityServer>`,
off which every library and bolt-on is chained. Hosts compose by **chaining on that builder**, never by
calling capability methods on the bare `IServiceCollection`.

## Options considered

- **Naked `IServiceCollection` for capabilities (the prior state of the Accounts/interactive
  bolt-ons).** Rejected: no compile-time prerequisite; a capability silently binds against an absent
  library and fails confusingly at runtime.
- **Runtime `VerifyIsRegistered` for additional prerequisites.** Rejected and **removed**: it defers an avoidable
  mistake to startup and is order-sensitive. A second prerequisite is instead a **required `IServiceBuilder<…>` parameter**
  (the proof the prerequisite's own registration returned), so every prerequisite — primary receiver or additional
  parameter — is a compile error when missing. `VerifyIsRegistered` no longer exists.
- **A composite `IServiceBuilder<TMarker1, TMarker2>` receiver (combined with `primary.And(other)`).** Considered and
  **rejected** as over-built: it carries both proofs in the receiver _type_, but needs a whole second builder interface +
  implementation + an `And` combinator + a `.Primary` accessor, and the call site reads awkwardly
  (`a.And(b).AddX()`, re-combining mid-chain). A **required parameter** proves exactly the same thing at compile time
  (the method is uncallable without the argument) with none of that machinery and a cleaner call site
  (`a.AddX(b)`) — the "caller might forget the argument" objection does not apply to a _required_ parameter.
- **A marker _value_ (or a dedicated proof type) as the parameter instead of the builder.** Rejected: a marker is an empty
  `readonly struct` any caller can `default`/`new`, so a marker instance witnesses nothing; the parameter must be an
  unforgeable minted handle, which is the builder. A bespoke proof/ticket type would just be a renamed, narrower builder —
  more surface for no gain.
- **A DI-container feature or source generator to express ordering.** Rejected: heavier and
  container-coupled. The marker wrapper is a few lines, container-agnostic, and AOT-friendly.

## Consequences

- Adding a framework capability to a host that lacks the underlying library **fails to compile** — the
  intended guarantee.
- The opt-in **framework capabilities** gate on their library marker receiver and take any second prerequisite as a
  required parameter: `AddLocalAccountAuthentication` and `AddLocalAccountInteractiveAuthentication` extend
  `IServiceBuilder<OpenIdAuthenticationLibrary>` and take `IServiceBuilder<LocalAccountLibrary>`; `AddInteractiveAuthentication`
  extends `IServiceBuilder<OpenIdAuthenticationLibrary>` (no second prerequisite); `AddLocalAccountManagement` extends
  `IServiceBuilder<OpenIdManagementLibrary>` and takes `IServiceBuilder<LocalAccountLibrary>`. Each returns its receiver
  builder so the chain continues on the primary library. The `OpenIdManagementLibrary` marker moves into
  `NCode.Identity.OpenId.Management.Abstractions` so a capability referencing only abstractions can name it. These are
  breaking surface changes, acceptable pre-release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- The **persistence framework** owns `StoreManagerLibrary` (`NCode.Persistence.Abstractions`): the Entity Framework root
  `AddEntityFrameworkCorePersistence<TDbContext>()` mints it, and the local-account store slices
  (`AddLocalAccountEntityFramework`, `AddAspNetIdentityLocalAccountStore`) receive `IServiceBuilder<StoreManagerLibrary>`
  and return `IServiceBuilder<LocalAccountLibrary>`, so a store slice cannot be registered without the persistence
  framework and a capability cannot be registered without a store.
- The **event bus** owns `EventsLibrary` (`NCode.Identity.Events.Abstractions`): `AddEventServices()` mints it and
  returns `IServiceBuilder<EventsLibrary>`, and `AddEventHandler<TEvent, THandler>()` / `AddBackgroundEventDelivery()`
  receive that builder — so an event opt-in cannot compile without the event services.
- Host wiring moves from a flat list of `serviceCollection.AddX()` calls to chaining off the builders
  the composition root exposes (`AddIdentityServer()` → `IIdentityServerBuilder`, whose
  `OpenIdAuthenticationLibraryBuilder` / `OpenIdManagementLibraryBuilder` properties carry the capability
  bolt-ons), which also documents the dependency order at the call site.
- One registration vocabulary (`IServiceBuilder`) spans packages and folders; the registration class
  is always `DefaultRegistration`.
- The foundational standalone libraries now gate at **compile time** like the rest of the chain:
  `AddJoseLibrary` extends `IServiceBuilder<SecretsLibrary>` and `AddJsonWebTokensLibrary` extends
  `IServiceBuilder<JoseLibrary>`, and every library marker lives in that library's `.Abstractions` package
  (`JsonWebTokensLibrary` → `NCode.Identity.JsonWebTokens.Abstractions`, `OpenIdCoreLibrary` →
  `NCode.Identity.OpenId.Abstractions`). **No registration relies on a runtime `VerifyIsRegistered` — the method was
  removed; a second prerequisite (e.g. `AddOpenIdAuthenticationLibrary`'s `IServiceBuilder<JoseLibrary>` parameter, and the
  account capabilities' `IServiceBuilder<LocalAccountLibrary>` parameter) is a compile-time required argument, not a
  runtime check.**
- A **marker is an empty `readonly struct`** — the `DisplayName` and `ConfigureMethod` members and the `IMarker<TMarker>`
  base were removed, so the type identity is the entire signal and there is no per-marker data to keep in sync.

## References

- `NCode.Registration`: `IServiceBuilder` / `IServiceBuilder<TMarker>`, `IRegistrationMarker<TMarker>`,
  `ServiceCollectionExtensions` (`NewBuilder` / `AddRegistrationMarker` / `IsRegistered`).
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md) — implementation packages
  depend only on abstractions (why markers must live in `.Abstractions`).
- [ADR-0043](0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) — the builder's
  hierarchy is registration-time only; the runtime routing tree is a separate mechanism.
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service / strategy /
  mediator extensibility model these registrations wire up.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — pre-release posture permitting the
  breaking migration.
