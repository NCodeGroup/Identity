# 1418. Limits are set by the authority above their scope, and readers learn which they may change

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A setting that merges with a meet bounds every scope beneath the one that sets it: the platform's value caps a team's,
a team's caps its tenants', and a tenant's caps its clients'
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). The bound holds only
while the scope that wrote it is the one that can change it. A node's administrators write the node's whole settings
document, so a team's administrators raise or remove a `max_tenants` or a quota the platform wrote on the team, a
tenant's owners do the same to the rate limits a team wrote on the tenant, and a client's owners do the same to the
rate limit a tenant wrote on the client. A value set above is advice, not a limit.

The settings merge declares which scopes a setting applies to
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)). It does not
declare who may write the setting in a given scope's document, and the plan tier's quotas
([ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md)) and the tenant rate limits
([ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) are the settings that most need a
writer above their scope.

A console that edits the whole document would also offer limits the caller cannot change, and the caller would learn
so only from the `403` after saving. The settings document resource cannot carry the answer: it is the audit snapshot
of the document and carries the document's ETag, while whether a caller may change a limit depends on the caller.

## Decision

**A setting can declare itself a limit, a limit in a scope's document is set by the authority over the node above that
document's node, and a settings reader asks a read-only sub-resource which limits a scope may carry and whether it may
change them.**

1. **`SettingDescriptor.IsLimit` marks a limit.** Any package marks its own settings. A limit in a team's document is
   set by the authority over the platform for a top-level team, or over the parent team for a nested one
   ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md)); in a tenant's document by the authority over its team;
   and in a client's or resource server's document by the authority over its tenant. The platform's own document is
   the root: its administrators write its limits.
2. **A limit merges with a meet.** A limit descriptor whose merge is not a meet (`HasMeetMerge`) is refused when the
   descriptor collection is built, because a scope beneath could otherwise override the value.
3. **A write that changes a limit needs `Update` at the node above.** A management settings write adds, changes or
   removes a limit when the limit is added, removed, or its JSON value differs
   (`ISettingSerializer.GetChangedLimits`, which compares JSON values). The write is refused with `403` naming the
   limits unless the caller holds `Update` at the node above. The node's own administrators cannot lower a limit
   either, so each value has one author. A value written differently but equal counts as a change, which fails closed.
4. **Creation needs no separate check.** Only roles that also manage the parent can create a node's resource there, so
   a creator already holds the authority over the limits it writes. The RFC 7592 registration update keeps the limits
   the client document carries.
5. **The built-in limits are** `max_teams`, `max_tenants`, `quota_tenant_requests_per_day`,
   `quota_tenant_token_requests_per_day`, `quota_tenant_requests_overage_per_day`,
   `quota_tenant_token_requests_overage_per_day`, `rate_limit_tenant_per_minute`, `rate_limit_address_per_minute` and
   `rate_limit_client_per_minute`. A plan tier's quotas are written on the team's document by the administrators above
   the team.
6. **"Above" is the next node of the authority chain.** A deeper chain needs no descriptor change.
7. **`GET …/settings/limits` answers `SettingLimitsResource { names, canChange }`.** The route exists on the platform,
   team, tenant, client and resource server, beside each one's settings document. `names` are the registered limits a
   document at that scope may carry, listed in ordinal order from `ISettingSerializer.GetLimits`. `canChange` says
   whether the caller holds `Update` at the node above, which for the platform's own document is the platform node.
8. **The limits route authorizes `Read` at the node.** It answers `404` for a missing resource and carries no ETag,
   because the answer is specific to the caller. The settings document resource stays the audit snapshot with its own
   ETag.
9. **The management console shows a caller without `canChange` the document's limits read-only.** It keeps them out of
   its patch, refuses editor text that names a limit, and saves the edited settings merged with the unchanged limit
   entries. A caller with `canChange` edits the whole document, with a caption naming the limits. The console caches
   each answer per owner and invalidates it when a role assignment changes, since authority above can change.

## Options considered

- **A platform-authored layer per team.** A separate document the platform writes for each team doubles every
  document, store, endpoint and cache entry, and binds only the platform. Rejected.
- **A fixed writing scope per setting.** A descriptor that names the tier that writes it ties a setting to the tier
  names and denies a team the ability to limit its own tenants. Rejected.
- **Letting a node lower its own limits.** A node that may lower a limit makes the value have two authors, and equal
  values written differently cannot be told from a change. Rejected.
- **Fields on the settings document resource.** The document is the audit snapshot and carries its ETag, and the
  answer is specific to the caller. Rejected.
- **A setting catalog in the capabilities document, with authority computed in the browser.** It duplicates the
  authority chain, nested teams included. Rejected.
- **Hiding the limits from a caller who may not change them.** The reader needs the values that bound its scope.
  Rejected.
- **Chosen:** a limit flag on the descriptor, a write check at the node above, and a caller-specific read-only limits
  sub-resource.

## Consequences

- No scope's administrators raise, lower or remove a limit set on their scope, so each value has one author.
- A package marks its own limits with `IsLimit`; the host needs no list of them.
- A plan tier's quotas and a tenant's rate limits are settings of the scope above the one they bound, written by the
  administrators of that scope; a client's owner cannot raise the rate limit its tenant set.
- The console shows a limit as read-only before the caller edits, rather than after a refused save.
- The authority chain stays in the server: the browser reads one boolean and never computes who is above a node.
- The settings document resource and its ETag are unchanged by the caller's authority, so the audit snapshot stays a
  pure representation of the document.

## References

- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md) — settings
  declare the scopes they apply to
- [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — tenants are rate limited through
  a shared expiring counter set
- [ADR-1198](1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — plan-tier quotas are daily UTC
  counts enforced at the quota
- [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md)
  — the management console reaches every owner's secrets and settings through one endpoint per operation
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) — the platform is the
  deployment root and a team groups a customer's tenants
- [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md) — a team can nest beneath a parent team
- `SettingDescriptor.IsLimit`, `ISettingSerializer.GetChangedLimits`, `ISettingSerializer.GetLimits`,
  `SettingLimitsResource`
