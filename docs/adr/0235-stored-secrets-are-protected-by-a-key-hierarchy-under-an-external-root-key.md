# 235. Stored secrets are protected by a key hierarchy under an external root key

- **Status:** Accepted (amended by
  [ADR-0247](0247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md): a live key ring key the server's
  master key cannot decrypt stops the host)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

The server stores private and symmetric key material — tenant and server signing and encryption keys, client secrets,
resource-server keys — in its own database, and ASP.NET Core Data Protection keeps its key ring in the same database.
Protecting both with a key ring that sits beside them only moves the problem: whoever holds a backup holds the keys. A
certificate-wrapped key ring ([ADR-0177](0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)) puts
one long-lived, hand-rotated secret outside the database, gives every tenant the same keys, and relies on an API that
Microsoft documents as not primarily intended for indefinite persistence.

A secrets-manager service would duplicate the store the server already has. What the server needs from outside is a root
of trust: a key that never leaves a key management service or HSM, used rarely, through credentials that are themselves
short-lived.

## Decision

Every stored private or symmetric value is encrypted under a key hierarchy whose root lives in an external key management
service.

1. **Root key.** One per deployment, held by a root key provider — an open vocabulary of `IRootKeyProvider` strategies
   selected by `SecretProtection:RootKeyProvider`: `ephemeral` (non-Production default), `local` (an AES key file), and
   `aws-kms` (package `NCode.Identity.Secrets.Persistence.AwsKms`); other services register their own. The provider
   only wraps and unwraps master keys, binding the owner and key id as associated data (the KMS encryption context)
   where the service supports it.
2. **Master keys.** One 256-bit key per owner — the server and each tenant, as peers — versioned, stored in `MasterKeys`
   wrapped by the root key, bound to its owner and version by an HMAC key-check value, and, when configured, wrapped a
   second time under an offline recovery RSA public key. Clients and resource servers use their tenant's key. Exactly
   one master key per owner is active — a partial unique index on the owner's unretired keys enforces it — and master
   keys are read through indexed store queries: the active key, plus only the retired keys an envelope names.
3. **Purpose keys** are derived from a master key with HKDF-SHA256 per purpose (`persisted-secret`,
   `data-protection-key-ring`, …); they are never stored.
4. **Records.** Each protect call draws a fresh 256-bit data key, encrypts the value with AES-256-GCM, and wraps the data
   key with the purpose key, producing an `nce1.` envelope. Associated data binds every envelope to its owner, purpose
   and item id, so a value moved to another row, tenant or purpose does not decrypt. Owner type, owner id and item id
   are case-insensitive in the binding — lower-cased with `ToLowerInvariant`, as the stores normalize them — and
   purposes compare ordinally.
5. **Public material** (`spki`, `x509`) is stored unencrypted; every other encoding must be an envelope.
6. **Data Protection.** The key ring stays in the database, each key encrypted under the server master key; certificate
   key-ring protection is removed.
7. **Acquisition is asynchronous, cryptography synchronous.** `ISecretProtectorProvider.GetProtectorAsync` loads (or
   creates) an owner's master keys — the only step that may reach the key management service — and the returned
   `ISecretProtector` works synchronously. A caller acquires a protector only when it will use one — after the request
   is authorized and validated, and for a secret that exists — so a request naming an unknown owner never creates a
   master key.
8. **Production** refuses the ephemeral provider, an inline local key (whatever provider is selected: an unused root
   key in configuration is still a leaked key), and a missing decision about recovery, reporting them together with
   every other production-safety finding.
9. **Deletion.** Deleting a tenant deletes its master keys, so its data in the live database is unreadable; backups
   still holding the wrapped master key stay readable until they expire.
10. **Buffers.** The protector and every primitive behind it follow
    [ADR-0116](0116-memory-allocations-and-buffer-operations.md): producers write into caller-supplied writers or
    spans with a sizing method beside them, transient buffers are rented, secrets live in pinned buffers zeroed on
    release, and sensitive text is never a `string`. A `string` is produced only for a non-secret value at a storage or
    wire boundary — the envelope in a text column, a wrapped key, a key-management encryption context — and is written
    once, in place, with `string.Create`.
11. **Public use.** Applications and sibling packages protect their own sensitive data through
    `ISecretProtectorProvider` and `ISecretProtector` with a namespaced purpose (`<vendor>/<name>`), inheriting the
    root key, tenant isolation, crypto-shredding and binding to owner, purpose and item id. The envelope layout, master
    keys, key derivation and root wrappers stay internal, so they can evolve (master key rotation) without breaking
    anyone.
12. **Development.** No root key is committed, not even a development one. The LocalDev AppHost generates one
    development root key per machine, outside the repository, and gives it to every OP instance that shares the
    development database; any other Development host uses `ephemeral`, and development pruning heals the tenants a
    changed root key left unreadable.
13. **Caching.** Unwrapped master keys are cached per owner for the life of the process, in pinned memory zeroed when
    the ring is disposed. Changing or deleting a tenant evicts its master keys on every replica through the tenant
    change broadcast; a replica that may have missed a broadcast evicts every owner's; and a load that an eviction
    overtakes is not cached. Evicting does not zero a key, because a protector handed out earlier may still be using
    it.

## Options considered

- **Keep the certificate-wrapped Data Protection ring.** One key for all tenants, long-lived and rotated by hand,
  breaks silently when renewed in place, and offers no tenant isolation or deletion. Rejected.
- **A secrets-manager service (Infisical, Doppler, cloud secret managers) as the store.** Duplicates the server's own
  store, bills per identity or per secret, and from a cluster outside the vendor's cloud usually needs a stored
  credential. Rejected as the store; any of their key management services can be a root key provider.
- **Sign and decrypt every key inside the key management service.** Strongest against a compromised replica, but adds a
  network round trip and a fee to every token. Kept as an opt-in for signing keys (backlog `S7`), not the default.
- **A server key above the tenant keys.** Adds no protection — every replica holds every tenant's keys — and would stop
  a tenant from bringing its own root later. Rejected.
- **Stored per-purpose ("namespace") keys.** Allows rotating one purpose alone; rotating a master key already rewraps
  only 32-byte data keys, so the extra stored state is not worth it. Rejected.
- **Google Cloud KMS as the built-in cloud provider.** Its workload identity federation accepts an uploaded JWKS, so a
  cluster with a private service-account issuer authenticates without a stored credential. AWS KMS was chosen instead
  because the platform's state key, backups, parameters and CI federation already live in one AWS account: the root key
  adds no vendor and is audited by the same CloudTrail. Google Cloud KMS remains a follow-up provider.

## Consequences

- A database dump or backup no longer exposes any secret without the root key; key management service audit logs record
  every unwrap.
- Stored data cuts over cleanly: nothing reads a value protected by the Data Protection key ring any more. A
  development database heals its tenants' keys by pruning and reprovisioning them; client secrets stored before this
  change must be reissued, or the database reset.
- The vendor choice shrinks to keyless authentication, region, deletion protection and cost. AWS web identity
  federation needs a publicly discoverable service-account issuer; a cluster without one uses the `local` provider
  until IAM Roles Anywhere is supported.
- The Helm chart mounts AWS web identity credentials only in a pod that selects `aws-kms`, and refuses a malformed key
  ARN before the release renders.
- Losing the root key without a recovery key loses every stored secret; Production makes operators decide.
- A compromised replica still exposes the keys it has loaded; remote signing (`S7`) and key rollover (`S8`) address that.
- Master key rotation, the rewrap and recovery tool, more providers, per-tenant root keys, client-secret hashing and
  grant-payload encryption are follow-ups (`S5`, `S13`–`S20`).
- Supersedes [ADR-0177](0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md).

## References

- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies (root key providers, owner types,
  purposes).
- [ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md) — persisted tenant keys.
- [ADR-0134](0134-cloud-native-operating-principles.md) — no long-lived credentials; recoverable, off-vendor backups.
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — spans, pooled writers and secure lifetimes for every
  buffer.
- Auth0, "Customer Managed Keys" (envelope hierarchy: environment root key → tenant master key → namespace key → data
  key).
- Microsoft, "ASP.NET Core Data Protection overview" (not intended for indefinite persistence).
