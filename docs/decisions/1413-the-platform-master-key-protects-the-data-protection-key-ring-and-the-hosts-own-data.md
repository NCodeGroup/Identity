# 1413. The platform master key protects the Data Protection key ring and the host's own data

- **Status:** Accepted (amends [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md) and
  [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md);
  amended by
  [ADR-1423](1423-developer-key-pruning-starts-the-platform-over-when-its-master-keys-cannot-be-unwrapped.md): with
  developer key pruning a platform master key that cannot be unwrapped is replaced rather than stopping the host)
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)

## Context

Stored secrets are protected by a key hierarchy under an external root key: each owner of secrets has a master key that
the root key wraps
([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). The Data Protection
key ring that a host persists is protected by a master key too, and a host whose ring it cannot decrypt stops
([ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md)).

The deployment has one root, the platform
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)), which is
configuration and holds no stored secret. A team groups tenants and holds settings only. The question is which owners
have a master key and what each protects.

## Decision

**The platform and each tenant own a master key, and the platform's protects the Data Protection key ring and the
application data a host protects under the platform, and no stored secret of the family.**

1. **Master-key owners are the platform and each tenant.** The platform is `SecretOwner.Platform`, with the owner type
   `platform` and the owner id `platform`.
2. **The platform master key protects the key ring and host data.** It protects the Data Protection key ring and the
   application data a host protects under `SecretOwner.Platform`, and no stored secret of the family: the family's
   stored secrets are protected under the key hierarchy ADR-1235 describes.
3. **A team has no master key.** The platform and a team hold no stored secret, so only a tenant's secrets sit under a
   tenant master key, and a team owns nothing that needs one.
4. **The protector is loaded before the ring is judged.** `PlatformSecretProtectorWarmup` loads the platform's
   protector before `DataProtectionKeyRingGate` judges the ring, so a ring the platform key cannot decrypt stops the
   host as ADR-1247 records.
5. **The platform's master key rotates, and its retired versions are destroyed under ADR-1407's rules.** A new Data
   Protection key and the host data protected afterwards are protected under the new version. An existing ring key is
   never re-encrypted, because Data Protection never rewrites a key element
   (`DataProtectionKeyRingValueSource.RewrapAsync` rewraps nothing), so a retired version stays while a live ring key
   names it, and a ring that cannot be read names every version. The maintenance lease is
   `master-key-maintenance:platform`, and the key is reported and rekeyed at `GET /api/platform/encryption-keys` and
   `POST /api/platform/encryption-keys/rekey` with the `platform_encryption_keys` scope family, authorized at the
   platform node.

## Options considered

- **A team master key.** Nothing a team owns is a secret, so the key would protect nothing and add a row, a wrap and a
  rotation to every team. Rejected.
- **Keep secrets at the root.** The root is configuration, not a secret owner, so a secret stored there would have no
  owner to authorize it, list it or delete it. Rejected.

## Consequences

- Rotating the platform master key protects the ring keys and host data created afterwards under the new version and
  touches no tenant's secrets; a ring key written earlier keeps naming the retired version until Data Protection
  retires that key.
- A retired platform key version is destroyed only once no live ring key and no value that a registered
  protected-value source reports for the platform names it, and its grace has passed. A host that protects its own data
  under `SecretOwner.Platform` registers a source for that data, or the data becomes unreadable once the version is
  destroyed.
- A tenant's secrets never depend on the platform master key, so a tenant's data stays decryptable through the root key
  and its own master key alone.
- A team is created and deleted without any key material.
- The gate that judges the ring runs after the platform's protector is available, so a missing platform key stops the
  host before any request is served.

## References

- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform and the
  team
- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — key hierarchy under
  an external root key
- [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md) — a Data Protection key the host
  cannot decrypt stops the host
