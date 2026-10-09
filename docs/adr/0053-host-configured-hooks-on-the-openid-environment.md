# 53. Host-configured behavioral hooks are surfaced on the OpenID environment

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

Some host-level customizations are **logic**, not data: a delegate the host can replace to change how the library
derives a value from a `ClaimsPrincipal`, parses a token, or shapes a claim. Subject-id extraction is the canonical
example — `OpenIdOptions.GetSubjectId` (and `GetSubjectIdentity`) let a host override the default `sub` →
`nameidentifier` → `upn` priority. Such a hook is genuinely a start-up option (a function, which a per-tenant data
**setting** cannot express — see [ADR-0036](0036-unified-openid-request-environment.md), "settings over start-up
options for per-request knobs," which governs _data_ knobs).

The question is **how a consumer reaches that configured logic**. `OpenIdOptions` lives in the core slice, so a
consumer that injects `IOptions<OpenIdOptions>` takes a dependency on core. The management slice may depend only on
`*.Abstractions` ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)), so it cannot inject the
options at all. Left unaddressed, each surface re-implements the hook its own way: the protocol handlers read
`OpenIdOptions.GetSubjectId`, while the management audit recorder hardcodes a subset (`sub ?? nameidentifier`). The
behaviors then drift — a host that customizes the delegate sees it honored on one surface but not the other — and the
"single source of truth" is lost.

## Decision

**A host-configured behavioral hook is exposed as a member on `OpenIdEnvironment`. The contract is declared in
`*.Abstractions`; the default implementation is wired in core from `OpenIdOptions`; every HTTP surface consumes the hook
through the request's `OpenIdContext.Environment` rather than by injecting the options.**

`OpenIdEnvironment` already is the shared, per-request home for host-level, tenant-independent infrastructure (the JSON
options, the data protectors, the known-parameter catalog, the error factory), and it is reachable from both the
protocol and management surfaces through `OpenIdContext.Environment` (and from an `HttpContext` via
`GetOpenIdContext()`). That makes it the correct seam for host-configured logic too:

- **Declared in abstractions.** `OpenIdEnvironment.GetSubjectId(ClaimsPrincipal)` and
  `GetSubjectIdentity(ClaimsPrincipal)` are abstract members on the abstractions-level `OpenIdEnvironment`, so any
  surface — including management, which may not reference core — can call them.
- **Configured in core.** `DefaultOpenIdEnvironment` implements each member by delegating to the matching
  `OpenIdOptions` delegate; the environment factory injects `IOptions<OpenIdOptions>` and threads the delegates in. The
  booleans that build the default (`allowNameId`/`allowUpn`) stay private to the default factory and never leak to a
  call site.
- **Consumed through the environment.** The authorize/subject-authentication handlers and the token service, which
  already hold a context, read `openIdContext.Environment.GetSubjectId(...)` / `GetSubjectIdentity(...)`. The management
  audit recorder has no need of a request context — the environment is app-level infrastructure, so it injects
  `IOpenIdEnvironmentProvider` (the same singleton source the context factory uses) and reads
  `EnvironmentProvider.Get().GetSubjectId(...)`. None of them inject `IOptions<OpenIdOptions>` for this. A host
  customizes the delegate once and every surface honors it.

### Rule

A new host-level **behavioral** hook (a replaceable delegate, not a data value) follows this shape: define it as a
member on `OpenIdEnvironment` in abstractions, back it with an `OpenIdOptions` delegate in core, and have consumers
reach it through the environment — from an in-hand `OpenIdContext.Environment`, or directly from
`IOpenIdEnvironmentProvider` when no request context is needed (the environment is app-level, carrying no request or
tenant state). A per-tenant **data** knob remains a setting on the resolved tenant
([ADR-0036](0036-unified-openid-request-environment.md)); a value a consumer _computes itself_ stays local.

### Boundary: raw extraction vs. federated resolution

`GetSubjectId` is **raw, store-free claim extraction** only. Mapping an authenticated caller to the server-owned,
stable principal id is a separate concern that requires persistence and lives behind `IPrincipalResolver`
([ADR-0035](0035-federated-principals-and-identity-resolution.md)). The two compose — the raw subject id is the _input_
to federated resolution, which emits the principal id carried into grants and the `sub` claim — and must not be merged:
`OpenIdEnvironment` is a lightweight, store-free value that cannot take an `IStoreManager`, and the federated id is the
_output_ of resolution, so having `GetSubjectId` return it would be circular.

## Options considered

- **A member on `OpenIdEnvironment`, configured from options (chosen).** Reuses the existing shared seam; no new DI
  service, options type, or package; satisfies ADR-0016 because abstractions declare it and core configures it. One
  source of truth that every surface honors.
- **A dedicated DI service (e.g. `ISubjectIdAccessor`) registered by core.** Also works and respects ADR-0016, but adds
  a second request-scoped abstraction for something the environment already models, and every consumer must inject one
  more dependency. More surface for no extra capability.
- **A public static helper in abstractions.** Shares the _default logic_ but not the _configured_ behavior: a consumer
  calling the static method ignores a host's `OpenIdOptions` override, so customization silently fails to propagate —
  the exact drift this decision removes.
- **Management-local options.** Re-introduces a second configuration surface for the same concept, which is the
  divergence this decision eliminates.

## Consequences

- A host customizes subject-id derivation in one place (`OpenIdOptions.GetSubjectId`) and it is honored uniformly by the
  protocol endpoints and the management audit trail.
- The pattern is reusable: future host-level logic hooks have a clear, documented home instead of being injected ad hoc
  or duplicated per surface.
- `OpenIdEnvironment` grows members over time; each is a public abstract addition, so a new hook is a deliberate
  surface change tracked in `PublicAPI.Unshipped.txt` ([ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).
- A consumer reaches the hook through the environment — from an in-hand `OpenIdContext.Environment`, or directly from
  the app-level `IOpenIdEnvironmentProvider` when it has no context (as the audit recorder does). The environment is a
  process-level singleton with no request or tenant state, so provider access needs no context and cannot return null;
  a non-HTTP caller would resolve the provider the same way.
- **The hooks must stay leaf operations to avoid re-entrancy.** An environment hook derives a value from the
  `ClaimsPrincipal` and nothing else — it must never reach back into environment, context, server, tenant, or store
  _creation_. Equivalently, no creation path (the environment/context/server/tenant factories) may call a hook. Holding
  this invariant keeps the seam acyclic: today the hooks are pure extraction and every call site passes an
  already-materialized environment, so there is no recursion.
- The environment's raw `GetSubjectId` and `IPrincipalResolver`'s `PrincipalSourceClaim`-driven read decide "which claim
  is the subject" independently. They agree on the default (`sub`) but can diverge under custom configuration; reconciling
  them is tracked as pending work.

## References

- `src/NCode.Identity.OpenId.Abstractions/Environments/OpenIdEnvironment.cs` — the declared hooks.
- `src/NCode.Identity.OpenId.Core/Environments/DefaultOpenIdEnvironment.cs` and `DefaultOpenIdEnvironmentFactory.cs` —
  the core wiring from `OpenIdOptions`.
- `src/NCode.Identity.OpenId.Core/Options/OpenIdOptions.cs` — the host knobs.
- `src/NCode.Identity.OpenId.Management/Auditing/DefaultManagementAuditRecorder.cs` — a cross-package consumer.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-0035](0035-federated-principals-and-identity-resolution.md),
  [ADR-0036](0036-unified-openid-request-environment.md).
