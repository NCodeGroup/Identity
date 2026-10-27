# 149. Settings declare the scopes they apply to

- **Status:** Accepted (amended by [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): the
  request-object, UserInfo, and ID token algorithm lists apply to clients; extended by
  [ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md): the access-token
  lifetime, signing, and sender-constraint settings apply to resource servers, and every setting that applies to
  resource servers merges with a lattice operation)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Setting names are a closed vocabulary ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)) and setting values
are validated against their domain ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)). Four scopes
carry a settings document — the server, each tenant, each client, and each resource server — and their documents
cascade: the server's settings merge into every tenant's, and a tenant's into each of its clients' and each of its
resource servers'. Each runtime service reads one scope's effective settings. Discovery and the tenant-wide services
(sessions, principal resolution, federated-identity linking, dynamic registration policy, UserInfo) read a tenant's;
the protocol endpoints read the effective settings of the client making the request; and access-token issuance reads a
targeted resource server's, only for the token's encryption
([ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md)).

The vocabulary was the same for every scope, so a document accepted any registered setting whether or not its scope
ever read it, and the setting was then stored without effect — the silent failure ADR-0139 removes for names and
ADR-0140 for values:

- `security_profiles` on a resource server is accepted and stored, but a resource-server document is merged unexpanded
  and every requirement is evaluated against a client's settings, so the profile neither tightens nor enforces anything
  there ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)).
- A client setting on a resource server (`require_pkce`, `access_token_lifetime`) and a tenant-wide setting on a client
  (`session_idle_timeout`, `subject_claim_types`, discovery metadata such as `service_documentation`) are the same
  class: accepted, stored, and never read.
- The management API's effective-settings previews showed every setting of the merged collection, so a resource
  server's preview listed the client settings it inherited from its tenant, and a client's listed the tenant's session
  settings, as if they governed it.

One setting showed the opposite gap: `token_endpoint_auth_signing_alg_values_supported` was enforced against the
tenant's settings, so a narrower list on a client — or the ceiling of a security profile the client selects — was
stored and never enforced.

## Decision

**Every setting declares the scopes it applies to — the scopes whose effective settings the server reads it from — and
a settings document may carry a setting only at a scope that applies it or that cascades into one that does. A setting
anywhere else is rejected at every entry point, exactly as an unregistered name or a value outside its domain is.**

1. **Scopes are registered data, and the cascade is derived from them.** A scope is a `SettingScopeDescriptor` — a name
   and the name of the parent scope whose settings cascade into it — contributed through
   `ICollectionDataSource<SettingScopeDescriptor>`, exactly as settings are contributed through
   `ICollectionDataSource<SettingDescriptor>`. `ISettingScopeCollection` validates the set when it is composed (unique
   names, registered parents, no cycles) and derives, for each scope, the scopes its settings cascade into. The OpenID
   server registers `server`, `tenant` (beneath the server), and `client` and `resource_server` (beneath the tenant),
   named by the `OpenIdSettingScopes` constants. A package that stores settings for an entity of its own — an
   organization, a connection — registers that entity's scope beneath the scope it inherits from and declares its own
   settings for it; the settings framework itself names no entity
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
2. **The descriptor declares its scopes.** `SettingDescriptor.AppliesTo` is a required list of scope names: every scope
   whose effective settings read the setting, to enforce it or to advertise it. Discovery advertises a tenant's
   effective settings, so every discoverable setting applies to tenants. The property is `required`, so a descriptor
   author cannot leave it unstated and no default means "everywhere"; a setting that names no registered scope applies
   nowhere.
3. **Acceptance follows the cascade.** `ISettingScopeCollection.AppliesWithin(descriptor, scopeName)` is true when the
   setting applies to the scope itself or to a scope the scope's settings cascade into: a setting that applies to clients
   applies within a client, a tenant, and the server, but not within a resource server. A descriptor states one fact,
   and the registered topology decides the rest, so the two cannot contradict each other.
4. **Every entry point passes the scope.** `ISettingSerializer.DeserializeSettings` and `ValidateSettings` and
   `ISecurityProfileCatalog.Expand` take the name of the registered scope whose document they read, and an unregistered
   name is an argument error. Configuration (`OpenId:Server:Settings`) is read at the server scope; management creates
   and settings patches validate at the scope of the resource they write (`400` keyed by setting name); dynamic client
   registration and the conformance and developer seeders validate at the client scope; and stored documents fail closed
   when the server, a tenant, a client, or a resource server loads them. The error names the scopes the setting applies
   to, so the operator knows where to set it.
5. **A resource server selects no security profile.** A profile is a baseline for the authorization server's dealings
   with a client: its ceilings bound the settings the client and discovery read, and its requirements run against a
   client's request. A resource server's settings govern only the access tokens issued for it. A resource server that
   needs those tokens constrained — sender-constrained, short-lived, signed with particular algorithms — states that as
   its own settings, combined with the client's at issuance by their lattice merges (issue [#100](https://github.com/NCodeGroup/Identity/issues/100) for encryption, [#101](https://github.com/NCodeGroup/Identity/issues/101)
   for the rest), not by selecting a profile whose requirements have no request to run against.
6. **Previews show what governs the scope.** A management effective-settings preview lists only the settings that apply
   within the previewed scope, so a resource server's preview omits the client settings it inherits and a client's omits
   the tenant-wide ones.
7. **A setting the scope should honor is read there.** When a setting is meaningful at a scope but read only at its
   parent, the reader moves rather than the setting being rejected. `token_endpoint_auth_signing_alg_values_supported` is
   enforced against the authenticating client's effective settings — which carry the tenant's list and every selected
   profile's ceiling — and a set list permits only what it names, an empty one nothing
   ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)).
8. **Stored documents are migrated.** The `RemoveSettingsOutsideTheirScopes` migration removes from every client
   document the settings that do not apply within a client, and from every resource-server document those that do not
   apply within a resource server. Such a setting never had an effect where it was stored, so it is removed, not
   translated — no setting at that scope expresses its intent, and moving it to the tenant would change every sibling.
   Every setting applies within the server and a tenant, so their documents are unchanged.

## Options considered

- **Declare the document scopes that may carry a setting, ancestors included.** Rejected: every descriptor would restate
  the cascade, and one that listed a client without its tenant would make a tenant default impossible without any
  signal. Declaring the scopes that read the setting states one fact; acceptance is derived from it.
- **A separate descriptor catalog per scope.** Rejected: the cascade merges one setting across scopes, so a setting read
  at several scopes would be declared several times and the declarations could disagree on type, merge, or domain.
- **Skip a setting that does not apply when a document is read.** Rejected for the reason ADR-0139 rejects it for
  names: the setting is configuration someone made, and loading the rest without it hides the problem.
- **Normalize documents on write by dropping out-of-scope settings.** Rejected: the write would report success for a
  setting it discarded.
- **Let a resource server select security profiles with its own enforcement.** Rejected: profile requirements evaluate a
  client's request, and a profile's ceilings bound client and discovery settings that a resource server never reads.
  Every constraint a resource server could want from a profile is a resource-server setting that binds the tokens
  issued for it, which the cascade already merges and the operator can state directly.
- **Reject `token_endpoint_auth_signing_alg_values_supported` on clients.** Rejected: narrowing the algorithms a client
  may sign its assertions with is meaningful per client, and a client-selected profile's ceiling must bind it.

## Consequences

- A setting configured at a scope that never reads it stops the host (configuration), returns `400` (management API),
  is refused (`invalid_client_metadata` in dynamic client registration; a seeder stops naming the client), or fails the
  request that loads it (stored document) — with a message naming the scopes the setting applies to.
- A descriptor author declares `AppliesTo` for every setting. A composed-server test pins every registered descriptor to
  a defined, non-empty set of scopes and every discoverable descriptor to tenants.
- Moving a reader to another scope changes the setting's scopes, and narrowing them ships with a data migration, as
  renaming a setting does (ADR-0139).
- The request-object and UserInfo algorithm settings were read only by discovery, so they applied to tenants;
  [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) enforces them, and the ID token
  lists, per client, so they apply to tenants and clients.
- **Breaking:** `SettingDescriptor.AppliesTo`, `SettingScopeDescriptor`, `ISettingScopeCollection`,
  `ISettingScopeCollectionProvider`, `OpenIdSettingScopes`, and the scope-name parameters of
  `ISettingSerializer.DeserializeSettings`, `ISettingSerializer.ValidateSettings`, and `ISecurityProfileCatalog.Expand`
  change the public surface; a deployment that stores settings documents in its own database applies the same removal
  there.

## References

- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — the closed vocabulary of names (extended: and of scopes).
- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) — value domains, checked at the same entry
  points.
- [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — security profiles (amended: a resource
  server selects none).
- [ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md) — the settings a targeted resource server governs.
- [ADR-0040](0040-effective-settings-preview-endpoints.md) and
  [ADR-0066](0066-resource-server-settings-and-secret-owner.md) — the effective-settings previews and resource-server
  settings documents (amended: scoped to the settings that apply within them).
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — an unset list is unrestricted; a set one permits only
  what it names.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
- Code: `NCode.Identity.Settings.SettingScopeDescriptor`, `ISettingScopeCollection`, `DefaultSettingScopeCollection`,
  `NCode.Identity.Settings.SettingDescriptor.AppliesTo`, `NCode.Identity.Settings.DefaultSettingSerializer`,
  `NCode.Identity.OpenId.Settings.OpenIdSettingScopes`, `NCode.Identity.OpenId.Settings.DefaultSettingScopeDataSource`,
  `NCode.Identity.OpenId.Settings.DefaultSettingConfigurationReader`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Settings.DefaultSecurityProfileCatalog`,
  `NCode.Identity.OpenId.Authentication.Clients.Handlers.ClientAssertionClientAuthenticationHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.BaseApiEndpointHandler`, and the `RemoveSettingsOutsideTheirScopes`
  migration.
