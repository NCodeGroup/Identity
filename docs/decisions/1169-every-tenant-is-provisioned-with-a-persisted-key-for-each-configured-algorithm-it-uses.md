# 1169. Every tenant is provisioned with a persisted key for each configured algorithm it uses

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A tenant signs with the keys in its persisted secrets, and the JSON Web Key Set publishes the same keys. Outside
development, nothing would create such a key unless the server did. Creating keys by default is open to three
objections ([ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)):

- keys regenerated on every restart invalidate every issued token;
- replicas would each mint their own keys;
- key provenance is a decision operators must own.

The first two concern keys held in memory. A key generated once, persisted and protected by the stored-secret key
hierarchy survives restarts and is the same on every replica. The third is met when generation is a configurable default
that never overrides a key the tenant already holds, which is the model of Auth0 tenant signing keys and Keycloak realm
keys.

Without provisioning, a fresh production database has no tenant key. The JSON Web Key Set is empty and nothing can be
signed, including the bootstrap administrator's first management token
([ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)),
which is the only credential that could import a key. The deployment deadlocks before its first request.

Every tenant already passes through one seed pipeline under every strategy
([ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md)), and that
pipeline is where tenant data is reserved. Settings materialize only after the seed, so the seed cannot read a
materialized tenant to learn which algorithms the tenant uses.

## Decision

**When a tenant is seeded, it gets a persisted key for each configured algorithm its effective settings use and no
serving key satisfies. The server generates keys to fill those gaps, and the runtime, the JSON Web Key Set and the
management API read that one source.**

1. **One seed contributor in the core library.** `NCode.Identity.OpenId.Core` registers `TenantKeySeedHandler` by
   default, beside the tenant seed pipeline and the tenant options it owns. It generates each key through
   `ISecretGenerator` and stores it as a tenant secret. The seed applies the rollover plan, which holds key lifetimes,
   successors and retirement ([ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)).
2. **Keys are named by algorithm, an open vocabulary.** `OpenId:Tenant:KeyProvisioning:Keys` lists
   `OpenIdTenantKeyOptions { Algorithm, KeySizeBits?, Curve?, SecretType? }` entries.
   - A key's use follows its algorithm: a signature algorithm yields a `sig` key and a key-management algorithm an `enc`
     key. Any registered asymmetric algorithm can be provisioned without code changes
     ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), for example `EdDSA` when its package is
     registered, or `RSA-OAEP-256` for a tenant encryption key.
   - Symmetric and `none` algorithms are refused: a tenant key is published and used across trust boundaries.
   - An unset size is the smallest the algorithm accepts.
   - A configured list is the whole set; it does not add to the defaults.
   - A key may name its `SecretType` among the key types its algorithm accepts and its `Curve` among that type's curves;
     otherwise it takes the first of them ([ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md)). A
     key is generated on the curve its algorithm is bound to
     ([ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md)).
3. **One rule set for validation and seeding.** `ITenantKeyPolicy`, implemented by `DefaultTenantKeyPolicy`, is the rule
   set of both `TenantKeyProvisioningOptionsValidator`
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md))
   and the seed, with the same key-to-algorithm rule management writes use. A configuration the host accepts is
   therefore one the seed can honor.
4. **Defaults.**
   - **Algorithms:** `RS256`, which OpenID Connect Core section 15.1 requires every provider to support, plus `PS256`
     and `ES256`, the algorithms the FAPI 2.0 profile permits.
   - **One key per algorithm**, binding each key to the single algorithm it is used with (RFC 8725 section 3.1).
   - **Key sizes:** RSA keys are 3072 bits, because NIST SP 800-57 accepts 2048-bit RSA only through 2030. The EC key is
     on P-256.
   - **No default encryption key.** Access-token encryption is opt-in. A tenant that encrypts tokens to itself
     ([ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md)) lists
     a key-management algorithm.
5. **A held key satisfies a specification only when the server could select it.** All of these hold:
   - its `alg` is null or matches the specification;
   - its encoding is not public (`spki` and `x509` never count);
   - its custody matches the specification's
     ([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md));
   - its `SecretType` is the configured one;
   - its stored public half reads and is on the specification's curve;
   - its `use` and its key are ones the algorithm accepts, as the runtime credential selector judges. The configured
     size is not compared, so a held RS256 key of any size RS256 accepts satisfies RS256.

   Otherwise a key is provisioned.
6. **A tenant gets only the keys its settings use.** A configured algorithm is provisioned for a tenant only when one of
   its effective algorithm lists names it. `OpenId:Tenant:KeyProvisioning:AlgorithmSettings` names those lists, an open
   vocabulary. By default they are the lists the tenant signs ID tokens, UserInfo responses, introspection responses,
   JWT-secured authorization responses (JARM) and access tokens with, and the key-management lists it decrypts request
   objects and access tokens encrypted to it with. Configuration adds to them.
   - The configured key set stays the ceiling: a `fapi2` tenant, whose lists exclude `RS256`, gets no RS256 key, and a
     client registering an algorithm outside the set, such as `ES384`, still needs the algorithm configured or a key
     imported.
   - The seed resolves the tenant's effective settings through `IEffectiveSettingsResolver`
     ([ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)),
     because settings materialize only after the seed.
7. **An unreadable key fails the tenant.** A key that cannot be unprotected counts as held, so nothing is generated
   beside it. The tenant load fails with a message that names the tenant and the lost or changed key. Only the
   development opt-in `DeveloperKeys:PruneUnreadableKeys` (`AddDeveloperKeyPruning`,
   [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md))
   removes such a key first, through a contributor that runs before provisioning.
8. **Concurrent seeds converge.** Adding a tenant secret checks the tenant row's concurrency token, so two seeds of a
   new tenant, from two replicas or two requests that missed the tenant cache, cannot both commit keys.
   `ITenantProvisioner.SeedAsync` retries a failed commit once in a fresh unit of work. Seed contributors are
   idempotent, so the retry adds only what the winner did not.
9. **A committed settings change re-seeds at once.** A management write that changes a tenant's settings sends
   `TenantChangedCommand` with `SettingsChanged`, and its handler seeds the tenant before the write returns. A change of
   the server's settings runs `TenantKeyReconciliationHandler` in the per-team reconciliation
   ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)),
   which re-seeds each seeded tenant of the team that lacks a serving key. A tenant that holds no key is left to its
   first materialization.
10. **Operators keep control.** A deployment that brings its own keys imports one per algorithm, or sets
    `OpenId:Tenant:KeyProvisioning:Enabled` to `false`. Deleting a tenant removes its keys
    ([ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md)).

## Options considered

- **Keep keys an operator-only decision and document an import path.** Rejected: importing needs a management token,
  and a token needs a key, so the deployment deadlocks. Breaking the cycle would take an out-of-band tool. The
  generated-by-default model is what Auth0 and Keycloak offer.
- **Provision keys at startup.** Rejected: tenants are created at runtime and materialized lazily, and the seed path is
  the one boundary every tenant passes through.
- **Seed a single RSA key.** Rejected: FAPI 2.0 tenants cannot use RS256, and a fixed key shape is a closed vocabulary.
- **One RSA key without `alg` for both RS256 and PS256.** Rejected: a key should be bound to one algorithm.
- **Provision every configured algorithm for every tenant.** Rejected: a tenant whose profile excludes an algorithm
  would hold and publish a key for it that it never uses.
- **Provision the union of the configured set and each tenant's lists.** Rejected: the effective lists default to every
  registered asymmetric algorithm, so a tenant would hold a key for each; the configured set keeps the operator in
  control of which key types exist.
- **Remove and regenerate an unreadable key in production.** Rejected: it hides a lost key and silently changes the keys
  relying parties trust.
- **Provision only in `AddIdentityServer`.** Rejected: the core library owns tenants, their seed pipeline and their
  options, so every composition gets the same behavior.
- **2048-bit RSA keys.** Rejected for the NIST horizon above.
- **Re-seed from each replica's settings poll.** Rejected: the poll runs outside a request on every replica, so each
  would race to seed the same tenant, and the new key would still wait for the secrets poll. The replica that makes the
  change seeds once, when it commits. The reconciliation also reacts to a poll, but one lease per change lets only one
  replica run it.
- **Generate a tenant's keys by default, only for the algorithms its settings use (chosen).** The seed fills the gaps
  the effective settings leave, never overrides a held key, and leaves import and opt-out to operators.

## Consequences

- A production deployment boots from configuration alone: the first request to the root tenant provisions its keys, the
  bootstrap administrator's first token needs no import, and every tenant created through the management API holds keys
  before its create returns.
- By default a tenant publishes three signing keys; a `fapi2` tenant publishes two.
- Every seed computes the tenant's effective settings once, a merge of in-memory collections, to decide which keys it
  needs.
- A tenant's first seed generates its keys, which takes hundreds of milliseconds per RSA key and never happens on a
  cache hit.
- A tenant whose keys cannot be unprotected stays unavailable until its key is restored, so the protection of the key
  hierarchy matters more.
- Tests cover the seed handler, the policy and its validator, key satisfaction, concurrent seeds and the reconciliation
  handler.

## References

- [ADR-1370](1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md)
  — development conveniences and the pruning opt-in.
- [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)
  — the bootstrap administrator whose first token needs a key.
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) — the seed
  pipeline.
- [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md) — key lifetimes
  and rollover.
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)
  — the per-team reconciliation.
- [ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)
  — effective settings.
- [ADR-1212](1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md) — tenant deletion.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — keys named by an open vocabulary.
- Code: `NCode.Identity.OpenId.Tenants.Keys.TenantKeySeedHandler`, `DefaultTenantKeyPolicy`,
  `TenantKeyProvisioningOptionsValidator`, `NCode.Identity.OpenId.OpenIdTenantKeyProvisioningOptions`,
  `NCode.Identity.OpenId.Tenants.DefaultTenantProvisioner.SeedAsync`,
  `NCode.Identity.Server.DeveloperKeysRegistration`.
