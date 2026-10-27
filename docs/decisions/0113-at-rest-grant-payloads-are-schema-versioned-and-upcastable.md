# 113. At-rest grant payloads are schema-versioned, upcastable, and preserve unknown members

- **Status:** Accepted (amended by
  [ADR-0265](0265-grant-payloads-are-protected-under-the-tenant-master-key.md): the versioned document is stored
  protected)
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

Every persisted grant stores its domain payload as a JSON blob: `GrantEntity.PayloadJson` holds the serialized
`PersistedGrant<TPayload>.Payload` for authorization codes, refresh tokens, device/CIBA requests, pushed authorization
requests, DCR registration/initial-access tokens, the end-user `session` grant, continue envelopes, and requested-claims
pointers. `DefaultPersistedGrantService` serialized these with a bare `JsonSerializer.SerializeToElement` /
`JsonElement.Deserialize<TPayload>` round-trip against the environment's `JsonSerializerOptions`.

That round-trip is forward-read tolerant by luck of the defaults (unknown members are skipped; enums are snake_case
strings, not ordinals) but has no story for **deliberate schema evolution** of data already written to a store:

- There is no version marker on a stored document, so there is no place for a migration to hook — a non-additive change
  (renaming, removing, or retyping a member; removing an enum value) silently mis-reads or throws on old rows.
- Adding a **`required`** member is a latent breaking change: `System.Text.Json` enforces `required` on deserialize, so
  every previously-written row without that member throws `JsonException` on read.
- During a mixed-version deployment, an older node that reads a payload written by a newer node and then rewrites it
  (the in-place `UpdatePayloadAsync` path used by `session`, device, and CIBA grants) **silently drops** the members it
  did not recognize, because nothing captures them.

The family is greenfield and pre-release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), so there is no
shipped data to migrate today — but the at-rest format is exactly the kind of decision that is cheap to get right now
and expensive to retrofit once real stores exist. The valuable, non-speculative move is to stamp a version into the data
from day one and establish the single seam where migration will live.

## Decision

**Persisted-grant payloads are written through a single versioned serialization seam: a self-describing,
schema-versioned envelope with an upcasting pipeline for non-additive change, and an unknown-member capture bag on the
long-lived, in-place-mutated payloads.** Concretely:

- **One seam.** `IPersistedPayloadSerializer` (`NCode.Identity.OpenId.Authentication.Persistence`) is the only place
  `DefaultPersistedGrantService` (de)serializes a payload. It is `TryAddSingleton`-registered and DI-overridable.
- **Self-describing versioned envelope.** A payload is stored as `{ "$ver": N, "$payload": { … } }`. The `$` sentinel
  reserves envelope keys; payload members must never begin with `$`. A document lacking `$ver` is read as schema
  version `1`, so any pre-envelope blob still reads.
- **Current version is declared, defaulting to 1.** `[PersistedPayloadVersion(N)]` on a payload type declares its
  current at-rest schema version; absence means version `1`. The version is stamped on write.
- **Upcasting pipeline for non-additive change.** `IPersistedPayloadUpcaster` is a single-step (`FromVersion` →
  `FromVersion + 1`) migration registered as an enumerable DI service. On read, the serializer chains the upcasters
  from the stored version up to the current version before deserializing; a missing step throws rather than silently
  mis-reading. A stored version _newer_ than the current version (a rollback) is read forward-compatibly (unknown
  members skipped), so no down-casting is attempted.
- **Upcasters transform the mutable DOM.** Upcasting is a transformation, so it operates on `System.Text.Json.Nodes`
  (`JsonNode`), the mutable DOM built for in-place edits — immutable `JsonElement` is used only at the serializer's
  storage boundary. The node is freshly parsed and owned per read and never escapes the seam.
- **Unknown-member capture on mutable long-lived payloads.** `SessionGrant`, `CibaGrant`, and `DeviceCodeGrant` — the
  payloads that are rewritten in place and live long enough to span a deployment — carry a `[JsonExtensionData]` bag so
  a newer node's members survive a read-modify-write by an older node instead of being dropped.
- **The additive-only contract.** Payload DTOs evolve additively by default: a new member is optional (nullable or
  defaulted), **never `required`**; enums stay string-valued; a member is never renamed, removed, or retyped in place.
  A non-additive change bumps `[PersistedPayloadVersion]` and ships an upcaster for the new step.

## Scope

- **In scope:** the `PersistedGrant<TPayload>` payload path, which carries the complex, evolving at-rest documents.
- **Out of scope by design:** primitive scope-list columns (`ConsentEntity.RequestedScopesJson`,
  `ClientGrantEntity.ScopesJson`) are a bare `List<string>` with no schema to evolve; client/resource-server
  `SettingsJson` deserializes through `ISettingSerializer`, where every setting is typed by its registered descriptor and
  retiring a setting is a data migration ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)); and the opaque
  `JsonElement` metadata bags
  (`FederatedPrincipalEntity`, `LocalAccount` profile/system metadata) are stored and returned verbatim. If any of these
  later needs ordered migration, the serializer can be promoted to a shared location and reused.

## Options considered

- **Keep the bare `JsonSerializer` round-trip.** Rejected: no version marker means no migration hook, a new `required`
  member silently breaks old rows, and mixed-version rewrites drop unknown members.
- **A per-column `PayloadSchemaVersion` on `GrantEntity`.** Rejected: it couples the version to the relational schema
  (a migration to add it, and a second one per store that adopts the pattern) and does not travel with the JSON if the
  document is copied elsewhere. A self-describing envelope carries its own version and needs no storage migration.
- **Embed the version as a member of each payload DTO.** Rejected: it pollutes every domain record with a transport
  concern and makes the version subject to the same deserialization it is meant to govern.
- **Rely solely on `[JsonExtensionData]` round-tripping, no versioning.** Rejected: extension data preserves _unknown_
  members but cannot rename, remove, retype, or restructure _known_ ones — it is a complement to upcasting, not a
  substitute.
- **Self-describing envelope + upcaster pipeline + extension-data capture (chosen).** Stamps a version from day one,
  localizes all future migration to one seam, needs no storage migration, and closes the mixed-version-rewrite gap.

## Consequences

- The at-rest shape of grant payloads changes to the `{ "$ver", "$payload" }` envelope. Under the pre-release posture
  this requires a dev-store reset; there is no shipped data to migrate.
- Adding an optional member to a payload needs no version bump; the envelope reads old rows unchanged. A non-additive
  change is now a deliberate, testable, reviewable act: bump `[PersistedPayloadVersion]` and add one `FromVersion` →
  `FromVersion + 1` upcaster.
- There are **zero** upcasters today — the pipeline is dormant until the first non-additive change. The value realized
  now is the version stamp in the data and the single seam; adding the first migration later is a localized change, not
  a refactor.
- `DefaultPersistedGrantService` no longer null-checks a deserialized payload: the serializer throws a descriptive
  `JsonException` on a corrupt document or a missing upcaster step.

## References

- [ADR-0025: Pre-release posture and Auth0-parity-plus](0025-pre-release-posture-and-auth0-parity-plus.md)
- [ADR-0059: Device authorization grant](0059-device-authorization-grant.md) — the `UpdatePayloadAsync` in-place
  mutation path
- [ADR-0074: Session management and logout](0074-session-management-and-logout.md) — the long-lived `session` grant
- [System.Text.Json: required members](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/required-properties)
- [System.Text.Json: handle overflow JSON (`JsonExtensionData`)](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/handle-overflow)
