# 1411. The platform is the deployment root and a team groups a customer's tenants

- **Status:** Accepted (amends [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md),
  [ADR-1011](1011-secret-management-api.md),
  [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md),
  [ADR-1014](1014-server-generated-opaque-resource-ids.md),
  [ADR-1019](1019-collection-endpoints-keyset-pagination.md),
  [ADR-1024](1024-control-plane-and-per-tenant-planes.md),
  [ADR-1031](1031-control-plane-management-resource-server-and-root-tenant-seeding.md),
  [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md),
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1036](1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md),
  [ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md),
  [ADR-1042](1042-management-api-route-driven-tenant-scope.md),
  [ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md),
  [ADR-1050](1050-secret-material-revealed-once-on-create.md),
  [ADR-1058](1058-asymmetric-public-key-material-read-path.md),
  [ADR-1064](1064-frontend-presentation-platform.md),
  [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md),
  [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md),
  [ADR-1142](1142-listed-response-types-are-the-only-front-channel-token-control.md),
  [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md),
  [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
  [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md),
  [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md),
  [ADR-1174](1174-the-management-consoles-url-is-its-navigation-state.md),
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md),
  [ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md),
  [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md),
  [ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md),
  [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md),
  [ADR-1207](1207-client-authentication-methods-are-named-by-registered-descriptors.md),
  [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1223](1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md),
  [ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md),
  [ADR-1255](1255-each-tenant-surface-of-the-management-console-has-its-own-address.md),
  [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md),
  [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) and
  [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md); amended by
  [ADR-1417](1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md): a tenant administrator is a
  `TenantAdmin` assignment at the tenant's node; amended by
  [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md): a limit is set by the
  authority above the scope whose document carries it; amended by
  [ADR-1420](1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md): the console opens on the
  caller's home; amended by [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): a team can nest beneath a
  parent team; amended by
  [ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md): a team that owns a
  machine client cannot be deleted)
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Refines:** [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)

## Context

A deployment has one root: the configuration its host starts with, the master key of its Data Protection key ring and
the tenant that holds the control plane. A customer of that deployment runs related tenants, such as a sandbox, a
testing tenant and a production tenant, that share a bill, a set of administrators and a policy
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) models them as separate tenants). Nothing
names the customer: a policy that should bind all of a customer's tenants has to be written into each one, the
administrators of one tenant cannot create another, a plan's tenant quota has no scope to apply to, and usage cannot be
summed per customer.

The family already has the pieces a grouping needs: a cascade of settings scopes, each setting declaring the scopes it
applies to ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), security profiles that accumulate down the
cascade ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)), tenants named by their creator
([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)), and a role model whose assignments are owned by a scope
([ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md)). The open questions are what the deployment root
is, what groups tenants, and how authority, settings, lifecycle and the console address them.

## Decision

**The platform is the one deployment root, and a team is a persisted grouping of a customer's tenants that sits between
the platform and its tenants in the settings cascade and in authority.**

1. **The platform is the deployment root.** It is the host configuration (`OpenId:Platform:Settings`), one persisted
   settings document at `/api/platform/settings`, the master key of the Data Protection key ring
   ([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)) and the authority
   node `(platform, platform)`. It has no identifier, no list and no secrets: there is exactly one, so nothing names it.
   Its management surface is `/api/platform/settings`, its effective settings and its master keys at
   `/api/platform/encryption-keys` ([ADR-1247](1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)),
   with no list, create, delete or secret endpoints.
2. **A team is a persisted grouping of tenants.** Its creator names it, in the tenant-identifier form
   ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)); it has a display name, an `isDisabled` flag and a settings
   document. Every tenant belongs to exactly one team and never moves. The platform's own team
   (`TenantResolution:Root:TeamId`, default `platform`) holds the root tenant and every tenant a tenant-resolution
   strategy creates.
3. **The cascade has four tiers.** Settings resolve through host configuration and the platform, the team, the
   tenant, and the client or resource server, with the registered setting scopes `platform`, `team`, `tenant`, `client`
   and `resource_server` ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)). A team document carries every
   setting that applies beneath it, so the security profiles and ceilings a team selects bind its tenants
   ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)). `max_tenants` applies to the team
   scope, merges with `Min`, and a creation past it answers `409`; the ceiling is checked before the insert and never
   serialized on the team row, so two creations that race may overshoot it by one.
4. **Authority runs platform, team, tenant, leaf.** `TeamAdmin` at a team creates its tenants and manages everything
   beneath it; `Owner` never creates. Granting or revoking `TeamAdmin` is creation authority at the team node
   (`Operations.Create`), so an `Owner` can neither mint nor revoke it. Role-assignment lookups span tenants: the
   platform, team and tenant nodes have globally unique, retired identifiers, so an assignment matches them on type and
   identifier, and any other node counts an assignment only when it belongs to the node's tenant, because client and
   resource-server identifiers are unique only within a tenant. Tenants are created and listed under
   `/api/teams/{teamId}/tenants`. The root tenant holds the `platform_settings`, `platform_encryption_keys`, `teams`,
   `team_settings` and `tenants` scope families.
5. **Lifecycle.** A team's lifecycle is platform authority: an assignment at the platform node (or `GlobalAdmin`)
   creates, deletes, disables and restores a team and lists teams and tenants across teams, and a team's own owner or
   admin does none of these. A disabled team's tenants answer `404` on the protocol surface and on the management
   routes that act through a tenant's OpenID runtime (sessions, principals, local accounts), as a disabled tenant does,
   while their stored configuration (settings, clients, resource servers, grants, secrets, owners) stays manageable. A
   team with tenants cannot be deleted. A deleted team's identifier is retired
   ([ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md)). The platform's own team can be neither
   disabled nor deleted.
6. **Audit and usage carry the team.** Usage rows and tenant audit events carry the team. Team and platform audit
   events are recorded in the control-plane tenant with the resource types `team` and `platform`.
7. **The console addresses the tiers.** Its routes are `/teams`, `/settings`, `/encryption-keys`,
   `/teams/{teamId}/{tenants|settings|usage}` and `/tenants/{tenantId}/…`. The owner kinds `platform` and `team` carry
   settings only.
8. **The words.** "Platform" is the deployment root; ADR-1495's platform environments (`production`, `lab`, `ci`) are
   where a platform runs. "Team" is the grouping. "Organization" stays free for the in-tenant B2B feature
   ([ADR-1035](1035-federated-principals-and-identity-resolution.md)).

## Options considered

- **Name the grouping `organization`.** Collides with the in-tenant B2B vocabulary and with the ecosystem's use of the
  word. Rejected.
- **Name it `customer`.** Ambiguous for a consumer whose own customers are organizations. Rejected.
- **Name it `tenant group`.** Names the container by its contents, implies a many-to-many relationship, and collides
  with user groups. Rejected.
- **A listable set of root rows.** Nothing routes a tenant to one of several roots, so a second root would have no
  tenant to serve. Rejected: the platform is singular.
- **Nested teams.** A tree needs a rule for how a setting, a role and a quota inherit across several levels, and no
  customer needs more than one grouping above its tenants. Deferred until a requirement names the second level.

## Consequences

- A customer's policy is written once, at its team, and binds every tenant it creates; a plan's tenant quota has a scope
  to apply to; usage sums per team.
- The administrators of a team create tenants without holding authority over the deployment, and the platform
  administrator never needs to be a member of a customer's tenant.
- Every tenant has a team, so a tenant cannot be created without one and its creation path takes a team identifier.
- A disabled team switches off all of a customer's protocol surface and tenant runtime routes in one write, while its
  tenants' stored configuration stays manageable.
- The cascade resolves four tiers: host configuration and the platform, the team, the tenant, and the client or
  resource server; a change to a team document reaches its cached tenants on every replica.
- A tenant never moves between teams, so a customer that reorganizes recreates tenants rather than relocating them.
- A package extends the cascade by registering a setting that applies to the `team` or `platform` scope, never by
  editing the library ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## References

- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md) — unset settings are unrestricted
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md) — pre-release posture
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) — hosted platform environment model
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md) — a tenant is named by its creator
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md) — retired identifiers are never reissued
- [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — key hierarchy under an
  external root key
