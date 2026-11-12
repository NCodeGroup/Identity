# 1235. Stored secrets and grant payloads are protected by a key hierarchy under an external root key

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0127](project-template/0127-the-data-protection-key-ring-is-shared-and-encrypted-at-rest.md)

## Context

The server stores private and symmetric key material in its own database: tenant and platform signing and encryption
keys, client secrets and resource-server keys. ASP.NET Core Data Protection keeps its key ring in the same database, and
every persisted grant (authorization codes, refresh tokens, sessions, device, CIBA and pushed authorization requests,
DCR tokens and continue envelopes) keeps a payload that carries issued tokens, personal data and request parameters.
Protecting all of it with a key ring that sits beside it only moves the problem: whoever holds a database dump or backup
holds the keys. A certificate-wrapped key ring puts one long-lived, hand-rotated secret outside the database, gives every
tenant the same keys, and relies on an API that Microsoft documents as not primarily intended for indefinite
persistence. The project template decides that the Data Protection key ring is encrypted at rest under a master key whose
root is outside the store
([project-template ADR-0127](project-template/0127-the-data-protection-key-ring-is-shared-and-encrypted-at-rest.md));
this record decides what that hierarchy is, and that it covers grant payloads as well as secrets.

A secrets-manager service would duplicate the store the server already has. What the server needs from outside is a root
of trust: a key that never leaves a key management service or HSM, is used rarely, and is reached through credentials
that are themselves short-lived.

## Decision

Every stored private or symmetric value, and every persisted grant's payload, is encrypted under a key hierarchy whose
root lives outside the database.

1. **Root key.** One per deployment, held by the key wrapper that `SecretProtection:RootKeyProvider` names: `ephemeral`,
   `local`, `aws-kms`, `azure-key-vault`, `google-cloud-kms` or any wrapper a package registers
   ([ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md)). No provider is a default, and a
   host always names one
   ([ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)).
   The root key only wraps master keys, binding the owner and key id as associated data (the KMS encryption context)
   where the service supports it.
2. **Master keys.** One 256-bit, versioned key per owner, where the owners are the platform (`SecretOwner.Platform`) and
   each tenant (`SecretOwner.ForTenant`). Clients and resource servers use their tenant's key. The platform's key
   protects only the Data Protection key ring and the host's own data
   ([ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)).
   Each master key is stored wrapped in `MasterKeys`, bound to its owner and version by an HMAC key-check value, and,
   when configured, wrapped a second time under an offline recovery RSA public key. A partial unique index on an owner's
   unretired keys keeps exactly one active key per owner, and reads fetch the active key plus only the retired keys an
   envelope names.
3. **Purpose keys** are derived from a master key with HKDF-SHA256 per purpose, named by `SecretProtectionPurposes`:
   `persisted-secret`, `grant-payload` and `data-protection-key-ring`. They are never stored.
4. **Envelopes.** Each protect call draws a fresh 256-bit data key, encrypts the value with AES-256-GCM, and wraps the
   data key with the purpose key into an `nce1.` envelope. Associated data binds every envelope to its owner, purpose
   and item id, so a value moved to another row, tenant or purpose does not decrypt. Owner type, owner id and item id are
   lower-cased with `ToLowerInvariant`, as the stores normalize them; purposes compare ordinally.
5. **Unprotected values.** Public material (`spki`, `x509`) and remote references are stored unencrypted
   (`SecretEncodings.IsUnprotected`). Every other encoding, verifiers included, is an envelope.
6. **Acquisition is asynchronous, cryptography synchronous.** `ISecretProtectorProvider.GetProtectorAsync` loads or
   creates an owner's keys, which is the only step that may call the key management service, and returns a synchronous
   `ISecretProtector`. A caller acquires one only after the request is authorized and validated, and for a secret that
   exists, so a request naming an unknown owner never creates a master key.
7. **Grant payloads.** A grant's payload is stored only as an envelope under the master key of the grant's tenant.
   `DefaultGrantPayloadProtector`, the implementation of the internal `IGrantPayloadProtector`, protects through
   `ISecretProtectorProvider` with owner `SecretOwner.ForTenant(grant.TenantId)` and context
   `SecretProtectionContext.ForGrantPayload(hashedKey)`: purpose `grant-payload`, item id the grant's hashed key, so a
   payload copied to another grant row does not decrypt. It layers over `IPersistedPayloadSerializer`, which keeps the
   schema versioning
   ([ADR-1395](1395-grant-payloads-have-one-versioned-serializer-and-a-stored-record-missing-written-members-fails-closed.md));
   the protector encrypts the versioned document. `PersistedGrant.ProtectedPayload` and `GrantEntity` hold the
   envelope, and both readers of grant payloads, `DefaultPersistedGrantService` and
   `DefaultSessionService.GetSessionsAsync`, go through the protector.
8. **Unreadable means absent.** A payload that fails to unprotect (a `CryptographicException` from `GetProtectorAsync`
   or `Unprotect`) makes the grant absent (`null`, `false` or skipped) and logs the Warning event
   `GrantPayloadUnreadable`. This fails closed: an altered or foreign row never authorizes anything.
   `ConsumeOnceOrDefault` checks the payload before it marks a grant consumed.
9. **Production** refuses the `ephemeral` provider, an inline local key under any provider (an unused root key in
   configuration is still a leaked key), and a missing decision about recovery, reporting them together with every other
   production-safety finding.
10. **Deletion.** Deleting a tenant deletes its master keys, so its secrets and grant payloads are unreadable in the live
    database. A backup that holds the wrapped master key stays readable until it expires.
11. **Buffers** follow
    [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md):
    producers write into caller-supplied writers or spans with a sizing method beside them, transient buffers are
    rented, secrets live in pinned buffers zeroed on release, and grant plaintext is written to and read from sensitive
    pooled writers. A `string` is produced only for a non-secret stored value at a storage or wire boundary, such as the
    envelope in a text column, a wrapped key or a key-management encryption context, and is written once, in place, with
    `string.Create`.
12. **Public use.** Applications and sibling packages protect their own sensitive data through
    `ISecretProtectorProvider` and `ISecretProtector` with a namespaced purpose (`<vendor>/<name>`), inheriting the root
    key, tenant isolation, crypto-shredding and binding to owner, purpose and item id. The envelope layout, master keys,
    key derivation and wrappers stay internal, so they can evolve without breaking anyone.
13. **Caching.** Unwrapped master keys are cached per owner in pinned memory that is zeroed when the ring is disposed.
    A tenant change evicts its master keys on every replica through the tenant change broadcast, a replica that may
    have missed a broadcast evicts every owner's, and a load that an eviction overtakes is not cached. Evicting does not
    zero a key, because a protector handed out earlier may still be using it. Entries reload after `RefreshInterval`
    ([ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md)).

## Options considered

- **A certificate-wrapped Data Protection ring.** One key for all tenants, long-lived and rotated by hand, it breaks
  silently when renewed in place and offers no tenant isolation or deletion. Rejected.
- **A secrets-manager service (Infisical, Doppler, cloud secret managers) as the store.** It duplicates the server's own
  store, bills per identity or per secret, and from a cluster outside the vendor's cloud usually needs a stored
  credential. Rejected as the store; any of their key management services can be a root key provider.
- **Signing and decrypting every key inside the key management service by default.** It is the strongest posture against
  a compromised replica, but adds a network round trip and a fee to every token. It stays an opt-in for a tenant's
  signing keys
  ([ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)).
- **A server key above the tenant keys.** It adds no protection, because every replica holds every tenant's keys, and it
  would stop a tenant from bringing its own root. Rejected.
- **Stored per-purpose keys.** They allow rotating one purpose alone, but rotating a master key already rewraps only
  32-byte data keys, so the extra stored state is not worth it. Rejected.
- **Encrypting grant payloads in the EF store.** The store knows no tenant keys, and acquiring them is asynchronous and
  tenant-aware. Rejected.
- **Encrypting grant payloads inside `IPersistedPayloadSerializer`.** The serializer is synchronous, and schema
  versioning is a separate concern from encryption. Rejected.
- **`GrantId` as the grant envelope's item id.** It does not bind the envelope to the lookup key a reader holds.
  Rejected for the hashed key.
- **A plaintext fallback for grant payloads.** It would also accept a forged plaintext row. Rejected.
- **Envelopes under a root-wrapped master key per owner, with grant payloads under the tenant's key.** Chosen.

## Consequences

- A database dump or backup exposes no secret and no grant payload without the root key, and key management service audit
  logs record every unwrap.
- The vendor choice shrinks to keyless authentication, region, deletion protection and cost. AWS web identity
  federation needs a publicly discoverable service-account issuer; a cluster without one uses the `local` provider.
- The Helm chart mounts AWS web identity credentials only in a pod that selects `aws-kms`, and refuses a malformed key
  ARN before the release renders.
- Losing the root key without a recovery key loses every stored secret and grant payload; Production makes operators
  decide.
- A compromised replica still exposes the keys it has loaded; remote signing is the answer to that.
- Reading or writing a grant acquires a protector, which may reach the key management service the first time a tenant's
  master key loads.
- A grant whose master key was lost or replaced reads as absent and logs a warning rather than failing the request.
- Tests cover the envelope binding to owner, purpose and item id, the case-insensitive binding, the partial unique index,
  unreadable-payload handling and the production-safety findings.

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies (root key providers, owner
  types, purposes).
- [ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md) — persisted
  tenant keys.
- [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) — no long-lived
  credentials; recoverable, off-vendor backups.
- [ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md) — the root key providers.
- [ADR-1395](1395-grant-payloads-have-one-versioned-serializer-and-a-stored-record-missing-written-members-fails-closed.md)
  — the versioned payload document.
- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) —
  spans, pooled writers and secure lifetimes for every buffer.
- Auth0, "Customer Managed Keys" (envelope hierarchy: environment root key, tenant master key, namespace key, data key).
- Microsoft, "ASP.NET Core Data Protection overview" (not intended for indefinite persistence).
