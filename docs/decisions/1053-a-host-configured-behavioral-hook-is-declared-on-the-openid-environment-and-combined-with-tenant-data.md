# 1053. A host-configured behavioral hook is declared on the OpenID environment and combined with tenant data

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

Some host-level customizations are logic, not data: a delegate the host can replace to change how the library derives a
value from a `ClaimsPrincipal`, parses a token or shapes a claim. Subject-id extraction is the canonical example, and it
has two separable dimensions. Which claims carry the subject is a per-tenant data choice, because a federated tenant may
key on a non-standard claim. How to extract the subject from a `ClaimsPrincipal` is host logic, a function that a data
setting cannot express. [ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md)
prefers settings over start-up options for per-request knobs and governs the data dimension; the logic dimension is
genuinely a start-up option.

The question is how a consumer reaches the configured behavior. `OpenIdOptions` lives in the core slice, so a consumer
that injects `IOptions<OpenIdOptions>` depends on core. The management slice may reference only `*.Abstractions`
([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
so it cannot inject the options at all. Left unaddressed, each surface implements the hook its own way: the protocol
handlers read the host's delegate while the management audit recorder hardcodes a subset of the claims. A host that
customizes the delegate then sees it honored on one surface and not on the other, and the single source of truth is lost.

## Decision

**A host-replaceable behavioral hook is declared on `OpenIdEnvironment` in `NCode.Identity.OpenId.Abstractions`, backed
by an `OpenIdOptions` delegate in `NCode.Identity.OpenId.Core` and wired in `DefaultOpenIdEnvironment`. A consumer reaches
it through the environment and never by injecting `IOptions<OpenIdOptions>`, so the management slice honors the same
hook.**

### Subject-id extraction has three layers

- **Which claims (per-tenant data).** `subject_claim_types` is an ordered list setting on the resolved tenant, defaulting
  to `OpenIdConstants.DefaultSubjectClaimTypes`: `sub`, then `nameid`, then the long-URI `nameidentifier`.
- **How to extract (host logic).**
  `OpenIdEnvironment.GetSubjectId(ClaimsPrincipal, IReadOnlyCollection<string> subjectClaimTypes)` is the
  host-replaceable algorithm, taken from `OpenIdOptions.GetSubjectId`. Its default returns the first non-empty claim that
  matches the ordered claim types.
- **Combining them per request.** `OpenIdContext.GetSubjectId(ClaimsPrincipal)` reads the resolved tenant's
  `subject_claim_types` and applies the environment algorithm. Every end-user surface calls this one method, so it honors
  the host's logic and the tenant's data.

`NormalizeSubjectId` and `GetSubjectIdentity` follow the same shape: declared on `OpenIdEnvironment`, configured from
`OpenIdOptions`. `NormalizeSubjectId` is the inverse of extraction, so
`GetSubjectId(NormalizeSubjectId(subject, id)) == id`, and `OpenIdContext.NormalizeSubjectId` applies the tenant's claim
types. Subject-building handlers, such as the password grant, stamp the subject under the tenant's primary subject claim
type, so the write and the read round-trip agree.

### Two subject domains

The end-user subject, read by the authorize gate, the federated resolver and the subject-building handlers, goes through
`OpenIdContext.GetSubjectId` and so honors the end user's tenant. The management audit actor is a control-plane
identity, not the target tenant's federated user. It is read through the app-level `IOpenIdEnvironmentProvider` with
`OpenIdConstants.DefaultSubjectClaimTypes`, never with the target tenant's setting. Both domains share one extraction
algorithm; only the claim-types input differs. The environment is a process-level value with no request or tenant
state, so provider access needs no context.

### The rule

Per-tenant data is a setting. Host logic is an environment delegate. A concern that needs both gets a context method that
combines them. A value a consumer computes itself stays local.

### Raw extraction versus federated resolution

`GetSubjectId` is raw, store-free claim extraction. Mapping a caller to the server-owned principal id is
`IPrincipalResolver` ([ADR-1035](1035-federated-principals-and-identity-resolution.md)), which takes the raw subject as
its input. The two do not merge: `OpenIdEnvironment` cannot take an `IStoreManager`, and the principal id is the output
of resolution, so having `GetSubjectId` return it would be circular.

### Hooks are leaf operations

A hook derives a value from the principal and the supplied claim types only. It never reaches the environment, the
context, the platform, the tenant or store creation, and no creation path calls a hook. Every call site passes an
already-materialized environment, so the seam stays acyclic.

## Options considered

- **Fold the claim choice into host logic, with no data setting.** Rejected: it loses per-tenant configuration, which
  [ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md) places in
  data.
- **A dedicated `ISubjectIdAccessor` service.** Rejected: it adds a second abstraction, and one more dependency for every
  consumer, for something the environment already models.
- **A public static helper.** Rejected: it shares the default logic but ignores a host's override, so customization does
  not propagate.
- **Management-local options.** Rejected: they create a second configuration surface for the same concept.
- **Layered data setting, environment delegate and context seam (chosen).** It reuses the environment and tenant-settings
  seams, adds no service, options type or package, and satisfies
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
  because abstractions declare the members and core configures them.

## Consequences

- A host customizes how the subject is derived in one place, `OpenIdOptions.GetSubjectId`, and a tenant customizes which
  claims identify the subject in one place, `subject_claim_types`. The protocol endpoints and the management audit trail
  honor both uniformly.
- A future host-level logic hook has a documented home instead of an ad hoc injection or a copy per surface.
- The federated principal resolver derives its subject through `OpenIdContext.GetSubjectId`, so the authenticate gate and
  the resolver read one configured source. Only the upstream issuer claim stays a setting of its own.
- `OpenIdEnvironment` and `OpenIdContext` grow members as hooks are added, each a deliberate public surface addition
  ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).

## References

- `src/NCode.Identity.OpenId.Abstractions/Environments/OpenIdEnvironment.cs`: the host-replaceable extraction hooks.
- `src/NCode.Identity.OpenId.Abstractions/Contexts/OpenIdContext.cs`: the tenant-aware `GetSubjectId` seam.
- `src/NCode.Identity.OpenId.Abstractions/OpenIdConstants.SubjectClaimTypes.cs` and the `subject_claim_types` setting:
  the per-tenant data default.
- `src/NCode.Identity.OpenId.Core/Environments/DefaultOpenIdEnvironment.cs` and `DefaultOpenIdEnvironmentFactory.cs`:
  the core wiring from `OpenIdOptions`.
- `src/NCode.Identity.OpenId.Core/Options/OpenIdOptions.cs`: the host knobs.
- `src/NCode.Identity.OpenId.Management/Auditing/DefaultManagementAuditRecorder.cs`: a cross-package consumer.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1035](1035-federated-principals-and-identity-resolution.md),
  [ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md),
  [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md).
