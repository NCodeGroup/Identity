# 21. Developer signing keys are seeded through the persistence layer, not injected at the read path

- **Status:** Accepted (the single-source-of-truth policy stands; the developer key's realization moved from the
  read-path `DeveloperSigningKeyOpenIdTenantFactory` to a seed handler by
  [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md))
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

[ADR-0002](0002-ephemeral-development-keys.md) established that a runnable-out-of-the-box signing key is an explicit,
development-only opt-in, never a silent default. Its implementation, `AddEphemeralDeveloperKeys()`, overrides
`GetTenantSecretsAsync` on the tenant factory to return a **static, in-memory** RSA key **instead of loading persisted
secrets**.

That read-path override forks the system into two sources of truth. The runtime tenant `SecretsProvider` (which the
JWKS endpoint publishes and `DefaultTokenService` signs with) sees the in-memory key, but the persistence layer never
does: the tenant secret store stays empty, so the management API (`GET /api/tenants/{id}/secrets`) shows nothing. A key
that the server is actively using is invisible to the API that is supposed to manage keys. The key also regenerates on
every materialization, so it cannot survive a restart — previously issued tokens stop validating.

For local development this is doubly awkward: it both diverges runtime behavior from the backend/management view and
loses the key across restarts.

## Decision

**A development signing key is seeded through the normal persistence path and read back through the production code
path, so the runtime, JWKS, and the management API share one source of truth. Ephemeral in-memory keys remain, but only
as the hermetic path for unit and integration tests.**

Two explicit, development-only opt-ins:

- **`AddEphemeralDeveloperKeys()` (tests).** Unchanged in spirit: replaces the tenant factory with one that supplies an
  in-memory RSA key, with no database or filesystem footprint. This is the right default for unit and integration
  tests, which want a hermetic, dependency-free, regenerate-per-run environment. Its read-path override is acceptable
  precisely because tests do not assert runtime-vs-management consistency.
- **`AddDeveloperSigningKey(keyRingDirectory)` (local running).** Replaces the tenant factory with
  `DeveloperSigningKeyOpenIdTenantFactory`, which **ensures a persisted RSA signing key exists** — generating one via
  `ISecretGenerator` and storing it with `ITenantStore.AddSecretAsync` when absent — then delegates to the unmodified
  `DefaultOpenIdTenantFactory.GetTenantSecretsAsync`. The key is therefore loaded from the store exactly as production
  loads it; JWKS and the management API observe the identical key.
    - **Persistence.** Paired with a local SQLite database file and an explicit, persistent Data Protection key ring
      (`PersistKeysToFileSystem`, DPAPI-protected on Windows), the seeded secret survives restarts — the runtime keeps a
      stable signing key, so tokens issued before a restart still validate.
    - **Self-healing.** If the persisted secret cannot be unprotected (for example, the key ring was deleted), it is
      treated as absent and regenerated, so the server stays runnable. Deleting the SQLite file, the key ring, or both
      always recovers on restart.
- **Opt-in selection is out of band of shared configuration.** The persistent mode is selected by
  `DeveloperKeys:Mode=PersistentSigningKey`, set in the Playground's `launchSettings.json` (which test hosts do not
  load), so the test default remains ephemeral with no special-casing in the test host.

The ADR-0002 decision is unchanged: keys are still an explicit opt-in, never created by the default registrations. This
ADR only changes **how** the developer key is realized — from intercepting the read to seeding the source.

## Options considered

- **Keep the read-path override and additionally expose the in-memory key through the management API.** Rejected: it
  teaches the management/persistence layer about a non-persisted runtime-only source — more code, two read paths, and
  the very divergence this ADR removes.
- **Seed only at tenant creation (not on materialization).** Rejected as the sole mechanism: it covers "delete the
  database" (the tenant is recreated and reseeded) but not "delete the key ring" (the tenant row survives with an
  undecryptable secret). Ensuring the key on materialization is self-healing for every deletion combination.
- **Make persisted-secret protection ephemeral for dev too.** Rejected: an ephemeral protector cannot unprotect a
  persisted secret after restart, which defeats the persistence goal. A persistent, DPAPI-where-available key ring is
  what lets the seeded secret round-trip.
- **Fold persistence into `AddEphemeralDeveloperKeys()`.** Rejected: tests require the opposite properties — zero
  machine footprint and regenerate-per-run. Persistence and hermeticity are different needs and get different opt-ins.

## Consequences

- The developer signing key is a single source of truth: the runtime, JWKS, and the management API agree, and the key
  is manageable through the same endpoints as any other secret.
- Local development keeps a stable key across restarts; the SQLite file and key ring live under a git-ignored
  `App_Data` directory and can be deleted (individually or together) to reset, always recovering on the next start.
- Test hosts are unaffected: they keep the ephemeral, hermetic path with no database or filesystem writes.
- On Linux/macOS the key ring is stored unprotected at rest by default (a development-only key), which ASP.NET Core
  Data Protection surfaces as a warning; production must configure a real protector and a stable, securely-managed key.

## References

- [ADR-0002](0002-ephemeral-development-keys.md) — the explicit-opt-in decision this ADR implements differently (and
  does not supersede).
- [ADR-0011](0011-secret-management-api.md) — server-side secret generation reused to seed the dev key.
- [ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md) — tenant-owned families (the tenant and its
  secrets) are not query-scoped, so seeding and reading the tenant secret is unaffected by the ambient tenant filter.
- Code: `NCode.Identity.Server.DeveloperSigningKeyOpenIdTenantFactory`,
  `NCode.Identity.Server.DeveloperKeysRegistration.AddDeveloperSigningKey`,
  `NCode.Identity.OpenId.Playground.Program` (mode selection + SQLite provider in the EXE host).
