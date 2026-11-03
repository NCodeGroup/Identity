# 1395. A stored record that lacks what the serving code writes is malformed and fails closed

- **Status:** Accepted (amends
  [ADR-1113](1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md),
  [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) and
  [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md))
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)
- **Supersedes:** [project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md),
  its bullet that reads a document without `$ver` as version 1; and, until the first stable release, its
  additive-by-default bullet

## Context

The family is greenfield and pre-release
([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)): no released package, deployed database or consumer
holds a stored record. Every record a store holds is one that the serving code wrote.

A reader that accepts a record lacking what the serving code writes gives that record a meaning the serving code never
gives it:

- [project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md) reads a
  document without `$ver` as version 1, for a project that adopts the envelope over documents it already stored. The
  family's grant payloads carry the envelope from their first stored document: `IPersistedPayloadSerializer.Serialize`
  writes it, and the protected payload of every grant is an `nce1.` envelope over that output
  ([ADR-1265](1265-grant-payloads-are-protected-under-the-tenant-master-key.md)). A document without `$ver` is
  therefore corrupt or foreign, and reading it as version 1 deserializes a document of unknown origin as a grant.
- [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) decisions 2 and 3 make
  `SessionGrant.AbsoluteExpiresWhen` and `SessionGrant.IdleTimeout` nullable, read a session without an absolute end
  as ending at its grant's expiry, and stamp that end on its first touch. `ISessionService.CreateAsync` stamps both
  bounds on every session, and `CreateSessionRequest.IdleTimeout` and `CreateSessionRequest.AbsoluteLifetime` are
  required, so the nullable members and the fallback describe a session the server does not create.
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) decision 4 selects session
  grants without an expiration for termination, and a request serves such a session until its payload's bounds pass.
  Both describe a session the server does not create either: every session grant is added with a lifetime.
- The additive-only contract of ADR-0108 and
  [ADR-1113](1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md) keeps every older stored document
  readable. Before the first stable release no older document is one a reader must keep: holding a pre-release payload
  member optional, or shipping an upcaster for a shape no release wrote, carries an unreleased state into the first
  release's code.

## Decision

1. **A stored record that lacks what the serving code writes is malformed, and it fails closed.** A reader honors only
   the shape the serving code writes; it never supplies a default, a fallback or an alternative reading for a record
   that lacks part of it.
2. **A grant payload is a schema-versioned envelope, or it is rejected.** `IPersistedPayloadSerializer.Deserialize`
   reads a document that is an object with an integer `$ver` and a `$payload`, and throws a `JsonException` for any
   other document. The envelope, `[PersistedPayloadVersion]`, the `IPersistedPayloadUpcaster` pipeline, the
   forward-compatible read of a newer `$ver` and the `[JsonExtensionData]` bags of `SessionGrant`, `CibaGrant` and
   `DeviceCodeGrant` stay as ADR-1113 decides them. This holds after the first stable release too: the family's
   envelope exists from its first stored document, so no released store holds a document without one.
3. **A session always carries its bounds.** `SessionGrant.IdleTimeout` and `SessionGrant.AbsoluteExpiresWhen` are
   required and not nullable, like the other members the serving code always writes (`AuthTime`, `ClientIds`,
   `LastActivityWhen`). A stored session that lacks one does not deserialize, so it is never served as a valid session.
   A session grant is always stored with an expiration; one without it is malformed, and a request that reads it
   throws an `InvalidOperationException` rather than serving it; the background pass skips it (decision 4). A session
   ends at `AbsoluteExpiresWhen`, when its grant's expiry has passed, or when its idle window has lapsed, and a touch
   changes only `LastActivityWhen` and the grant's expiry.
4. **The ended-session pass selects grants whose expiry has passed.**
   `IGrantStore.GetUnrevokedExpiredPageAsync` selects the grants of the type that are not revoked and whose expiration
   is at or before `expiredAt`, of a tenant that is not disabled. A grant without an expiration has not expired, and
   the pass does not select it; a session grant without one is malformed (decision 3). The pass terminates each
   selected session on its own: a session that it cannot terminate, such as a malformed one, is logged with its
   identifier as `EndedSessionTerminationFailedForSession` (event 2070, Warning) and skipped, so the tenant's other
   ended sessions are terminated. `EndedSessionTerminationFailedForTenant` covers only a tenant whose context cannot be
   created.
5. **Until the first stable release, a stored payload has one shape.** A grant payload's members are added (required
   when the serving code always writes them), renamed, retyped or removed at the payload's current schema version,
   with no `[PersistedPayloadVersion]` bump and no upcaster. A stored record that lacks what the serving code writes is
   malformed and fails closed (decision 1). From the first stable release, the additive-only contract of ADR-1113 and
   the version-and-upcaster step govern every change to the released shape, and
   [#535](https://github.com/NCodeGroup/Identity/issues/535) retires this decision then.

## Options considered

- **Keep the readers until the first stable release.** Rejected: they read records the server does not write, so they
  are untestable against real data, and the first release would carry them as if a released store needed them.
- **Read a session without a bound as ended** (non-nullable members that deserialize to their defaults). Rejected: it
  gives a malformed record a meaning, as the fallback of ADR-1220 decision 2 does, and it leaves the session's type admitting a
  session without bounds, unlike its other always-written members.
- **Fail closed on a record that lacks what the serving code writes (chosen).** Every reader has one shape to honor,
  and a corrupt record surfaces as an error instead of a silently reinterpreted one.

## Consequences

- A grant payload without the envelope, and a session payload without `idleTimeout` or `absoluteExpiresWhen`, throws
  a `JsonException` when it is read, as a corrupt document or a missing upcaster step does
  ([ADR-1113](1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md)), and a session grant without an
  expiration throws an `InvalidOperationException`. The exception fails the request that reads the record: an
  authorize request that validates the session, a sign-in whose concurrent-session cap lists the subject's sessions,
  and the subject's session listings at `/account/sessions/list` and in the management API. A listing does not skip a
  malformed session, because skipping hides a record the store holds behind a listing that looks complete. A payload
  that does not authenticate is a different case: the grant protector logs it and reads the grant as absent
  ([ADR-1265](1265-grant-payloads-are-protected-under-the-tenant-master-key.md)).
- The background pass does not select a session grant without an expiration; the server adds every session grant with
  a lifetime, so the pass reaches every session the server creates.
- The background pass skips a malformed session where a request fails closed. A request has a caller that the failure
  reaches, and serving the record instead would admit it; the pass has no caller, and stopping at one malformed session
  would leave every later ended session, and the tokens issued under it, active at every pass. The pass logs the
  skipped session with its tenant and identifier at each pass until the record is removed, and it never serves or
  reinterprets the record.
- Before the first stable release, a change to a payload's shape is one edit of its type, with no upcaster, no version
  bump and no compatibility test. The rule in
  [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) states both
  periods.
- The project template keeps ADR-0108's bullet for a project that adopts the envelope over documents it already stored;
  the family lists its override in [`AGENTS.md`](../../AGENTS.md).

## References

- [project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md)
- [ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)
- [ADR-1113](1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md)
- [ADR-1220](1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md)
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)
- [ADR-1265](1265-grant-payloads-are-protected-under-the-tenant-master-key.md)
- [ADR-1393](1393-the-pre-release-product-is-described-as-it-is-and-its-changes-are-not-tracked.md)
- [`DefaultPersistedPayloadSerializer`](../../src/NCode.Identity.OpenId.Authentication/Persistence/DefaultPersistedPayloadSerializer.cs)
- [`SessionGrant`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Sessions/SessionGrant.cs)
- [`DefaultSessionService`](../../src/NCode.Identity.OpenId.Authentication/Sessions/DefaultSessionService.cs)
- [`EndedSessionTerminationService`](../../src/NCode.Identity.OpenId.Authentication/Sessions/EndedSessionTerminationService.cs)
- [`GrantStore`](../../src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/GrantStore.cs)
