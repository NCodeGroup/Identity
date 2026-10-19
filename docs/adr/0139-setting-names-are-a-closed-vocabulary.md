# 139. Setting names are a closed vocabulary, and configuration the server cannot honor fails loudly

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

[ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) makes every options section the server
binds strict and validates it when the host starts. Settings sit outside that: the server, each tenant, each client, and
each resource server carry a settings document — keyed by setting name, typed by a registered `SettingDescriptor`, and
merged down the hierarchy ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)) — and those documents come
from configuration (`OpenId:Server:Settings`), from the management API, and from dynamic client registration.

A settings document that names no registered setting is configuration accepted without being honored. When a name has
no descriptor and the reader invents one — a string for a configured value, a type inferred from the JSON token for a
stored one — the setting loads, nothing reads it, and nothing reports it. A misspelled name, a setting left behind by a
refactor, and a setting whose extension is no longer composed all look like working configuration. A pinned issuer
under a retired setting name stops having any effect with no signal.

Values fail the same way. A list setting written as a single configuration value binds to nothing, and an empty array
binds to nothing. A stored value of the wrong type (`"require_pkce": "yes"`) is accepted by the management API and then
fails every request that resolves the client. A management `create` whose `settings` is not an object stores `{}`.

Dynamic client registration stores RFC 7591 metadata the runtime has no descriptor for (`client_name`, `contacts`,
`jwks`, …) and stores the requested `scope` under `scopes_supported`, a setting that no longer exists: scope entitlement
comes from resource servers and client grants ([ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md)),
so the stored scope is never consulted, yet the registration response echoes it as if it were granted.

A handful of configuration inputs outside the settings documents are lenient in the same way: trusted reverse-proxy
entries that do not parse are skipped (the IPv4 parser also accepts shorthand such as `10.0.0` for another address, and
the network parser masks host bits, so `10.0.0.4/8` trusts `10.0.0.0/8`), and a few values are read as single keys
rather than bound, so a misspelled sibling key — or an unknown `DeveloperKeys:Mode`, which falls back to `Ephemeral` —
goes unnoticed. The developer CORS section is never honored in Production at all, because the developer policy is
registered only outside Production.

## Decision

**Setting names are a closed vocabulary: every setting has a registered descriptor, and its value conforms to the
descriptor's type. A setting that cannot be honored is rejected wherever it enters the system — never loaded, stored,
or skipped.** Configuration outside the settings documents follows ADR-0137: bound strictly, validated before the host
accepts traffic.

1. **No ad-hoc descriptors.** Only a registered descriptor types a setting. An extension that reads or writes a setting
   contributes its descriptor through `ICollectionDataSource<SettingDescriptor>`. `ISettingCollection.Set(key, value)`
   throws for a key with no descriptor of that value type.
2. **Configuration is validated when the host starts.** `OpenId:Server:Settings` is read through one reader that rejects
   a name with no descriptor, a value the binder cannot convert, a list setting written as a single value, and a
   single-value setting written as a list or section; an empty array is an empty list. The reader backs an
   `IValidateOptions<OpenIdOptions>` registered with `ValidateOnStart`. A configuration **reload** that cannot be
   honored is rejected with an error log and the previously loaded settings stay in force: a running host cannot be
   stopped, and applying part of a reload would be worse than applying none of it.
3. **Management writes are validated before they are stored.** Creating a server, tenant, client, or resource server,
   and patching its settings, validate the resulting document through `ISettingSerializer.ValidateSettings`. A document
   that cannot be honored — a non-object, an unregistered name, a value of the wrong type, `null` for a setting with no
   default — returns `400` with a validation problem whose `errors` are keyed by setting name, and nothing is stored.
4. **Stored documents fail closed when read.** `ISettingSerializer.DeserializeSettings` throws for a document it would
   reject. A stored setting whose descriptor is gone is a control the operator configured that the server would no
   longer enforce, so loading the rest without it is the unsafe outcome. Retiring or renaming a setting therefore ships
   with a data migration that rewrites the stored documents.
5. **Client metadata is a registered client setting.** The RFC 7591 metadata a registration stores — `client_name`,
   `client_uri`, `logo_uri`, `tos_uri`, `policy_uri`, `contacts`, `jwks`, `software_id`, `software_version` — have
   descriptors and keys like any other client setting. The requested `scope` is not stored; the registration response
   reports the scopes the client may request now, from `IClientScopeService` (RFC 7591 §3.2.1 lets the server replace
   requested metadata). The conformance seed carries no scopes for the same reason.
6. **The remaining lenient inputs are bound and validated.**
   - `ReverseProxy` is registered as options with a validator: a trusted proxy must be an IP address (IPv4 written as
     four decimal octets), a trusted network must be CIDR notation whose address is the network's first address, and
     the forward limit must be at least one.
   - `InteractiveAuthentication` binds `InteractiveAuthenticationOptions` strictly and is validated on start.
   - `DeveloperKeys` binds `DeveloperKeysOptions` strictly when the host composes its services (the mode decides what is
     registered); a mode that names no `DeveloperKeyMode` throws.
   - `Cors` binds `DeveloperCorsOptions` strictly, and each allowed origin must be written as a browser sends it. Because
     the developer policy is never registered in Production, any `Cors` section there is a production-safety violation,
     and `UseDeveloperCors` adds no middleware when the policy is not registered.

## Options considered

- **Keep names open, log a warning for an unknown name.** Rejected: a warning in a startup log is the silent failure of
  ADR-0137 with extra text, and stored documents would still need a policy.
- **Close the vocabulary for configuration only.** Rejected: the management API and registration are the larger write
  surfaces, and an operator's typo there is just as silent.
- **Reject unknown names on write, skip them on read.** Rejected: a stored setting is configuration someone made;
  skipping it on read is exactly the failure this decision removes, and for a security control it silently weakens the
  client. Failing closed surfaces the problem and keeps the control's intent.
- **Keep registration metadata as untyped keys in the settings document.** Rejected: it reopens the vocabulary for one
  writer, and the consent page already reads `client_name` as a setting.
- **Store the requested `scope` as registration metadata.** Rejected: the response would claim scopes the client cannot
  obtain, and it would be a second source of truth beside resource servers and grants.
- **Validate setting values against their domain (allowed values, ranges) in the same change.** Deferred: types are
  what a descriptor declares today; a value-domain rule (for example, that `security_profile` names a registered
  profile) is a descriptor extension recorded as backlog `F53`.

## Consequences

- A configured setting the server cannot honor stops the host with an error naming its configuration path; a management
  write of one returns `400`; a stored one fails the request that loads it rather than being ignored.
- Every setting name the library defines has a descriptor in the composed server, and a test pins it.
- Extensions must register a descriptor for every setting they read or write. Retiring or renaming a setting needs a
  migration of the stored documents.
- The registration response's `scope` reflects the client's current entitlement rather than its request.
- A Production configuration with a `Cors` section fails `AddProductionSafety`, and the reference host starts in
  Production without the developer CORS policy.

## References

- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — strict binding and validation on start,
  which this decision extends to setting names and the remaining configuration inputs.
- [ADR-0026](0026-scope-enforcement-via-resource-servers-and-client-grants.md) — scope entitlement, the reason the
  registration `scope` is derived.
- [ADR-0063](0063-dynamic-client-registration.md) — the registration metadata now stored as registered client settings.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — the settings hierarchy and merge lattice.
- Code: `NCode.Identity.Settings.DefaultSettingSerializer`, `NCode.Identity.OpenId.Settings.DefaultSettingConfigurationReader`,
  `NCode.Identity.OpenId.Settings.ServerSettingsOptionsValidator`, `NCode.Identity.OpenId.Settings.RootSettingsCollectionDataSource`,
  `NCode.Identity.Server.ReverseProxy.ReverseProxyOptionsValidator`,
  `NCode.Identity.Server.DevelopmentEnvironment.DeveloperCorsOptionsValidator`,
  `NCode.Identity.Server.DeveloperKeysConfiguration`, `NCode.Registration.StrictConfigurationBindingExtensions`.
