# 1289. A tenant declares its environment type, which selects its guardrails

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0119](project-template/0119-production-refuses-development-only-settings.md)

## Context

Operators run a customer's dev, test and prod as separate tenants
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)),
so a tenant has to say which one it is. Without a declaration nothing distinguishes a sandbox from the tenant that
serves real users, so nothing protects the latter more than the former: a production tenant could be deleted as easily
as a scratch one, could register clients with plain `http` redirect URIs, and a developer could not tell from the login
page that they are signing in to a non-production tenant. Telemetry could not separate the traffic of a load-test
tenant from production traffic either.

The family already has the pieces a declaration needs: tenant settings that cascade
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), security
profiles that accumulate down the cascade as ceilings, defaults and requirements
([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)),
and a tenant branding document that the interactive pages embed
([ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)). The open questions are what the
declaration is (free text or a vocabulary), what it defaults to, where the guardrails apply, and what telemetry may
carry.

## Decision

**A tenant declares a registered environment type in its settings; the type, not free text, selects the guardrails, and
a tenant that declares nothing is `production`.**

1. **Three tenant settings.** `environment_type`, `environment_name` and `deletion_protection` each apply to the tenant
   scope, are not discoverable and are merged with `Replace`. A platform or team settings document may set them as the
   defaults its tenants inherit
   ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).
   `environment_type` is the name of a registered environment type. `environment_name` is a free-form display name of at
   most 64 characters with no control characters ("sandbox", "qa"); it is used for display only, never for behavior and
   never in telemetry. `deletion_protection` is a bool, `false` by default.
2. **The type vocabulary is open.** `ITenantEnvironmentType` (`TypeName`, `IsProduction`, `SecurityProfiles`) is a DI
   strategy collection resolved through `ITenantEnvironmentCatalog` (`TryGetType`, `GetEffectiveType`,
   `SelectImpliedProfiles`). The names are the constants in `OpenIdConstants.TenantEnvironmentTypes`, and a package adds
   a type by registering a class, never by editing the library
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). The catalog rejects an empty or duplicate name,
   and a type that selects an unregistered profile, when it is composed.
3. **Four built-in types.** `development` and `testing` are not production and select no profile. `staging` is not
   production but selects the `production` profile, because staging mirrors production's rules, and it shows the banner.
   `production` is production and selects the `production` profile.
4. **The default is `production`.** A tenant that declares nothing gets the guardrails, so the default fails closed.
5. **The guardrails are a registered security profile.** `ProductionSecurityProfile`, named `production`
   (`OpenIdConstants.SecurityProfiles.Production`), sets the ceilings `redirect_uri_kinds_supported` to `https`,
   `loopback` and `private_use` (it drops plain `http`) and `registration_endpoint_allow_anonymous` to `false`, defaults
   `deletion_protection` to `true`, and has no requirements. It is an ordinary profile, so any scope may also select it
   through `security_profiles`.
6. **The type's profiles are added at read time.** `IEffectiveSettingsResolver.ReadScopeSettings(parentSettings,
   scopeName, settings)` calls `ITenantEnvironmentCatalog.SelectImpliedProfiles` before
   `ISecurityProfileCatalog.Expand`, only at the tenant scope, with the tenant's own type, else its parent's effective
   value, else the default. A ceiling applied at the platform would bind development tenants too, because the type
   merges with `Replace`; the guardrails follow the tenant's effective type, so they are added where that type is known.
7. **Deletion protection.** While `deletion_protection` is `true`, `DELETE /api/tenants/{tenantId}` answers `409`. The
   operator turns it off first, which is a deliberate act.
8. **A non-production tenant shows a banner.** A non-production type adds `"environment":{"type":…,"name":…}`
   (`InteractiveBranding.Environment`) to the branding JSON the interactive pages embed, and `apps/auth`'s
   `EnvironmentBanner` shows "<name or type> environment — not for production use".
9. **Telemetry carries only the registered type name**, which registration bounds
   ([ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)).
   The audit field `tenant_environment_type` follows `tenant_id` in audit schema `1.0`
   ([ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)), as
   `IAuditEvent.TenantEnvironmentType` and `AuditRecordFields.TenantEnvironmentType`. The metric attribute
   `ncode.identity.tenant.environment_type` is on `ncode.identity.events.published` and
   `ncode.identity.rate_limiting.requests`. `DefaultAuditEventRecorder` stamps protocol events;
   `DefaultManagementAuditRecorder` resolves the tenant's effective type once per tenant per request, and a tenant
   deletion records the type it had. Platform and team events leave the field null.
10. **The hosts declare their own environments.** `appsettings.Development.json` declares
    `OpenId:Platform:Settings:environment_type` as `development`, and `appsettings.Conformance.json` as `testing`,
    because the conformance suite registers clients anonymously, which the `production` profile forbids.
11. **A Production host refuses a non-production platform-wide type.** In the Production host environment
    `AddProductionSafety` (`ProductionEnvironmentTypeValidator`) refuses to start when the configured platform
    `environment_type` names a registered type whose `IsProduction` is `false`; the registered types decide, so a
    contributed type is judged by its own flag. Non-production tenants in a Production host declare their type on the
    tenant.
12. **A changed platform reaches cached tenants at once.** A platform settings change clears every cached team and
    tenant on the replica, so the implied profiles follow immediately
    ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)); a replica that missed it converges within
    `TenantCacheLifetime`.
13. **The management API and console show each tenant's environment.** `TenantResource.Environment`
    (`TenantEnvironmentResource`: `type`, `name`, `isProduction`) is resolved after authorization and is best-effort: it
    is omitted rather than answering `409` when stored settings are unreadable or name an unregistered type
    ([ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)),
    because the operator needs the list to find the tenant to repair. An audit record's resource values omit it. The
    `apps/mgmt` tenant list highlights non-production tenants and filters by type.

## Options considered

- **A free-form environment name only.** Rejected: nothing may key behavior or metrics off arbitrary text, so a name
  alone cannot select guardrails or label telemetry. It survives as `environment_name`, display only.
- **Type aliases (`sandbox` resolving to `development`).** Rejected: it is extra machinery, and "sandbox" means
  different things to different companies. An operator who wants another name registers a type or uses
  `environment_name`.
- **A `PersistedTenant` column.** Rejected: the settings document already cascades and is validated, so settings need
  no schema change.
- **Production ceilings at the platform.** Rejected: `environment_type` merges with `Replace`, so a platform ceiling
  would bind development tenants too (decision 6).
- **`development` as the default.** Rejected: it fails open, and a forgotten declaration would drop the guardrails
  (decision 4).
- **Refusing non-production types in every host.** Rejected: development and conformance hosts declare one on purpose
  (decision 10); only the Production host environment refuses it (decision 11).
- **Passing the type from each management handler.** Rejected: dozens of call sites would each have to remember it;
  resolving it in the recorder stamps every event that names a tenant (decision 9).
- **Chosen:** a registered, open type vocabulary whose production type is the default and selects a registered profile
  at read time.

## Consequences

- A tenant that declares nothing is `production` and has the ceilings and deletion protection. An operator who runs a
  non-production tenant declares its type.
- The development and conformance host overlays declare a non-production type, so local development and conformance
  runs work; any other host that runs non-production tenants declares it at the platform, team or tenant.
- A platform `environment_type` change reaches cached tenants at once on every replica that sees it, at the cost of
  materializing each tenant again on its next request.
- A management audit event that names a tenant costs one read of the tenant's settings per tenant per request.
- A package adds an environment type by registering a class; a type that selects an unregistered profile fails when the
  catalog is composed rather than when a tenant is read.

## References

- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)
  — hosted platform environment model
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)
  — security profiles accumulate down the cascade
- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md) — settings
  declare their scopes
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed
- [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md) — tenant branding
- [ADR-1197](1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md) —
  bounded metric dimensions
- [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) — audit records
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — platform and teams
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) — settings reconciliation
- [ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)
  — unreadable stored settings
