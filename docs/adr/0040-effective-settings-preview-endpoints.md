# 40. Effective-settings preview endpoints for tenant, client, and server

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

Settings in this family are **layered and merged** at runtime (ADR-0038): the server contributes the baseline (every
descriptor default plus supported-algorithm and configuration values), a tenant merges its persisted overrides onto the
server, and a client merges its persisted overrides onto the tenant. What a request actually sees is the _effective_
(merged) collection — `OpenIdServer.SettingsProvider.Collection`, `OpenIdTenant.SettingsProvider.Collection`, and
`OpenIdClient.Settings` — not the raw persisted blob the management API's existing `GET …/settings` endpoints return.

Discovery used to expose an anonymous `?showAll=true` override so an operator could see non-discoverable effective
settings; that override was removed as a security leak (it bypassed `IsDiscoverable` for anyone). The replacement needs
to be **authenticated and authorized**, and it should cover the other settings-bearing entities too, since "why did my
client end up with this effective value?" (after tenant ceilings like `Intersect`/`And` narrow it) is the common
operator question.

Only three entities carry settings — **server**, **tenant**, and **client**. Resource servers carry scopes, not
settings, so they are out of scope.

## Decision

**Add read-only `effective-settings` endpoints that return the resolved, merged settings view** for each
settings-bearing entity, gated by the same authorization as reading that entity's raw settings, and serialized as a flat
`name → value` JSON object using the **same value representation as the discovery document**
(`SettingDescriptor.Format`), but including settings that are not discoverable.

- **Tenant** — `GET /api/tenant/effective-settings` acts on the request's **current (resolved) tenant**, reading
  `OpenIdContext.Tenant.SettingsProvider.Collection` directly. This mirrors exactly what the removed discovery override
  exposed. It is deliberately _not_ a by-id endpoint: reconstructing the merged view for an arbitrary, non-resolved
  tenant would re-run the whole resolution pipeline, and the operator need the override served was always the current
  tenant.
- **Client** — `GET /api/clients/{clientId}/effective-settings` is **by id** (clients are always addressed by id within
  the current tenant). It resolves the client's merged view the same way the auth pipeline does
  (`Tenant.SettingsProvider.Collection.Merge(deserialized client settings)`), so the preview matches what the server
  actually resolves for that client.
- **Server** — `GET /api/servers/{serverId}/effective-settings` is **by id** and returns the resolved
  `OpenIdServer.SettingsProvider.Collection` (the root of the hierarchy — its own settings with descriptor defaults
  applied).

**No `ETag` / concurrency token on the effective view.** A merged view spans multiple persisted entities (for a client,
both the client _and_ the tenant), so no single entity's concurrency token correctly versions it; emitting one would let
a conditional request get a stale `304`. The raw `…/settings` endpoints keep their `ETag`; the effective endpoints do
not advertise one.

**Authorization reuses the raw-settings permission.** Each effective endpoint authorizes the same `Read` operation on
the same resource (the tenant/client node, or the server settings) as the corresponding raw-settings read — if you may
read an entity's persisted settings, you may read its effective view. No new scope is introduced.

## Options considered

- **A single by-id endpoint for all three (including tenant by id).** Rejected for the tenant: there is no service that
  yields a merged view for a non-resolved tenant, and the operator need (replacing the current-tenant discovery
  override) is the current tenant. The server and client are naturally by-id, so the asymmetry is inherent, not
  accidental.
- **Reuse the raw `…/settings` shape (with `ConcurrencyToken`).** Rejected: the merged view has no well-defined single
  token (see above), so the effective resources omit it.
- **A richer per-setting payload (value plus `isDiscoverable`, source layer, type).** Deferred: the flat `name → value`
  object matches the discovery representation operators already know; richer introspection can be added later without a
  breaking change.
- **Extend to resource servers.** Not applicable — resource servers have no settings.

## Consequences

- Operators get an authenticated, authorized way to inspect the fully-resolved settings (including non-discoverable
  ones) for the server, a tenant, and a client — the safe replacement for the removed anonymous discovery override.
- A client effective read performs one store load plus an in-memory deserialize-and-merge against the current tenant's
  already-resolved settings; a server effective read resolves the (cached) server; a tenant effective read is in-hand.
- The effective view is a snapshot with no `ETag`; clients needing change detection watch the underlying raw-settings
  endpoints.
- New wire contracts `TenantEffectiveSettingsResource`, `ClientEffectiveSettingsResource`, and
  `ServerEffectiveSettingsResource` (namespace `NCode.Identity.OpenId.Management.Contracts.*`).

## References

- Code: `TenantApiEndpointHandler.GetEffectiveSettingsAsync`, `ClientApiEndpointHandler.GetEffectiveSettingsAsync`,
  `ServerApiEndpointHandler.GetEffectiveSettingsAsync`, `BaseApiEndpointHandler.ToEffectiveSettingsElement`,
  `OpenIdTenant.SettingsProvider`, `OpenIdClient.Settings`, `OpenIdServer.SettingsProvider`,
  `IReadOnlySettingCollection.Merge`, `RootSettingsCollectionDataSource`.
- Prior art: [ADR-0038](0038-setting-merge-lattice-duals.md) (setting merge semantics),
  [ADR-0033](0033-authorization-model-scopes-roles-and-ownership.md) (authorization model).
