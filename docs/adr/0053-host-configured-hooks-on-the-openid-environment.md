# 53. Host-configured behavioral hooks are surfaced on the OpenID environment

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

Some host-level customizations are **logic**, not data: a delegate the host can replace to change how the library
derives a value from a `ClaimsPrincipal`, parses a token, or shapes a claim. Subject-id extraction is the canonical
example. But it has **two separable dimensions** that were historically conflated: _which_ claim(s) carry the subject
(a per-tenant **data** choice — a federated tenant may key on a non-standard claim) and _how_ to extract it from a
`ClaimsPrincipal` (host **logic** — a function, which a data setting cannot express). [ADR-0036](0036-unified-openid-request-environment.md)
("settings over start-up options for per-request knobs") governs the data dimension; the logic dimension is genuinely a
start-up option.

The question is **how a consumer reaches that configured behavior**. `OpenIdOptions` lives in the core slice, so a
consumer that injects `IOptions<OpenIdOptions>` takes a dependency on core. The management slice may depend only on
`*.Abstractions` ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)), so it cannot inject the
options at all. Left unaddressed, each surface re-implements the hook its own way: the protocol handlers read
`OpenIdOptions.GetSubjectId`, while the management audit recorder hardcodes a subset (`sub ?? nameidentifier`). The
behaviors then drift — a host that customizes the delegate sees it honored on one surface but not the other — and the
"single source of truth" is lost.

## Decision

**Subject-id extraction is one seam with three cleanly separated layers: _which claims_ is per-tenant data (a
`subject_claim_types` setting), _how to extract_ is host-replaceable logic (a delegate surfaced on `OpenIdEnvironment`),
and `OpenIdContext.GetSubjectId` is the single per-request entry point that combines them. More generally, a
host-configured behavioral hook is declared on `OpenIdEnvironment` in `*.Abstractions`, implemented in core from
`OpenIdOptions`, and consumed through the environment — never by injecting the options.**

Each dimension lives where it belongs:

- **Data — which claims (per-tenant).** `subject_claim_types` is an ordered list setting on the resolved tenant,
  defaulting to `OpenIdConstants.DefaultSubjectClaimTypes` (the short `sub`, then the short `nameid` for handlers
  configured with `MapInboundClaims = false`, then the long-URI `nameidentifier` emitted by `Microsoft.AspNetCore.Identity`
  and the default inbound JWT claim-type map). It subsumes the former single `principal_source_claim` name **and** the
  old `allowNameId`/`allowUpn` booleans into one data-driven, per-tenant knob ([ADR-0036](0036-unified-openid-request-environment.md)).
- **Logic — how to extract (host-level).** `OpenIdEnvironment.GetSubjectId(ClaimsPrincipal, IReadOnlyCollection<string>
subjectClaimTypes)` is the host-replaceable algorithm (default: the first non-empty claim matching the ordered claim
  types), declared in abstractions and wired in `DefaultOpenIdEnvironment` from the `OpenIdOptions.GetSubjectId`
  delegate. The `GetSubjectIdentity(ClaimsPrincipal)` hook follows the same abstractions-declared, core-configured shape.
- **Seam — combine (per-request).** `OpenIdContext.GetSubjectId(ClaimsPrincipal)` reads the resolved tenant's
  `subject_claim_types` and applies the environment algorithm. Every end-user surface calls this one method and so
  honors both the host's logic and the tenant's data.

### Two subject domains

The **end-user** subject (the authorize/subject-authentication gate, the federated resolver, and the subject-building
handlers that stamp it) is read through `OpenIdContext.GetSubjectId`, so it honors the _end-user's_ tenant. The **admin
actor** recorded in the management audit trail is a self-issued/control-plane identity, not the target tenant's
federated user; it is read with `OpenIdConstants.DefaultSubjectClaimTypes` through the app-level environment
(`IOpenIdEnvironmentProvider`), deliberately **not** the request tenant's setting. Both domains share the one extraction
algorithm; only the claim-types input differs.

### Rule

A host-configured **behavioral** hook (replaceable logic) is declared on `OpenIdEnvironment` in abstractions, backed by
an `OpenIdOptions` delegate in core, and consumed through the environment — from an in-hand `OpenIdContext` (directly,
or via a convenience seam such as `OpenIdContext.GetSubjectId`), or from the app-level `IOpenIdEnvironmentProvider` when
no request context is needed (the environment is app-level, carrying no request or tenant state). A per-tenant **data**
knob is a setting on the resolved tenant ([ADR-0036](0036-unified-openid-request-environment.md)). When a concern needs
**both** — logic and per-tenant data — the data is a setting, the logic is the environment delegate, and a context
method combines them; a value a consumer _computes itself_ stays local.

### Boundary: raw extraction vs. federated resolution

`GetSubjectId` is **raw, store-free claim extraction** only. Mapping an authenticated caller to the server-owned,
stable principal id is a separate concern that requires persistence and lives behind `IPrincipalResolver`
([ADR-0035](0035-federated-principals-and-identity-resolution.md)). The two compose — the raw subject id is the _input_
to federated resolution, which emits the principal id carried into grants and the `sub` claim — and must not be merged:
`OpenIdEnvironment` is a lightweight, store-free value that cannot take an `IStoreManager`, and the federated id is the
_output_ of resolution, so having `GetSubjectId` return it would be circular.

## Options considered

- **Layered: per-tenant data setting + host-replaceable environment algorithm + context seam (chosen).** Reuses the
  existing shared environment and tenant-settings seams; no new DI service, options type, or package; satisfies ADR-0016
  because abstractions declare the members and core configures them. One source of truth that every surface honors,
  while each dimension (which-claims data vs. how-to-extract logic) lives where ADR-0036 says it should.
- **Demote the per-tenant claim choice to host-level logic (no data setting).** Simpler — fold the subject claim into
  the host `GetSubjectId` delegate and delete `principal_source_claim` — but it trades away per-tenant, data-driven
  configurability (counter to ADR-0036/ADR-0025) and splits a matched `(issuer, subject)` pair across two scopes. The
  layered option keeps the data knob _and_ the single seam.
- **A dedicated DI service (e.g. `ISubjectIdAccessor`) registered by core.** Also works and respects ADR-0016, but adds
  a second request-scoped abstraction for something the environment already models, and every consumer must inject one
  more dependency. More surface for no extra capability.
- **A public static helper in abstractions.** Shares the _default logic_ but not the _configured_ behavior: a consumer
  calling the static method ignores a host's `OpenIdOptions` override, so customization silently fails to propagate —
  the exact drift this decision removes.
- **Management-local options.** Re-introduces a second configuration surface for the same concept, which is the
  divergence this decision eliminates.

## Consequences

- A host customizes _how_ the subject is derived in one place (`OpenIdOptions.GetSubjectId`), and a tenant customizes
  _which_ claims identify the subject in one place (`subject_claim_types`); both are honored uniformly by the protocol
  endpoints and the management audit trail.
- The pattern is reusable: future host-level logic hooks have a clear, documented home instead of being injected ad hoc
  or duplicated per surface.
- `OpenIdEnvironment` and `OpenIdContext` grow members over time; each is a public abstract/virtual addition, so a new
  hook is a deliberate surface change tracked in `PublicAPI.Unshipped.txt` ([ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md)).
- A consumer reaches the seam the way its scope dictates: the **end-user** subject through `OpenIdContext.GetSubjectId`
  (tenant-aware), the **admin actor** through the app-level `IOpenIdEnvironmentProvider` with
  `OpenIdConstants.DefaultSubjectClaimTypes` (a self-issued control-plane identity must not read the target tenant's
  setting). The environment is a process-level singleton with no request or tenant state, so provider access needs no
  context and cannot return null.
- **The hooks must stay leaf operations to avoid re-entrancy.** An environment hook derives a value from the
  `ClaimsPrincipal` (and the supplied claim types) and nothing else — it must never reach back into environment,
  context, server, tenant, or store _creation_. Equivalently, no creation path (the environment/context/server/tenant
  factories) may call a hook. Holding this invariant keeps the seam acyclic: the hooks are pure extraction and every
  call site passes an already-materialized environment, so there is no recursion.
- The federated principal resolver ([ADR-0035](0035-federated-principals-and-identity-resolution.md)) derives its
  subject through the tenant-aware `OpenIdContext.GetSubjectId` seam, so the authenticate gate and the resolver's
  federated keying read one configured source — the tenant's `subject_claim_types`. Only the upstream issuer claim
  remains its own per-tenant setting. Subject-building handlers (such as the resource-owner password grant) stamp the
  subject under the tenant's primary subject claim type so the write/read round-trip stays consistent.

## References

- `src/NCode.Identity.OpenId.Abstractions/Environments/OpenIdEnvironment.cs` — the host-replaceable extraction hooks.
- `src/NCode.Identity.OpenId.Abstractions/Contexts/OpenIdContext.cs` — the tenant-aware `GetSubjectId` seam.
- `src/NCode.Identity.OpenId.Abstractions/OpenIdConstants.SubjectClaimTypes.cs` and the `subject_claim_types` setting —
  the per-tenant data default.
- `src/NCode.Identity.OpenId.Core/Environments/DefaultOpenIdEnvironment.cs` and `DefaultOpenIdEnvironmentFactory.cs` —
  the core wiring from `OpenIdOptions`.
- `src/NCode.Identity.OpenId.Core/Options/OpenIdOptions.cs` — the host knobs.
- `src/NCode.Identity.OpenId.Management/Auditing/DefaultManagementAuditRecorder.cs` — a cross-package consumer.
- [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-0035](0035-federated-principals-and-identity-resolution.md),
  [ADR-0036](0036-unified-openid-request-environment.md).
