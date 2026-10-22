# 204. A scope's effective settings are resolved by one service

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A scope's effective settings are the server's settings with the tenant's settings merged onto them, and then the
client's or the resource server's merged onto those. Before a document merges, the ceilings and defaults of every
security profile it selects are expanded into it, so a client that selects `fapi2` carries that profile's limits
whatever its own document says ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)). A resource
server selects no security profile ([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)).

The live providers materialize this merge for a request. Code that runs outside a materialized tenant or client needs
the same result: a key seed, a management preview, and a check of a proposed change. A review (backlog `R39`) found that
the merge was rebuilt by hand in about ten places: `TenantKeySeedHandler`, the tenant factories
(`DefaultOpenIdTenantFactory` and `EphemeralOpenIdTenantFactory`), `DefaultOpenIdClientResolver`,
`DefaultResourceServerCredentialResolver`, `ConformanceSeedHandler`, the tenant, client, resource-server, and server
management handlers, and `DefaultClientResponseAlgorithmValidator`.

The copies had already drifted:

- They read the server from different sources. Some read the live provider, which this replica refreshes by a poll
  every 5 minutes, and others merged the configured settings with a persisted document.
- The resource-server handler merged its document without expanding it, while every other copy expanded the document it
  read.

Two pieces of work needed one answer to what the server permits. The seed that re-seeds tenants after a server settings
change (backlog `R50`) runs on the replica that committed the change, so it must see the committed document rather than
the polled copy. The server settings PATCH (backlog `R21`) loaded every tenant and every client with their secrets and
with change tracking to check each client's registered algorithms
([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)), even when the change touched nothing
that check reads.

## Decision

1. **One service resolves a scope's effective settings.** `IEffectiveSettingsResolver`
   (`NCode.Identity.OpenId.Abstractions`, namespace `NCode.Identity.OpenId.Settings`) has four members:
   - `ReadScopeSettings(scopeName, document)` deserializes a scope's persisted document with the environment's JSON
     options and expands the security profiles it selects. The scope name lets a setting that does not apply within the
     scope fail the read ([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)).
   - `Resolve(parent, scopeName, document)` merges that onto the parent's effective settings.
   - `ResolveServerAsync(document, cancellationToken)` resolves a proposed server document onto the server's configured
     settings.
   - `ResolveServerAsync(cancellationToken)` does the same for the server's committed document.

   A document that selects a security profile that is not registered fails with a `JsonException`.
2. **The server's committed document is read when it is asked for.** `ResolveServerAsync(cancellationToken)` reads the
   server's settings row from `IServerStore` and merges it onto the configured settings, and returns the configured
   settings when the store holds no row. It does not read this replica's polled copy, so a seed, a preview, or a check
   that follows a server settings write sees the change at once, on the replica that made it and on any other.
3. **Every caller goes through it.** Each place that rebuilt the merge, listed in the Context, calls `Resolve`,
   `ReadScopeSettings`, or `ResolveServerAsync`. A resource server's document goes through `Resolve` as well, so no
   copy treats a scope differently; the result is the same as a merge without expansion, because that scope selects no
   profile. `DefaultOpenIdServerFactory` and `RootSettingsCollectionDataSource` keep calling `ISecurityProfileCatalog`,
   because the resolver depends on `IOpenIdServerProvider`, which the factory builds, and would create a cycle.
4. **The resolver is registrable and replaceable.** Core registers `DefaultEffectiveSettingsResolver` with
   `TryAddSingleton`, so a host that registers its own implementation first keeps it, and every caller above uses the
   host's.
5. **A change walks the clients only when it can matter, and reads them untracked.**
   `ClientResponseAlgorithmRule.ReadsChangedSetting(previous, current)` returns `true` when the effective value of any
   setting the algorithm check reads differs between two effective settings: each response's registered signing,
   key-management, and content-encryption algorithm, and its permitted lists, which are compared ignoring order. The
   server and tenant PATCH handlers resolve the settings before and after the change through the resolver and check the
   clients only when it returns `true`. The walks read tenants and clients through `ITenantStore.GetSettingsPageAsync`
   and `IClientStore.GetSettingsPageAsync`, which return a page of settings documents that is untracked, carries no
   secrets, and is ordered by identifier with a keyset cursor. The client page applies the ambient tenant's query
   filter, so a walk that spans tenants runs each tenant's clients under an explicit tenant scope.

## Options considered

- **A static helper.** Rejected: a host could not replace it, and it could not read the committed server document,
  which needs the store and the server provider.
- **Resolving onto the polled server settings.** Rejected: the seed that `R50` dispatches after a server settings
  change runs on the writing replica, whose polled copy can be 5 minutes behind the commit. A newly permitted
  algorithm would be missed by the seed, and a preview would show a stale state.
- **Keeping the copies.** Rejected: they had drifted, and every further caller would copy the merge again.

## Consequences

- A seed or a preview reads the server's settings row once per call to `ResolveServerAsync(cancellationToken)`.
- A type that needs a scope's effective settings outside a materialized tenant or client takes an
  `IEffectiveSettingsResolver` in its constructor. The public `DefaultOpenIdTenantFactory` and
  `EphemeralOpenIdTenantFactory` take one, so a host that constructs or derives from either supplies it
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)). The same holds for the internal seed, resolver, and
  management types listed in the Context.
- The resolver depends on `IOpenIdServerProvider`, so nothing on the path that builds the server may depend on it.
- Putting the resolver under `DefaultOpenIdClientResolver` exposed a host-composition cycle. Reading
  `IOptions<OpenIdOptions>.Value` runs the `OpenIdOptions` validators, which read the setting-descriptor catalog, which
  constructs every `IClientAuthenticationHandler` (`DefaultClientSettingDescriptorDataSource`). A handler that depends,
  however indirectly, on a service whose constructor reads `Value` therefore deadlocks host composition, and the
  container cannot detect the cycle because its last edge is a runtime `GetServices` call.
  `DefaultOpenIdEnvironmentFactory` and `DefaultOpenIdServerFactory` read `IOptions<OpenIdOptions>.Value` on use, never
  in a constructor. Naming the methods without constructing the handlers is backlog `R60`.
- A server or tenant settings change that touches no setting the algorithm check reads loads no client, and a change
  that does reads each tenant's and client's settings without their secrets.
- This implements backlog `R39` (one resolver) and `R21` (a bounded server settings PATCH).
