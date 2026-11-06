# 1137. Configuration binds strictly and is validated when the host starts

- **Status:** Accepted (extended by [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md): setting names are a
  closed vocabulary, and the remaining lenient configuration inputs are bound and validated; extended by
  [ADR-1160](1160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md): each tenant strategy validates its
  own options, by the same rules that judge the tenants written while the host runs; amended by
  [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md): the `OpenId:Platform`
  and `OpenId:Team` sections bind strictly and are validated at startup; amended by
  [ADR-1447](1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md):
  forwarded-header trust binds strictly from the `ForwardedHeaders` section)
- **Date:** 2026-10-18
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md)

## Context

[project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md) stops the
host on a configuration key that maps to no option or a value the application cannot honor: options bind strictly, each
section has one owner, and every options type is validated when the host starts. This record applies it to the OpenID
section and the reference host.

The reference host shipped an `appsettings.json` that configured tenant selection under `OpenId:Tenant`
(`Mode: StaticSingle`, `StaticSingle:TenantPath: /`). Tenant selection is bound from `OpenId:TenantResolution`;
`OpenId:Tenant` binds `OpenIdTenantOptions`, which configures how a resolved tenant is materialized and cached and has
no `Mode` or `StaticSingle` member. The configuration binder ignores keys that map to no property, so the host started,
served traffic, and looked healthy while its own tenant configuration was discarded. It only behaved because the
defaults happened to coincide with what it asked for. An operator copying that shape with a non-default value would
have had their setting ignored with no signal at all.

It was not an isolated slip. The `Conformance` launch profile pinned the issuer through
`OpenId:Server:Settings:tenant_issuer`, a setting that no longer exists
([ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md)); the pin silently stopped working. A key that
binds to nothing is indistinguishable from an absent key, so every misspelling, every misplaced section, and every key
left behind by a refactor fails the same way: quietly, at the moment it matters.

A related class fails late instead of silently. A tenant-resolution configuration that names an unknown strategy, a
path-based route without the tenant parameter, or an invalid host pattern was only discovered by the first request that
resolved a tenant — after health checks had already reported the instance ready.

## Decision

**The server applies project-template ADR-0106 to every options type it binds:**

1. **Strict binding.** Every options type the server binds from configuration is bound with the binder's
   `ErrorOnUnknownConfiguration`, at every depth, through one helper (`BindStrictly`) that names the offending section
   and key. This covers the OpenID section, `BootstrapAdmin`, `Frontend`, `ReverseProxy`, `ConformanceSeed`, and
   `DeveloperInteractiveSeed`.
2. **The OpenID section has one owner.** `AddConfiguration` declares the whole shape of the OpenID section and rejects
   any other subsection:
   - `Server` → `OpenIdServerOptions`, whose `Settings` member carries the server's settings subsection as-is (each
     child is a setting typed by its registered descriptor — a closed vocabulary validated on start,
     [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) — and the section is watched for reloads);
   - `Tenant` → `OpenIdTenantOptions` — how a resolved tenant is materialized and cached;
   - `TenantResolution` → `TenantResolutionOptions` — which tenant a request selects, and the route it is served under.
     **This is the canonical location of tenant selection**;
   - `MutualTls` → `MutualTlsForwardingOptions`.

   The mutual-TLS options are bound here rather than by the authentication package, so a host's chosen section name
   applies to every subsection of it.
3. **Validated on start.** Each bound options type is registered with `ValidateOnStart`, so binding runs as the host
   starts rather than on first use. `TenantResolutionOptions` also has an `IValidateOptions` validator that checks the
   selected strategy's options: a literal path for the static and host strategies, the tenant parameter in the
   path strategy's route, a compilable host pattern, and a static issuer that is an absolute `http`/`https` URL ending in
   the tenant path. An unknown strategy code fails when the endpoints are mapped, before the host starts.

## Options considered

- **Bind `OpenId:Tenant` as an alias of `OpenId:TenantResolution`.** Rejected: `OpenId:Tenant` already means
  materialization, so the alias would give one key two meanings and leave every other misplaced key silent.

## Consequences

- A misspelled or misplaced key stops the host with an error naming the section, the options type, and the key. The
  reference host and the conformance launch profile use the canonical keys.
- Extensions must not add keys under a section the server binds. A custom tenant strategy binds its own options from
  its own section, not from `OpenId:TenantResolution`.
- The server factory reads its settings from the bound `OpenIdServerOptions.Settings`; there is no separate
  section-name option to keep in step with the binding.
- Setting **names** under `OpenId:Server:Settings` are a closed vocabulary validated with the rest of the configuration,
  and the `ReverseProxy`, `InteractiveAuthentication`, `DeveloperKeys`, and `Cors` sections are bound and validated the
  same way ([ADR-1139](1139-setting-names-are-a-closed-vocabulary.md)).

## References

- [ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer the static tenant is pinned with.
- [ADR-1138](1138-protocol-endpoints-are-served-under-the-tenant-route.md) — the tenant route the validator checks.
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md) — setting names and the remaining configuration inputs.
- Code: `NCode.Identity.Server.IdentityServerBuilderExtensions.AddConfiguration`,
  `NCode.Registration.StrictConfigurationBindingExtensions`,
  `NCode.Identity.OpenId.Tenants.TenantResolutionOptionsValidator`.
