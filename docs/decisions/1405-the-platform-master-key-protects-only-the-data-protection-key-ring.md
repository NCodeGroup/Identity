# 1405. The platform master key protects only the Data Protection key ring

- **Status:** Accepted (amends [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)
  and [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md))
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)

## Context

Stored secrets are protected by a key hierarchy under an external root key: each owner of secrets has a master key that
the root key wraps
([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). The Data Protection
key ring that a host persists is protected by a master key too, and a host whose ring it cannot decrypt stops
([ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md)).

The deployment has one root, the platform
([ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)), which is
configuration and holds no stored secret. A team groups tenants and holds settings only. The question is which owners
have a master key and what each protects.

## Decision

**The platform and each tenant own a master key, and the platform's protects only the Data Protection key ring and the
application data a host protects under the platform.**

1. **Master-key owners are the platform and each tenant.** The platform is `SecretOwner.Platform`, with the owner type
   `platform` and the owner id `platform`.
2. **The platform master key protects the key ring and host data.** It protects the Data Protection key ring and the
   application data a host protects under `SecretOwner.Platform`, and nothing else.
3. **A team has no master key.** The platform and a team hold no stored secret, so only a tenant's secrets sit under a
   tenant master key, and a team owns nothing that needs one.
4. **The protector is loaded before the ring is judged.** `PlatformSecretProtectorWarmup` loads the platform's
   protector before `DataProtectionKeyRingGate` judges the ring, so a ring the platform key cannot decrypt stops the
   host as ADR-1247 records.

## Options considered

- **A team master key.** Nothing a team owns is a secret, so the key would protect nothing and add a row, a wrap and a
  rotation to every team. Rejected.
- **Keep secrets at the root.** The root is configuration, not a secret owner, so a secret stored there would have no
  owner to authorize it, list it or delete it. Rejected.

## Consequences

- Rotating the platform master key re-protects the key ring and host data and no tenant's secrets.
- A tenant's secrets never depend on the platform master key, so a tenant's data stays decryptable through the root key
  and its own master key alone.
- A team is created and deleted without any key material.
- The gate that judges the ring runs after the platform's protector is available, so a missing platform key stops the
  host before any request is served.

## References

- [ADR-1403](1403-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform and the
  team
- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — key hierarchy under
  an external root key
- [ADR-1247](1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md) — a Data Protection key the host
  cannot decrypt stops the host
