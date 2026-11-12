# 1139. Setting names are a closed vocabulary changed outright, and settings the server cannot honor fail loudly

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md)

## Context

[ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)
binds every options section strictly and validates it when the host starts. Settings sit beside that: the platform, each
team, tenant, client and resource server carry a settings document, keyed by setting name and typed by a registered
`SettingDescriptor`, and those documents come from configuration, from the management API and from dynamic client
registration.

A settings document that names no registered setting is configuration accepted without being honored. When a reader
invents a descriptor for an unknown name, the setting loads, nothing reads it and nothing reports it: a misspelled name,
a setting left behind by a refactor and a setting whose extension is not composed all look like working configuration.
Values fail the same way. A list written as a single configuration value binds to nothing, a stored value of the wrong
type is accepted by the management API and then fails every request that resolves the client, and a `create` whose
`settings` is not an object stores `{}`.

A stored control whose descriptor is gone is a control the operator configured that the server would not enforce, so
loading the rest of the document without it is the unsafe outcome.

Dynamic client registration carries RFC 7591 metadata and a requested `scope`. The metadata needs descriptors to be
stored, and scope entitlement comes from resource servers and client grants, so a stored `scope` would never be
consulted.

The project is pre-release: no stored document or configuration file has a reader that must keep working. A setting
that changes therefore changes outright, and the design carries no retired state for a setting nothing reads.

## Decision

**Setting names are a closed vocabulary: every setting has a registered `SettingDescriptor`, and its value conforms to
the descriptor's type. A setting that cannot be honored is rejected wherever it enters the system, and is never loaded,
stored or skipped.**

1. **No ad-hoc descriptors.** Only a registered descriptor types a setting. An extension that reads or writes a setting
   contributes its descriptor through `ICollectionDataSource<SettingDescriptor>`, and no reader invents one.
   `ISettingCollection.Set(key, value)` throws for a name with no descriptor or a descriptor of another value type.
2. **What cannot be honored.** The settings are rejected for an unregistered name, a value the binder cannot convert, a
   list written as a single value or the reverse, a document that is not an object, and `null` for a setting with no
   default.
3. **Configuration is validated when the host starts.** The settings under `OpenId:Platform:Settings` are read through
   `DefaultSettingConfigurationReader`, which backs `PlatformSettingsOptionsValidator`, an
   `IValidateOptions<OpenIdOptions>` registered with `ValidateOnStart`. A configuration reload that cannot be honored is
   rejected with an error log and the settings already loaded stay in force: a running host cannot be stopped, and
   applying part of a reload would be worse than applying none of it.
4. **Management writes are validated before they are stored.** The platform, team, tenant, client and resource-server
   creates and settings updates validate the written document alone through `ISettingSerializer.ValidateSettings`. A
   failure answers `400` with a validation problem keyed by setting name, and nothing is stored.
5. **Stored documents fail closed when read.** `ISettingSerializer.DeserializeSettings` throws for a document it cannot
   honor. A protocol request that loads such a document fails, and a management request answers `409`
   ([ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)). A stored
   document that names an unregistered setting stays `409` until a settings write replaces it, and
   `build/dev-db-reset.ps1` resets a developer database that holds one.
6. **A setting changes outright.** Until the first stable release a setting is renamed, removed, or narrowed in scope or
   domain by editing or deleting its descriptor and the code that reads it. No descriptor stays registered for a name
   nothing reads, and no migration rewrites stored documents
   ([ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)).
   `ISettingSerializer` has one validation member, `ValidateSettings`, which needs no stored document, and a
   `SettingDescriptor` has no retired state. How a released setting retires or is renamed across a rolling deploy is
   decided with the first stable release (issue #535).
7. **Registration metadata is typed.** The RFC 7591 metadata a registration stores are registered client settings:
   `client_name`, `client_uri`, `logo_uri`, `tos_uri`, `policy_uri`, `contacts`, `software_id` and `software_version`. A
   registered `jwks` is imported as the client's keys, not stored as a setting. The requested `scope` is not stored; the
   registration response reports the scopes the client may request, from `IClientScopeService` (RFC 7591 section 3.2.1).
8. **Properties are not settings.** No setting names a tenant's mutual-TLS endpoint alias, which is the tenant property
   `PersistedTenant.MtlsEndpointAliasAuthority`
   ([ADR-1165](1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).
9. **One outcome at every entry point.** A value's domain
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)) and the scopes a setting applies to
   ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)) are checked at the same entry points with the same
   outcome. A composed-server test pins every setting name the library defines to a registered descriptor.

## Options considered

- **Open names with a warning.** Rejected: a misspelled security control keeps working as if configured, and a warning
  in a log is the only sign.
- **Close configuration only.** Rejected: the management API and registration are the larger entry points, and a
  document stored through them fails later, in the request that resolves the client.
- **Reject on write but skip on read.** Rejected: a stored control the server would not enforce is the unsafe outcome,
  so a read fails closed.
- **Untyped registration metadata.** Rejected: metadata without descriptors is stored without being honored, which is
  the failure this record closes.
- **Store the requested `scope`.** Rejected: entitlement comes from resource servers and client grants, so the stored
  value is never consulted and a response that echoes it reads as granted.
- **A retired-descriptor state, or one retired descriptor per removed pre-release setting.** Rejected: it keeps a name
  registered that nothing reads, and every removal adds a descriptor, a reason and a later cleanup for stored documents
  that no reader depends on.
- **Chosen:** a closed vocabulary of registered descriptors, rejection wherever a setting enters, a fail-closed read,
  and outright change of a setting.

## Consequences

- A configured setting the server cannot honor stops the host and names its configuration path, a management write of
  one answers `400`, and a stored one fails the request that loads it.
- A vocabulary change touches nothing under the host's `Migrations/` folder.
- A misspelled name, a stale name and a setting whose extension is not composed are caught at the first start or write
  instead of passing as working configuration.
- A developer database that holds a document naming a removed setting answers `409` until a settings write replaces
  the document or `build/dev-db-reset.ps1` resets the database.
- Tests cover the reader, the validator, `ValidateSettings` on each create and update, the fail-closed read, and the
  composed-server test that pins each defined setting name to a descriptor.

## References

- [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md)
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md),
  [ADR-1165](1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md),
  [ADR-1180](1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md),
  [ADR-1245](1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md)
- RFC 7591 section 3.2.1; issue #535
- `DefaultSettingConfigurationReader`, `PlatformSettingsOptionsValidator`, `ISettingSerializer`, `SettingDescriptor`,
  `ISettingCollection`, `IClientScopeService`, `PersistedTenant.MtlsEndpointAliasAuthority`, `build/dev-db-reset.ps1`
