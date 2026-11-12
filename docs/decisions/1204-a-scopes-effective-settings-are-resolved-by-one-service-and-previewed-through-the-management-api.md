# 1204. A scope's effective settings are resolved by one service and previewed through the management API

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A scope's effective settings are the platform's settings with the team's, the tenant's and then the client's or the
resource server's merged onto them. Before a document merges, the ceilings and defaults of every security profile it
selects are expanded into it, so a client that selects `fapi2` carries that profile's limits whatever its own document
says ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
A resource server selects no security profile
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)).

The live providers materialize this merge for a request. Code that runs outside a materialized tenant or client needs
the same result: a key seed, a management preview, and a check of a proposed change. A merge rebuilt by hand in each of
those places drifts: copies read the platform from different sources (the live provider, which this replica refreshes by
a poll, or a persisted document), and a copy that skips the expansion yields a different answer than the runtime. A
seed that runs on the replica that committed a change must see the committed document rather than a copy that is not
yet refreshed.

An operator also needs to see what a scope really gets. The raw settings document of a scope holds only what that scope
sets, so it does not show the inherited values, the expanded profiles or the defaults.

## Decision

One service resolves a scope's effective settings, and the management API previews the result.

### The resolver

`IEffectiveSettingsResolver` (`NCode.Identity.OpenId.Abstractions`, namespace `NCode.Identity.OpenId.Settings`) is the
one service that resolves a scope's effective settings outside a materialized tenant or client:

- `ReadScopeSettings(parentSettings, scopeName, settings)`
- `Resolve(parentSettings, scopeName, settings)`
- `ResolveStored(parentSettings, scopeName, ownerId, settings)`
- `ResolvePlatformAsync(settings, ct)` for a proposed platform document, and `ResolvePlatformAsync(ct)` for the
  committed one
- `ResolveTeamAsync(teamId, ct)`, `ResolveTeamAsync(teamId, settings, ct)` and `ResolveTeamParentAsync(teamId, ct)`,
  which fold a team's lineage ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md))

Its behavior:

- `ReadScopeSettings` deserializes a document with the environment's JSON options, adds the profiles the environment
  type implies at the tenant scope
  ([ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md)) and expands the selected
  profiles. The scope name lets a setting outside its scope fail, and a document that cannot be honored, an unregistered
  profile included, fails with a `JsonException`.
- `ResolvePlatformAsync(ct)` reads the committed platform row from `IPlatformStore`, never this replica's polled copy,
  and returns the configured settings when there is no row, so a seed, a preview or a check sees a write at once on any
  replica.
- `ResolveStored` and the committed-document reads throw `UnreadableSettingsException`, naming the scope and owner
  ([ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)). `Resolve`
  serves proposed documents and the sites that skip an unreadable one.
- Every caller goes through the resolver: the team and tenant factories, the client resolver, the resource-server
  credential resolver, the key seed, rollover and reconciliation, the conformance seed and seeded-client validators, the
  management handlers and the coherence walks. A resource-server document goes through it too.
- `DefaultOpenIdPlatformFactory` and `RootSettingsCollectionDataSource` call `ISecurityProfileCatalog` directly, because
  the resolver depends on `IOpenIdPlatformProvider`. Nothing on the path that builds the platform depends on the
  resolver, and options are read on use, never in constructors.
- `DefaultEffectiveSettingsResolver` is registered with `TryAddSingleton`, so a host can replace it.
- Which settings writes walk the clients is decided by `IClientCoherenceValidator.ReadsChangedSetting` over untracked
  settings pages ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules.md)).

### The preview

The management API previews effective settings with `GET …/effective-settings` on the platform
(`/api/platform/effective-settings`), a team (`/api/teams/{teamId}`), a tenant (`/api/tenants/{tenantId}`), a client and
a resource server, each addressed by route.

- The body is a flat `name → value` JSON object in the discovery representation (`SettingDescriptor.Format`),
  non-discoverable settings included. It lists only the settings that apply within the scope
  ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), resolved
  and expanded as at runtime.
- A preview carries no ETag, because a merged view spans several entities.
- It authorizes `Read` on the same resource as the raw settings read and resolves only after authorization.
- The wire contracts are `PlatformEffectiveSettingsResource`, `TeamEffectiveSettingsResource`,
  `TenantEffectiveSettingsResource`, `ClientEffectiveSettingsResource` and `ResourceServerEffectiveSettingsResource` in
  `NCode.Identity.OpenId.Management.Contracts`.
- Discovery has no anonymous override that shows non-discoverable settings; the authorized preview is the way to see
  them.

## Options considered

- **A static helper.** Rejected: it cannot read the committed platform row or the environment's options, and a host
  cannot replace it.
- **Resolving onto the polled platform copy.** Rejected: a replica that committed a change would resolve against a copy
  that is not yet refreshed.
- **A copy of the merge in each caller.** Rejected: the copies drift in their source of the platform and in whether
  they expand profiles.
- **An anonymous discovery override that shows non-discoverable settings.** Rejected: it discloses settings to callers
  without authority.
- **The raw settings shape with a concurrency token.** Rejected: a merged view is not an entity that can be written back,
  so a token has nothing to guard.
- **A richer per-setting payload.** Rejected: the flat discovery representation is what a consumer of the effective
  values needs.
- **One injectable resolver with an authorized preview (chosen).** Every caller and the preview share one answer, and a
  host replaces the answer in one place.

## Consequences

- A seed, a preview, a check and a runtime factory agree on what a scope permits, on any replica, because they share one
  merge and the committed platform document.
- A host replaces the resolution by registering its own `IEffectiveSettingsResolver` before the library.
- A merge written by hand in a new caller is a smell: it belongs in the resolver.
- A preview costs one resolution per request and carries no ETag, so a client cannot make a conditional request on it.
- The resolver and the preview are covered by tests of the resolver, of each preview endpoint's authorization and body,
  and of the callers that resolve through it.

## References

- Code: `IEffectiveSettingsResolver`, `DefaultEffectiveSettingsResolver`, `UnreadableSettingsException`,
  `DefaultOpenIdPlatformFactory`, `RootSettingsCollectionDataSource`, `IClientCoherenceValidator`,
  `ISecurityProfileCatalog`, the `GetEffectiveSettingsAsync` methods of the platform, team, tenant, client and
  resource-server API endpoint handlers, and the `*EffectiveSettingsResource` contracts.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  (setting merge semantics).
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md),
  [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules.md),
  [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md),
  [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) and
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md).
