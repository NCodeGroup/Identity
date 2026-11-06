# 1418. A limit is set by the authority above the scope whose document carries it

- **Status:** Accepted (amends [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md),
  [ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md); amended by
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): a limit in a child team's document is set by the
  authority over its parent team, and `max_teams` is a limit)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md)

## Context

A setting that merges with a meet bounds every scope beneath the one that sets it: the platform's value caps a team's,
a team's caps its tenants', and a tenant's caps its clients'
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). The bound holds only
while the scope that wrote it is the one that can change it. A node's administrators write the node's whole settings
document, so a team's administrators raise or remove a `max_tenants` or a quota the platform wrote on the team, a
tenant's owners do the same to the rate limits a team wrote on the tenant, and a client's owners do the same to the
rate limit a tenant wrote on the client. A value set above is advice, not a limit.

The settings merge already declares which scopes a setting applies to
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md)). It does not declare who may write the setting in a
given scope's document, and the plan tier's quotas
([ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md)) and the tenant rate limits
([ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) are the settings that most need a
writer above their scope.

## Decision

**A setting can declare itself a limit, and a limit in a scope's document is set by the authority over the node above
that document's node.**

1. **`SettingDescriptor.IsLimit` marks a limit.** A limit in a team's document is set by the authority over the
   platform, in a tenant's document by the authority over its team, and in a client's or resource server's document by
   the authority over its tenant. The platform's own document is the root: its administrators write its limits. Any
   package marks its own settings.
2. **A limit merges with a meet.** A limit whose descriptor lacks a meet merge is refused when the descriptor
   collection is built, because a scope beneath could otherwise override the value.
3. **A write that changes a limit needs `Update` at the node above.** A management settings write adds, changes or
   removes a limit when the limit is added, removed, or its JSON value differs
   (`ISettingSerializer.GetChangedLimits`). The write is refused with `403` naming the limits unless the caller holds
   `Update` at the node above. The node's own administrators cannot lower a limit either, so a value has one author. A
   value written differently but equal counts as a change, which fails closed.
4. **Creation needs no separate check.** The authority to create a node's resource at its parent is held only by roles
   that also manage the parent, so a creator already holds the authority over the limits it writes. The check runs on
   the four settings `PATCH` endpoints: team, tenant, client and resource server. The RFC 7592 registration update
   replaces a client's whole document from metadata that names no limit, and keeps the limits the document carries.
5. **The limits are** `max_tenants`, `quota_tenant_requests_per_day`, `quota_tenant_token_requests_per_day`,
   `quota_tenant_requests_overage_per_day`, `quota_tenant_token_requests_overage_per_day`,
   `rate_limit_tenant_per_minute`, `rate_limit_address_per_minute` and `rate_limit_client_per_minute`. A plan tier is
   written on the team's document by the platform's administrators, which amends decision 4 of
   [ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md).
6. **"Above" is the next node of the authority chain.** A deeper chain, such as a team nested in a team, needs no
   descriptor change: a nested team's limits fall under its parent team.

## Options considered

- **A platform-authored per-team layer.** A separate document the platform writes for each team doubles every
  document, store, endpoint and cache entry, and binds only the platform. Rejected.
- **A fixed writing scope per setting.** A descriptor that names the tier that writes it ties a setting to the tier
  names and denies a team the ability to limit its own tenants. Rejected.
- **Letting a node lower its own limits.** A node that may lower a limit makes the value have two authors, and equal
  values written differently cannot be told from a change. Rejected.

## Consequences

- Who may change which limit follows the authority chain: no scope's administrators raise, lower or remove a limit set
  on their scope.
- A package marks its own limits with `IsLimit`; the host needs no list of them.
- A plan tier's quotas and a tenant's rate limits are settings of the scope above the one they bound, written by the
  administrators of that scope.
- A client's or resource server's limits are changed by its tenant's administrators, so a client's owner cannot raise
  the rate limit its tenant set.

## References

- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md) — settings declare the scopes they apply to
- [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — tenants are rate limited through
  a shared expiring counter set
- [ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — plan-tier quotas are daily UTC
  counts enforced at the quota
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform is the
  deployment root and a team groups a customer's tenants
