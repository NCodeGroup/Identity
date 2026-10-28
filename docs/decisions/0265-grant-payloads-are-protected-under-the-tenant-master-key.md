# 265. Grant payloads are protected under the tenant master key

- **Status:** Accepted (amends [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)
  and [ADR-0113](0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md))
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

Every persisted grant — authorization codes, refresh tokens, sessions, device, CIBA and pushed authorization requests,
DCR tokens and continue envelopes — stored its payload as plaintext JSON in `Grants.PayloadJson`. A database dump or
backup therefore exposed issued tokens, personal data and request parameters, even though
[ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) protects every stored
secret under a key hierarchy. That ADR covers persisted secrets and the Data Protection key ring only, and lists
grant-payload encryption as a follow-up ([#175](https://github.com/NCodeGroup/Identity/issues/175)). Nothing between
`IPersistedPayloadSerializer` and the store encrypted the payload.

## Decision

A grant's payload is stored only as an `nce1.` envelope under the master key of the grant's tenant.

1. **Protection.** `DefaultGrantPayloadProtector` protects through `ISecretProtectorProvider` and `ISecretProtector` with
   owner `SecretOwner.ForTenant(grant.TenantId)` and context `SecretProtectionContext.ForGrantPayload(hashedKey)`:
   purpose `grant-payload`, item id the grant's hashed key. The hashed key binds the envelope to its lookup row, so a
   payload copied to another grant row does not decrypt.
2. **One internal seam.** `IGrantPayloadProtector` layers on top of `IPersistedPayloadSerializer`. The serializer keeps
   schema versioning ([ADR-0113](0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md)); the protector
   encrypts the versioned document. Both readers of grant payloads, `DefaultPersistedGrantService` and
   `DefaultSessionService.GetSessionsAsync`, use it.
3. **Buffers.** Plaintext is written to and read from sensitive pooled buffers
   (`BufferFactory.CreatePooledBufferWriter(isSensitive: true)`) and never becomes a `string`
   ([ADR-0116](0116-memory-allocations-and-buffer-operations.md)); only the envelope is a `string`.
4. **Unreadable means absent.** A payload that fails to unprotect (a `CryptographicException` from `GetProtectorAsync`
   or `Unprotect`) makes the grant absent: `null`, `false` or skipped, with a Warning log event
   `GrantPayloadUnreadable`. This fails closed: an altered or foreign row never authorizes anything. In development, it
   also lets a tenant whose master keys `DeveloperSecretPruningSeedHandler` replaced keep working: its users sign in
   again instead of getting a 500. `ConsumeOnceOrDefault` checks the payload before it marks a grant consumed.
5. **Contract.** `PersistedGrant.PayloadJson` (`JsonElement`) becomes `ProtectedPayload` (`string`), and the same on
   `GrantEntity`. A clean cutover ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)): the
   `ProtectGrantPayloads` `[ContractMigration]` deletes every existing grant row, all short-lived, so users
   re-authenticate, and replaces the column.

## Options considered

- **Encrypt in the EF store.** The store knows no tenant keys, and acquiring them is asynchronous and tenant-aware.
  Rejected.
- **Encrypt inside `IPersistedPayloadSerializer`.** The serializer is synchronous, and schema versioning is a separate
  concern from encryption. Rejected.
- **Item id = `GrantId`.** Does not bind the envelope to the lookup key a reader holds. Rejected for the hashed key.
- **Keep legacy plaintext rows readable.** Needs a plaintext fallback that would also accept a forged plaintext row.
  Rejected: the family is pre-release, and a clean cutover is preferred.

## Consequences

- A database dump or backup no longer exposes any grant payload without the root key. Closes
  [#175](https://github.com/NCodeGroup/Identity/issues/175).
- Deleting a tenant deletes its master keys, so its grant payloads are unreadable in the live database too
  ([ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
- Reading or writing a grant acquires a protector, which may reach the key management service the first time a tenant's
  master key loads.
- Breaking: the `ProtectGrantPayloads` migration deletes every stored grant, so users sign in again.
- A grant whose master key was lost or replaced reads as absent and logs a warning rather than failing the request.

## References

- [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy and
  `nce1.` envelope.
- [ADR-0113](0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md) — the versioned payload document.
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — sensitive buffers.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — clean cutover.
