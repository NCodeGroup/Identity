# 289. A tenant declares its environment type, which selects its guardrails

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

Operators run a customer's dev, test and prod as separate tenants
([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)), but a tenant could not say which one it
is. Nothing distinguished a sandbox from the tenant that serves real users, so nothing could protect the latter more
than the former: a production tenant could be deleted as easily as a scratch one, could register clients with plain
`http` redirect URIs, and a developer could not tell from the login page that they were signing in to a non-production
tenant. Telemetry could not separate the traffic of a load-test tenant from production traffic either.

The family already has the pieces a declaration needs: tenant settings that cascade from the server
([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)), security profiles that accumulate down the cascade as
ceilings, defaults and requirements ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)), and a
tenant branding document that the interactive pages embed
([ADR-0194](0194-a-tenant-brands-its-interactive-surface-through-settings.md)). The open questions were what the
declaration is (free text, or a vocabulary), what it defaults to, where the guardrails apply, and what telemetry may
carry.

## Decision

**A tenant declares a registered environment type in its settings; the type, not free text, selects the guardrails, and a
tenant that declares nothing is `production`.**

1. **Three tenant settings.** `environment_type`, `environment_name` and `deletion_protection`, each applying to the
   tenant scope only ([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)), not discoverable, and merged with
   `Replace`. `environment_type` is the name of a registered environment type; `environment_name` is a free-form display
   name of at most 64 characters with no control characters, empty by default ("sandbox", "qa"), used for display only,
   never for behavior and never in telemetry; `deletion_protection` is a bool, false by default, which the `production`
   profile defaults to true.
2. **The type vocabulary is open.** `ITenantEnvironmentType` (`TypeName`, `IsProduction`, `SecurityProfiles`) is a DI
   strategy collection resolved through `ITenantEnvironmentCatalog` (`TryGetType`, `GetEffectiveType`,
   `SelectImpliedProfiles`). The names are the constants in `OpenIdConstants.TenantEnvironmentTypes` (`development`,
   `testing`, `staging`, `production`), and a package adds a type by registering a class, never by editing the library
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). The setting's domain is the registered type
   names, and the catalog rejects a duplicate or empty name, and a type that selects an unregistered profile, when it is
   composed.
3. **Four built-in types.** `development` and `testing` are not production and select no profile. `staging` is not
   production but selects the `production` profile, because staging must mirror production's rules; it still shows the
   banner. `production` is production and selects the `production` profile.
4. **The default is `production`.** A tenant that declares nothing gets the guardrails. Defaulting to `development`
   would fail open: a forgotten declaration would silently drop them.
5. **The guardrails are a registered security profile.** `ProductionSecurityProfile`, named `production`
   (`OpenIdConstants.SecurityProfiles.Production`), sets the ceilings `redirect_uri_kinds_supported` to https, loopback
   and private_use (it drops plain `http`) and `registration_endpoint_allow_anonymous` to false, defaults
   `deletion_protection` to true, and has no requirements. Because it is an ordinary profile, any scope may also select
   it directly through `security_profiles`.
6. **The type's profiles are added at read time.** `IEffectiveSettingsResolver.ReadScopeSettings(parentSettings,
   scopeName, settings)` deserializes a scope's document, calls `ITenantEnvironmentCatalog.SelectImpliedProfiles` and
   then `ISecurityProfileCatalog.Expand`. The catalog is consulted only at a scope that `environment_type` applies to,
   which is the tenant; the type is the tenant's own value, else the parent's effective value, else the default.
   `environment_type` merges with `Replace`, so a ceiling applied at the server scope, where the default sits in the
   baseline, would bind development tenants too. The guardrails must follow the tenant's effective type, so they are
   added where that type is known.
7. **Deletion protection.** While `deletion_protection` is true, `DELETE /api/tenants/{tenantId}` is refused with `409`.
   The operator turns it off first, which is a deliberate act.
8. **A non-production tenant shows a banner.** The interactive pages embed the tenant's branding JSON; a non-production
   type adds `"environment":{"type":...,"name":...}` (`InteractiveBranding.Environment`, omitted for a production
   type), and the `apps/auth` bundle shows a warning `Alert`, "<name or type> environment — not for production use"
   (`EnvironmentBanner`).
9. **Telemetry carries only the registered type name.** The name is bounded by registration
   ([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)); `environment_name` is
   never emitted. The audit record gains the field `tenant_environment_type`, written after `tenant_id`
   ([ADR-0283](0283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)), as
   `IAuditEvent.TenantEnvironmentType` and `AuditRecordFields.TenantEnvironmentType`, and
   `AuditRecordWriter.SchemaVersion` moves from 1.0 to 1.1, an additive change. The metric attribute
   `ncode.identity.tenant.environment_type` is added to `ncode.identity.events.published` (for audit events that carry
   it) and to `ncode.identity.rate_limiting.requests`. Protocol audit events (`DefaultAuditEventRecorder`) set it;
   management audit events leave it null.
10. **The hosts declare their own environments.** `appsettings.Development.json`, which the integration-test host also
    loads, declares `OpenId:Server:Settings:environment_type` as `development`; `appsettings.Conformance.json` declares
    `testing`, because the developer seed uses `http://localhost` redirect URIs and conformance needs anonymous
    registration.

## Options considered

- **A free-form environment name only.** Rejected: nothing may key behavior or metrics off arbitrary text, so a name
  alone could not select guardrails or label telemetry. It survives as `environment_name`, display only.
- **One setting whose types carry aliases (`sandbox` resolving to `development`).** Rejected: it is extra machinery, and
  "sandbox" means different things to different companies. An operator who wants another name registers a type or uses
  `environment_name`.
- **A `PersistedTenant` column and migration.** Rejected: the settings document already cascades and is validated, so
  settings need no migration.
- **Applying the production ceilings at the server scope.** Rejected: `environment_type` merges with `Replace` and its
  default sits in the baseline, so a server-scope ceiling would bind development tenants too (decision 6).
- **Defaulting to `development`.** Rejected: it fails open, and a forgotten declaration would drop the guardrails
  (decision 4).

## Consequences

- Existing tenants become `production` on upgrade, because they declare nothing: they gain the ceilings and, by
  default, deletion protection. An operator who runs a non-production tenant declares its type.
- The development and conformance host overlays declare a non-production type, so local development and conformance
  runs keep working; any other host that runs non-production tenants must declare it at the server or tenant.
- A materialized tenant computes its added profiles when it reads its own document, so a change to the server-wide
  `environment_type` alone reaches a cached tenant within `OpenIdTenantOptions.TenantCacheLifetime` (15 minutes by
  default). One-shot resolutions, such as management and coherence checks, see it at once. A follow-up tracks this.
- The audit record schema is 1.1; the new field is additive, so a reader of 1.0 records keeps working. Management audit
  events leave `tenant_environment_type` null until a follow-up stamps them.
- `IEffectiveSettingsResolver.ReadScopeSettings` takes the parent settings, a breaking change to the public API,
  acceptable before release ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- A package adds an environment type by registering a class; a type that selects an unregistered profile fails when the
  catalog is composed rather than when a tenant is read.

## References

- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — pre-release posture
- [ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md) — hosted platform environment model
- [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — security profiles accumulate down the
  cascade
- [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md) — settings declare their scopes
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed
- [ADR-0194](0194-a-tenant-brands-its-interactive-surface-through-settings.md) — tenant branding
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — bounded metric
  dimensions
- [ADR-0198](0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) — plan-tier quotas; dev and test
  tenants run on the production platform
- [ADR-0283](0283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) — audit records
