# 169. Every tenant is provisioned with persisted keys

- **Status:** Accepted (amended by [ADR-0212](0212-deleting-an-owner-removes-what-it-owns.md): deleting a tenant also
  removes the data of its end users; by
  [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md): the re-seed after a change
  of the server's settings is part of one reconciliation; by
  [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md): a key is unreadable
  when its tenant's master key, or the root key that wraps it, is lost or changed, no longer when the Data Protection
  key ring is)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A tenant signs with the keys in its persisted secrets, and the JSON Web Key Set publishes the same keys. Outside
development, nothing created such a key. [ADR-0002](0002-ephemeral-development-keys.md) rejected creating keys by
default for three reasons:

- keys regenerated on every restart invalidate every issued token;
- replicas would each mint their own keys;
- key provenance is a decision operators must own.

The development modes ([ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md)) are refused in
Production, so on a fresh production database no tenant had a key. The JSON Web Key Set was empty and nothing could be
signed. That included the bootstrap administrator's first management token
([ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md)), which is the only credential that could
import a key. The deployment was deadlocked before its first request.

The first two of ADR-0002's objections concern keys held in memory. A key generated once, persisted, and protected by
the shared Data Protection key ring survives restarts and is the same on every replica. The third objection is met when
generation is a configurable default that never overrides a key the tenant already holds — the model of Auth0 tenant
signing keys and Keycloak realm keys. Every tenant already passes through one seed pipeline under every strategy
([ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)), and that pipeline is where
tenant data is reserved.

## Decision

**When a tenant is seeded, it gets a persisted key for each configured algorithm its settings use and it holds no usable
key for. The server generates keys only to fill those gaps; it never removes, replaces, or rotates one.**

1. **One seed contributor in the core library.** `NCode.Identity.OpenId.Core` registers the contributor by default, beside the
   tenant seed pipeline and the tenant options it owns. It generates each key through `ISecretGenerator` and stores it
   as a tenant secret. The runtime, the JSON Web Key Set, and the management API therefore read one source.
2. **Keys are named by algorithm (an open vocabulary).** `OpenId:Tenant:KeyProvisioning:Keys` lists entries of
   `{ Algorithm, KeySizeBits? }`.
   - A key's type and use follow from the registered algorithm and key types: a signature algorithm yields a `sig`
     key, a key-management algorithm an `enc` key. Any registered asymmetric algorithm can be provisioned without code
     changes ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), for example `EdDSA` when its package is
     registered, or `RSA-OAEP-256` for a tenant encryption key.
   - Symmetric and `none` algorithms are refused: a tenant key is published and used across trust boundaries.
   - An unset size is the smallest the algorithm accepts.
   - A configured list replaces the default set.
   - The startup validator ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)) and the seed
     contributor apply one rule set, `ITenantKeyPolicy`, with the same key-to-algorithm rule management writes use. A
     configuration the host accepts is therefore exactly one the seed can honor.
3. **Defaults.**
   - **Algorithms:** `RS256`, which OpenID Connect Core §15.1 requires every provider to support, plus `PS256` and
     `ES256`, the algorithms the FAPI 2.0 profile permits.
   - **One key per algorithm**, binding each key to the single algorithm it is used with (RFC 8725 §3.1).
   - **Key sizes:** RSA keys are 3072 bits, because NIST SP 800-57 accepts 2048-bit RSA only through 2030 and these
     keys have no rotation until key rollover exists. The EC key is on P-256.
   - **No default encryption key.** Access-token encryption is opt-in. A tenant that encrypts tokens to itself
     ([ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)) lists a
     key-management algorithm.
4. **A held key counts when the server could select it.** It covers an algorithm when:
   - its `use` and `alg` are null or match;
   - its type and size are ones the algorithm accepts, as the runtime credential selector judges;
   - it holds private material.

   Public-only encodings (`spki`, `x509`) never count. An encoding this policy does not know — for example an external
   keystore's — is trusted rather than duplicated. A provisioned key carries no effective lifetime: nothing enforces a
   key's expiry, and it is stamped a century out. Rollover (backlog `S8`) gives keys lifetimes.
5. **An unreadable key fails the tenant.** A key the key ring can no longer unprotect still counts as held, so nothing
   is generated beside it. The tenant read path fails with a message that names the tenant and the lost or changed key
   ring. Only the development `PersistentSigningKey` mode removes such keys, through a pruning contributor that runs
   before provisioning, which then replaces them.
6. **Concurrent seeds converge.** Adding a tenant secret touches the tenant row, whose concurrency token is checked, so
   two seeds of a new tenant — two replicas, or two requests that missed the tenant cache — cannot both commit keys.
   `ITenantProvisioner.SeedAsync` runs a seed whose commit failed once more in a fresh unit of work. Because seed
   contributors are idempotent, the retry adds only what the winner did not.
7. **Developer modes:**
   - `Ephemeral` turns provisioning off, because its tenant factory never reads persisted keys.
   - `PersistentSigningKey` is provisioning plus pruning.
   - `None` is production behavior.

   Production safety still refuses both developer modes: the pruning mode would hide a lost key ring behind a silent
   key change.
8. **Operators keep control.** A deployment that brings its own keys imports one for each configured algorithm, or sets
   `OpenId:Tenant:KeyProvisioning:Enabled` to `false`.
9. **A tenant gets only the keys its settings use.** A configured algorithm is provisioned for a tenant only when one of
   its effective algorithm lists names it. `OpenId:Tenant:KeyProvisioning:AlgorithmSettings` names those lists (an
   open vocabulary): by default, the lists the tenant signs ID tokens, UserInfo responses, introspection responses,
   JWT-secured authorization responses (JARM, `authorization_signing_alg_values_supported`), and access tokens with,
   and the key-management lists it decrypts request objects and access tokens encrypted to it with.
   - The settings materialize only after the seed, so the contributor resolves the tenant's persisted document, expanded
     through its security profiles, onto the server's committed settings through `IEffectiveSettingsResolver`
     ([ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md)), as the materialized tenant does.
   - A `fapi2` tenant, whose lists exclude `RS256`, gets no RS256 key. A tenant that later permits a configured
     algorithm gains its key at its next seed (decision 10).
   - The configured set stays the ceiling: a client registering an algorithm outside it, such as `ES384`, still needs
     the algorithm configured or a key imported.
   - A held key the tenant's settings no longer use is kept: provisioning never removes a key, and retiring one is key
     lifecycle (backlog `S8`).
10. **A committed tenant change re-seeds the tenant at once.** A tenant is seeded when it is materialized, and the
    tenant cache keeps a materialized tenant until its absolute lifetime ends, so a tenant whose settings later permit a
    configured algorithm would advertise it without a key until then. Every management write to a tenant — its record,
    its settings, a secret, or its deletion — therefore sends one `TenantChangedCommand` after it commits. The mediator
    fans it out to every registered handler, so other packages can react to a tenant change too.
    - The default handler evicts the tenant from this replica's `IOpenIdTenantCache` (`RemoveAsync`), so the change
      takes effect on this replica at once. When the tenant's settings changed (`SettingsChanged`), it also seeds the
      tenant through `ITenantProvisioner`. It evicts first: a request that misses the cache afterwards materializes
      the tenant, which seeds it too.
    - Only a settings change seeds in the write. A secret delete that removed the last key for an algorithm in use
      would otherwise get that key back before the delete returned. The next materialization still fills the gap, so
      a provisioned tenant always holds keys, and deleting the tenant removes them (decision 11).
    - `Ephemeral` developer mode keeps one in-memory key per tenant for the life of the process, so the eviction does
      not replace the key and invalidate the tokens the tenant issued, including the administrator's own.
    - The seed runs in its own service scope, because the provisioner is scoped and a change need not be announced from
      a request. A seed failure fails the write's response, as it would fail the tenant's next materialization.
    - After the seed, whether or not it succeeded, the handler announces the change on the `tenant-changed` channel of
      the replica broadcast port, so every replica evicts the tenant
      ([ADR-0205](0205-replicas-announce-changes-through-a-broadcast-port.md)). A failed announcement is logged and
      never fails the committed write. A replica that missed it keeps its cached tenant and picks the change up through
      its polls: the tenant's settings every 5 minutes and its secrets, including the key this replica provisioned,
      every 15.
    - A change of the server's effective settings also reaches every tenant's effective lists, whether it is a
      management write, a configuration reload, or a deployment. The reconciliation of the server's settings
      ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)) runs
      `TenantKeyReconciliationHandler`, which reads the keys of every tenant whose effective lists name a configured
      algorithm and sends a `TenantChangedCommand` with `SettingsChanged` for each seeded one that lacks a key, which
      evicts, seeds, and announces it. A tenant that holds no key is left to its first materialization. A failure for
      one tenant is logged and the pass continues. The pass runs in the background on one replica, outside every
      request and outside the job dispatcher, and a pass that a restart interrupted runs again.
11. **A tenant's keys are part of the tenant.** Provisioning gives every tenant keys, and those keys serve only that
    tenant, so they must not make the tenant impossible to delete. Deleting a tenant therefore removes its secrets, its
    system resource servers with their scopes and secrets, the role assignments recorded in it, and the data of its end
    users ([ADR-0212](0212-deleting-an-owner-removes-what-it-owns.md)), in one unit of work. Only a client or a resource
    server the tenant did not seed blocks the delete (`409 Conflict`): those are records an operator created, and the
    delete would otherwise destroy them.

## Options considered

- **Keep keys an operator-only decision and document an import path.** Rejected: importing needs a management token,
  and a token needs a key. Breaking the cycle would take an out-of-band tool. The generated-by-default model is what
  Auth0 and Keycloak offer.
- **Provision keys at startup.** Rejected: tenants are created at runtime and materialized lazily, and the seed path is
  already the one boundary every tenant passes through (ADR-0166).
- **Seed a single RSA key, as the developer seeder did.** Rejected: FAPI 2.0 tenants cannot use RS256, and a fixed key
  shape is a closed vocabulary.
- **One RSA key without `alg` for both RS256 and PS256.** Rejected: a key should be bound to one algorithm.
- **Provision every configured algorithm for every tenant.** Rejected (originally adopted, revised by backlog `S12`): a
  tenant whose profile excludes an algorithm held and published a key for it that it never used.
- **Provision the union of the configured set and each tenant's lists.** Rejected: the effective lists default to every
  registered asymmetric algorithm, so a tenant would hold a key for each; the configured set keeps the operator in
  control of which key types exist.
- **Remove and regenerate an unreadable key in production.** Rejected: it hides a lost key ring and silently changes
  the keys relying parties trust.
- **Provision only in the server composition (`AddIdentityServer`).** Rejected: the core library owns tenants, their
  seed pipeline, and their options, so every composition gets the same behavior.
- **2048-bit RSA keys.** Rejected for the NIST horizon above, since provisioned keys do not rotate yet.
- **Re-seed when a replica's settings poll sees a change.** Rejected: the poll runs outside a request on every replica,
  so each would race to seed the same tenant, and the new key would still wait for the secrets poll. The replica that
  makes the change seeds once, at the moment it commits. The reconciliation reacts to a replica's poll too, but one
  lease per change lets only one replica run it
  ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)).
- **Skip seeding a tenant being deleted.** Rejected: it needs a tombstone state that every seed path checks, and the
  system resource server and ownership rows would still block the delete.

## Consequences

- A production deployment boots from configuration alone: the first request to the root tenant provisions its keys,
  the bootstrap administrator obtains a token, and every tenant created through the management API holds keys before
  its create returns.
- By default every tenant publishes three signing keys; a `fapi2` tenant publishes two, because its lists exclude RS256.
- Every seed computes the tenant's effective settings once, a merge of in-memory collections, to decide which keys it
  needs.
- A tenant's first seed generates its keys, which takes hundreds of milliseconds per RSA key and never happens on a
  cache hit.
- An existing tenant gains its missing keys at its next seed; a held RS256 key, of any accepted size, satisfies RS256.
- A management write to a tenant evicts it, so the next request on that replica materializes the tenant again; a
  settings write returns after any key the new settings need has been provisioned.
- A tenant whose keys cannot be unprotected stays unavailable until its key ring is restored, so the key ring's own
  protection (backlog `S10`) matters more.
- **Breaking:**
  - `AddDeveloperSigningKey` is `AddPersistentDeveloperKeys`, and it registers only the pruning contributor.
  - `Ephemeral` mode turns provisioning off.
  - `OpenIdTenantOptions` gains `KeyProvisioning`.
  - `TenantKeySeedHandler` reads `IEffectiveSettingsResolver` to compute the tenant's settings.
  - `TenantChangedHandler` needs an `IReplicaBroadcaster` to announce the change.
  - `NCode.Identity.OpenId.Core` references the SharedState abstractions.
  - `IOpenIdTenantCache` gains `RemoveAsync`, so a custom cache must implement eviction.

## References

- [ADR-0002](0002-ephemeral-development-keys.md) — the decision this one amends: ephemeral keys stay a development
  opt-in, and persisted keys are provisioned by default.
- [ADR-0021](0021-developer-signing-keys-seeded-through-persistence.md) — the persistent developer mode, reduced to
  self-healing over provisioned keys.
- [ADR-0044](0044-management-api-authentication-and-globaladmin-bootstrap.md) — the bootstrap administrator whose
  first token needs a key.
- [ADR-0045](0045-tenant-provisioning-and-seeding-pipeline.md) and
  [ADR-0166](0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md) — the seed pipeline.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — startup validation.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — keys named by an open vocabulary.
- Code: `NCode.Identity.OpenId.Tenants.Keys.TenantKeySeedHandler`, `DefaultTenantKeyPolicy`,
  `TenantKeyProvisioningOptionsValidator`, `NCode.Identity.OpenId.OpenIdTenantKeyProvisioningOptions`,
  `NCode.Identity.OpenId.Tenants.DefaultTenantProvisioner.SeedAsync`,
  `DefaultOpenIdTenantFactory.GetTenantSecretsAsync`,
  `NCode.Identity.Server.DevelopmentEnvironment.DeveloperSecretPruningSeedHandler`,
  `NCode.Identity.Server.DeveloperKeysRegistration`.
