# 0014. Resource identifiers are server-generated and opaque

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode.Identity maintainers

## Context

The management API creates four kinds of addressable resource — **server**, **tenant**, **client**, and
**secret** — each identified on the wire by a string id (`ServerId`, `TenantId`, `ClientId`, `SecretId`, the
last also serving as a JWK `kid`). Until now those ids were **caller-supplied**: the create request carried the
id and the server persisted it verbatim (secrets were a half-measure — caller-supplied when present, else a
server-minted `Guid`). The [owner-lifecycle work](0011-secret-management-api.md) added a uniqueness precondition
([ADR-0015](0015-management-core-validation-via-validators.md), originally the pipeline of the superseded
[ADR-0013](0013-management-precondition-mediator-pipeline.md)) to reject collisions, which made caller-supplied
ids workable but left three open questions the team resolved deliberately:

- Should a caller be able to choose the id at all?
- If the server mints ids, what shape are they — a `Guid`, a snowflake, an opaque random string?
- Do human-meaningful names disappear, or coexist with an opaque id?

Two facts shaped the answer. First, every management resource is already behind a resource-based
authorization check, so an id's guessability is **not** a security control — relying on an opaque id for
protection would be security-through-obscurity, and if it were load-bearing the authorization would be the
bug. Second, the entities already carry a server-generated surrogate `long` primary key (IdGen snowflake, via
`[UseIdGenerator]`), so the exposed string id is a _secondary_ identifier, not the storage key — changing how
it is minted touches only the create path, not the schema.

## Decision

**All resource identifiers are generated server-side and are opaque.** A create request no longer carries the
id of the resource it creates; the server mints it and returns it in the `201 Created` body and `Location`.

- **Form.** An id is a **128-bit CSPRNG value, base64url-encoded, with no prefix or structure**, produced by
  the house facade `ICryptoService.GenerateUrlSafeKey(16)` — the same primitive already used for grant keys
  (256-bit) and token ids. This keeps id generation on the one injectable crypto seam (testable by mocking the
  facade) rather than a bare `Guid.NewGuid()`, and yields ids that are unguessable, unpredictable, and
  pattern-free. `Guid` (only ~122 bits, not contractually CSPRNG) is **not** used for domain ids; the IdGen
  snowflake remains for internal surrogate keys only, which are never exposed.
- **Names stay names.** Human-meaningful, caller-supplied fields are unchanged: a tenant still carries its
  `DomainName` (the routing/lookup slug) and `DisplayName`. The decision demotes _identity_ to an opaque
  server value; it does not remove _naming_. Client and server gain no new name field for now (additive later
  if desired — the Auth0/Keycloak dual-identifier model).
- **Parent references stay caller-supplied.** A create request still names the _parent_ it attaches to (a
  client's `TenantId`), because that is a reference to an existing resource, not the id being minted.
- **Uniqueness is kept as a safety net.** The validators' uniqueness checks and the store's unique
  constraint remain. At 128 bits a collision is astronomically unlikely, but the check is cheap and keeps the
  invariant enforced defensively rather than assumed.

The corresponding create-request fields (`ServerId`, `TenantId` on the tenant create, `ClientId`, `SecretId`)
are removed from the public DTOs.

## Options considered

- **Caller-supplied natural-key ids (the prior model).** Rejected as the default. It welds "what an operator
  typed once" to "what every foreign key, issued token, and audit record points at," so the id can never be
  renamed, and it pushes format/validation/collision concerns onto every caller. It remains defensible for a
  protocol value a client must know a priori (`client_id` under RFC 7591 DCR), but that is an additive future
  option, not the baseline.
- **Server-minted but structured (`Guid`, snowflake, or type-prefixed).** Rejected. A `Guid` is lower-entropy
  and not guaranteed CSPRNG; a snowflake leaks creation time and ordering; a type prefix adds parsing surface
  for no benefit here. A bare high-entropy random string is the simplest opaque identifier and matches the
  existing `GenerateUrlSafeKey` house pattern.
- **Opaque id _and_ a required user-provided `name` slug on every entity (full dual-identifier model).**
  Deferred, not rejected. It is the mature Auth0/Keycloak shape, but adding a new unique `name` field to
  client and server is additional public surface and validation that can land additively when a concrete need
  appears. Tenant already has `DomainName`/`DisplayName`, which cover the naming role today.

## Consequences

- Create requests shrink: the caller supplies content and parent references, never the new id. A caller that
  needs the id reads it from the `201` response (body + `Location`) — the REST-idiomatic contract.
- The id is a stable, meaningless anchor: renaming a resource's human name (e.g. a tenant `DisplayName`) never
  disturbs the id, its foreign keys, or previously issued references.
- `SecretId` generation moves off `Guid.NewGuid()` onto the injected `ICryptoService`, removing a static,
  un-faceted dependency from the endpoint handlers and making the create path fully unit-testable via the
  crypto facade.
- This is a **breaking** change to the public create DTOs and to `client_id` semantics (a client's id is now
  server-assigned and opaque rather than caller-chosen). It is a major-version change, tracked by the public
  API baselines.
- BYO ids for a genuine a-priori protocol identifier (DCR-style `client_id`) remain a possible additive future
  option; the create DTOs can grow an optional "requested id" field guarded by its own review without breaking
  this baseline.

## References

- [ADR-0011](0011-secret-management-api.md) — the secret/owner management API whose create paths this governs.
- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the surrogate row token; ids are eager-assigned
  on insert alongside it.
- [ADR-0015](0015-management-core-validation-via-validators.md) — the core validators that assert uniqueness
  (among other preconditions) as a safety net.
- `NCode.Identity.Logic.ICryptoService` / `CryptoServiceExtensions.GenerateUrlSafeKey` — the id-generation facade.
- `NCode.Identity.OpenId.Persistence.EntityFramework.IdValueGenerator` — the internal surrogate-key generator
  that is unaffected by this decision.
