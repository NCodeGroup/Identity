# 1149. Settings declare the scopes they apply to, and the scopes are a registered cascade

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Setting names are a closed vocabulary
([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md))
and setting values are validated against their domain
([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)). Several scopes carry a
settings document, and their documents cascade: the platform's settings merge into each team's, a team's into its
tenants', and a tenant's into each of its clients' and each of its resource servers'. Each runtime service reads one
scope's effective settings. Discovery and the tenant-wide services (sessions, principal resolution, federated-identity
linking, dynamic registration policy, UserInfo) read a tenant's; the protocol endpoints read the effective settings of
the client making the request; and access-token issuance reads a targeted resource server's
([ADR-1075](1075-runtime-consumption-of-resource-server-secrets.md)).

If the vocabulary were the same for every scope, a document would accept any registered setting whether or not its
scope ever read it, and the setting would be stored without effect, the silent failure that
[ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)
removes for names and
[ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) for values:

- `security_profiles` on a resource server would be accepted and stored, but a resource-server document is merged
  unexpanded and every requirement is evaluated against a client's settings, so the profile would neither tighten nor
  enforce anything there
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
- A client setting on a resource server (`require_pkce`, `access_token_lifetime`) and a tenant-wide setting on a client
  (`session_idle_timeout`, `subject_claim_types`, discovery metadata such as `service_documentation`) are the same
  class: accepted, stored, and never read.
- A management effective-settings preview would list every setting of the merged collection, so a resource server's
  preview would show the client settings it inherits from its tenant, and a client's the tenant's session settings, as
  if they governed it.

A setting that is meaningful at a scope must also be read there. `token_endpoint_auth_signing_alg_values_supported`
narrows the algorithms a client may sign its assertions with, so it is meaningful per client, and a security profile
the client selects must bind it.

## Decision

**Every setting declares the scopes it applies to, the scopes whose effective settings the server reads it from, and a
settings document may carry a setting only at a scope that applies it or that cascades into one that does. A setting
anywhere else is rejected at every entry point, exactly as an unregistered name or a value outside its domain is.**

1. **Scopes are registered data, and the cascade is derived from them.** A scope is a `SettingScopeDescriptor`, a `Name`
   and the `ParentName` of the scope whose settings cascade into it, contributed through
   `ICollectionDataSource<SettingScopeDescriptor>`, exactly as settings are contributed through
   `ICollectionDataSource<SettingDescriptor>`. `ISettingScopeCollection`, reached through
   `ISettingScopeCollectionProvider`, validates the set when it is composed (unique names, registered parents, no
   cycles) and derives each scope's cascade.
2. **The OpenID server registers its scopes.** `platform` is the root, `team` is beneath it, `tenant` is beneath the
   team, and `client` and `resource_server` are beneath the tenant, named by the `OpenIdSettingScopes` constants.
   Nested teams fold their lineage in the resolver
   ([ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md)). A package that stores settings for an entity of its own
   registers that entity's scope beneath the scope it inherits from and declares its own settings for it; the settings
   framework names no entity ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
3. **The descriptor declares its scopes.** `SettingDescriptor.AppliesTo` is a required list of the scopes whose
   effective settings read the setting, to enforce it or to advertise it. The property is `required`, so no default
   means "everywhere", and a setting that names no registered scope applies nowhere. Discovery advertises a tenant's
   effective settings, so every discoverable setting applies to tenants.
4. **Acceptance follows the cascade.** `ISettingScopeCollection.AppliesWithin(descriptor, scopeName)` is true when the
   setting applies to that scope or to a scope beneath it. A client setting is accepted in client, tenant, team and
   platform documents but not in a resource-server document. A descriptor states one fact and the registered topology
   decides the rest, so the two cannot contradict each other.
5. **Every entry point passes a registered scope name.** `ISettingSerializer.DeserializeSettings` and `ValidateSettings`,
   `ISecurityProfileCatalog.Expand` and `IEffectiveSettingsResolver` take the name of the registered scope whose
   document they read, and an unregistered name is an argument error.
6. **Each source validates at its own scope.** Configuration is read at the platform scope; management writes validate
   at the written resource's scope and answer `400`, keyed by setting name; dynamic client registration and the seeders
   validate at the client scope; stored documents fail closed when they load. The error names the scopes the setting
   applies to, so the operator knows where to set it.
7. **A resource server selects no security profile.** A profile is a baseline for the authorization server's dealings
   with a client: its ceilings bound the settings the client and discovery read, and its requirements run against a
   client's request. `security_profiles` is therefore rejected on a resource-server document. A resource server's
   constraints are its own settings, combined with the client's at issuance by their lattice merges
   ([ADR-1158](1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)), and every setting
   that applies to resource servers merges with a lattice operation, which a test pins.
8. **Examples.** The request-object, UserInfo and ID token algorithm lists apply to clients
   ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)); the access-token lifetime,
   signing and sender-constraint settings apply to resource servers
   ([ADR-1158](1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)).
9. **Previews show what governs the scope.** A management effective-settings preview lists only the settings that apply
   within the previewed scope
   ([ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)).
10. **A setting meaningful at a scope is read there.** `token_endpoint_auth_signing_alg_values_supported` is enforced
    against the authenticating client's effective settings, which carry the tenant's list and the ceiling of every
    profile the client selects, and a set list permits only what it names
    ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
11. **A test pins the descriptors.** A composed-server test pins every registered descriptor to a defined, non-empty set
    of scopes and every discoverable descriptor to tenants. Narrowing a setting's scopes edits its descriptor, and a
    stored document that carries the setting outside them fails closed
    ([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)).

## Options considered

- **Declare the document scopes that may carry a setting, ancestors included.** Rejected: every descriptor would restate
  the cascade, and one that listed a client without its tenant would make a tenant default impossible without any
  signal. Declaring the scopes that read the setting states one fact; acceptance is derived from it.
- **A separate descriptor catalog per scope.** Rejected: the cascade merges one setting across scopes, so a setting read
  at several scopes would be declared several times and the declarations could disagree on type, merge or domain.
- **Skip a setting that does not apply when a document is read.** Rejected for the reason
  [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)
  rejects it for names: the setting is configuration someone made, and loading the rest without it hides the problem.
- **Drop out-of-scope settings on write.** Rejected: the write would report success for a setting it discarded.
- **Let a resource server select security profiles.** Rejected: profile requirements evaluate a client's request, and a
  profile's ceilings bound client and discovery settings that a resource server never reads. Every constraint a resource
  server could want from a profile is a resource-server setting that binds the tokens issued for it, which the cascade
  already merges and the operator can state directly.
- **Reject `token_endpoint_auth_signing_alg_values_supported` on clients.** Rejected: narrowing the algorithms a client
  may sign its assertions with is meaningful per client, and a client-selected profile's ceiling must bind it.
- **Declare the scopes that read the setting and derive acceptance from the registered cascade.** Chosen, as the
  Decision states.

## Consequences

- A setting configured at a scope that never reads it stops the host (configuration), returns `400` (management API),
  is refused with `invalid_client_metadata` (dynamic client registration; a seeder stops naming the client), or fails the
  request that loads it (stored document), with a message naming the scopes the setting applies to.
- A descriptor author declares `AppliesTo` for every setting; the composed-server test fails a descriptor that leaves it
  empty or a discoverable one that omits tenants.
- A package adds an entity's settings by registering a scope and descriptors, never by editing the framework.
- A resource server cannot weaken or strengthen a client's profile; it constrains only the tokens issued for it.
- Previews list only what governs the previewed scope, so an operator is not shown a setting that has no effect there.

## References

- [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md):
  the closed vocabulary of names, extended to scopes.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md): value domains, checked at
  the same entry points.
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md):
  security profiles, which a resource server does not select.
- [ADR-1075](1075-runtime-consumption-of-resource-server-secrets.md): the settings a targeted resource server governs.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  an unset list is unrestricted; a set one permits only what it names.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): vocabularies are open unless provably closed.
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
  [ADR-1158](1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md),
  [ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md) and
  [ADR-1426](1426-a-team-can-nest-beneath-a-parent-team.md): the settings that apply to clients and resource servers,
  the previews, and nested teams.
- Code: `NCode.Identity.Settings.SettingScopeDescriptor`, `ISettingScopeCollection`, `ISettingScopeCollectionProvider`,
  `DefaultSettingScopeCollection`, `NCode.Identity.Settings.SettingDescriptor.AppliesTo`,
  `NCode.Identity.Settings.DefaultSettingSerializer`, `NCode.Identity.OpenId.Settings.OpenIdSettingScopes`,
  `NCode.Identity.OpenId.Settings.DefaultSettingScopeDataSource`,
  `NCode.Identity.OpenId.Settings.DefaultSettingConfigurationReader`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Settings.DefaultSecurityProfileCatalog`,
  `NCode.Identity.OpenId.Authentication.Clients.Handlers.ClientAssertionClientAuthenticationHandler` and
  `NCode.Identity.OpenId.Management.Endpoints.BaseApiEndpointHandler`.
