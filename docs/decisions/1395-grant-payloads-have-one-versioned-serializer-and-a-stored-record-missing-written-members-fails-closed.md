# 1395. Grant payloads have one versioned serializer, and a stored record missing written members fails closed

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md)

## Context

[project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md) writes a persisted
JSON document through one versioned seam: a self-describing envelope, step-by-step upcasters, unknown members kept
across a rewrite, and additive change by default. This record applies it to grant payloads and adds the family's rule
for every stored record.

Every persisted grant stores its domain payload as a JSON blob: `GrantEntity.PayloadJson` holds the serialized
`PersistedGrant<TPayload>.Payload` of authorization codes, refresh tokens, device and CIBA requests, pushed
authorization requests, registration and initial-access tokens, the end-user session grant, continue envelopes and
requested-claims pointers. A bare serialize and deserialize round-trip skips unknown members, but it has no story for
deliberate schema evolution, and an older node that reads a payload a newer node wrote and then rewrites it (the
in-place `UpdatePayloadAsync` path of session, device and CIBA grants) would drop the members it did not recognize.

The family is greenfield and pre-release ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)):
no released package, deployed database or consumer holds a stored record, and every record a store holds is one that
the serving code wrote. A reader that accepts a record lacking what the serving code writes gives that record a meaning
the serving code never gives it:

- ADR-0108 reads a document without `$ver` as version 1, for a project that adopts the envelope over documents it
  already stored. The family's grant payloads carry the envelope from their first stored document:
  `IPersistedPayloadSerializer.Serialize` writes it, and the protected payload of every grant is an `nce1.` envelope
  over that output. A document without `$ver` is corrupt or foreign, and reading it as version 1 would deserialize a
  document of unknown origin as a grant.
- `ISessionService.CreateAsync` stamps both lifetime bounds on every session, and the create request requires them, so
  a session without `IdleTimeout` or `AbsoluteExpiresWhen`, or a session grant without an expiration, describes a
  session the server does not create.
- An additive-only contract keeps every older stored document readable. Before the first stable release no older
  document is one a reader must keep: holding a payload member optional, or shipping an upcaster for a shape no release
  wrote, carries an unreleased state into the first release's code.

## Decision

1. **A stored record that lacks what the serving code writes is malformed, and it fails closed.** A reader honors only
   the shape the serving code writes; it never supplies a default, a fallback or an alternative reading for a record
   that lacks part of it.
2. **One serializer writes and reads every grant payload.** `IPersistedPayloadSerializer`
   (`NCode.Identity.OpenId.Authentication.Persistence`, default `DefaultPersistedPayloadSerializer`, registered with
   `TryAddSingleton` and replaceable) is the only code that writes and reads a `PersistedGrant<TPayload>.Payload`.
   `IGrantPayloadProtector` protects its output under the tenant master key
   ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). A payload that does
   not authenticate under the protector is logged and read as absent, unlike a malformed one.
3. **A payload is a schema-versioned envelope, or it is rejected.** A payload is stored as
   `{ "$ver": N, "$payload": { ... } }` from its first stored document. `Deserialize` throws a `JsonException` for any
   document that is not an object with an integer `$ver` and a `$payload`, before and after the first stable release.
4. **A payload type declares its current version.** `[PersistedPayloadVersion(N)]` declares it (default 1), and the
   serializer stamps it on write.
5. **Upcasters chain on read.** `IPersistedPayloadUpcaster` steps (`PayloadType`, `FromVersion` to `FromVersion + 1`)
   are registered as an enumerable and chained on read over the `JsonNode` DOM. A missing step throws, two upcasters
   for one step throw `InvalidOperationException` at construction, and a stored version newer than the current one is
   read forward-compatibly.
6. **Long-lived payloads keep unknown members.** `SessionGrant`, `CibaGrant` and `DeviceCodeGrant`, the payloads changed
   in place, carry `[JsonExtensionData]`, so a read-modify-write keeps members it does not know.
7. **A session always carries its bounds.** A session payload without `IdleTimeout` or `AbsoluteExpiresWhen` does not
   deserialize, and a session grant stored without an expiration throws `InvalidOperationException` when a request
   reads it ([ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md)).
8. **A malformed record fails the request that reads it.** An authorize request that validates the session, a sign-in
   that lists sessions for the concurrent-session cap, and the listings at `/account/sessions/list` and in the
   management API fail; a listing never skips a malformed record.
9. **The background pass skips instead of stopping.** The ended-session pass has no caller, so it selects only grants
   whose expiry has passed and logs and skips a session it cannot terminate
   (`EndedSessionTerminationFailedForSession`, event 2071) rather than stopping
   ([ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)).
10. **Until the first stable release a payload has one shape.** A payload's members are added (required when always
    written), renamed, retyped or removed in place at the current version, with no version bump and no upcaster. From
    the first stable release, ADR-0108's additive contract and its version-and-upcaster step govern the released
    shapes.

Out of scope: primitive scope-list columns, settings documents (`ISettingSerializer`,
[ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md))
and opaque metadata bags.

## Options considered

- **A per-column schema version on `GrantEntity`.** Rejected: it couples the version to the relational schema and does
  not travel with the JSON when the document is copied elsewhere. A self-describing envelope carries its own version.
- **A version member inside each payload DTO.** Rejected: it pollutes every domain record with a transport concern and
  subjects the version to the deserialization it governs.
- **Keep readers for unwritten shapes until the first stable release.** Rejected: they read records the server does not
  write, so they are untestable against real data, and the first release would carry them as if a released store needed
  them.
- **Read a session without bounds as ended.** Rejected: it gives a malformed record a meaning and leaves the session's
  type admitting a session without bounds, unlike its other always-written members.
- **One envelope serializer that fails closed (chosen).** Every reader has one shape to honor, migration localizes to
  one seam, and a corrupt record surfaces as an error instead of a silently reinterpreted one.

## Consequences

- A grant payload without the envelope, and a session payload without its bounds, throws a `JsonException` when read,
  as a corrupt document or a missing upcaster step does, and a session grant without an expiration throws an
  `InvalidOperationException`. The failure reaches the caller, because skipping a malformed session in a listing would
  hide a record the store holds behind a listing that looks complete.
- The background pass skips a malformed session where a request fails closed: a request has a caller the failure
  reaches, while stopping the pass at one malformed session would leave every later ended session, and the tokens
  issued under it, active. The pass logs the skipped session with its tenant and identifier at each pass until the
  record is removed, and it never serves or reinterprets the record.
- The background pass does not select a session grant without an expiration; the server adds every session grant with a
  lifetime, so the pass reaches every session the server creates.
- Adding an optional member after the first stable release needs no version bump; a non-additive change is a deliberate,
  testable act: bump `[PersistedPayloadVersion]` and add one upcaster. Before it, a change to a payload's shape is one
  edit of its type. The rule in
  [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) states both
  periods.
- The serializer throws a descriptive `JsonException` on a corrupt document or a missing upcaster step, so
  `DefaultPersistedGrantService` does not null-check a deserialized payload.

## References

- [project-template ADR-0108](project-template/0108-persisted-documents-are-versioned-and-additive.md)
- [project-template ADR-0015](project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)
- [ADR-1220](1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md)
- [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md)
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)
- [`DefaultPersistedPayloadSerializer`](../../src/NCode.Identity.OpenId.Authentication/Persistence/DefaultPersistedPayloadSerializer.cs)
- [`SessionGrant`](../../src/NCode.Identity.OpenId.Authentication.Abstractions/Sessions/SessionGrant.cs)
- [`DefaultSessionService`](../../src/NCode.Identity.OpenId.Authentication/Sessions/DefaultSessionService.cs)
- [`EndedSessionTerminationService`](../../src/NCode.Identity.OpenId.Authentication/Sessions/EndedSessionTerminationService.cs)
- [`GrantStore`](../../src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/GrantStore.cs)
