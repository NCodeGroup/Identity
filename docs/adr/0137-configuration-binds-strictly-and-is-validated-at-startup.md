# 137. Configuration binds strictly and is validated when the host starts

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The reference host shipped an `appsettings.json` that configured tenant selection under `OpenId:Tenant`
(`Mode: StaticSingle`, `StaticSingle:TenantPath: /`). Tenant selection is bound from `OpenId:TenantResolution`;
`OpenId:Tenant` binds `OpenIdTenantOptions`, which configures how a resolved tenant is materialized and cached and has
no `Mode` or `StaticSingle` member. The configuration binder ignores keys that map to no property, so the host started,
served traffic, and looked healthy while its own tenant configuration was discarded. It only behaved because the
defaults happened to coincide with what it asked for. An operator copying that shape with a non-default value would
have had their setting ignored with no signal at all.

It was not an isolated slip. The `Conformance` launch profile pinned the issuer through
`OpenId:Server:Settings:tenant_issuer`, a setting that no longer exists
([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md)); the pin silently stopped working. A key that
binds to nothing is indistinguishable from an absent key, so every misspelling, every misplaced section, and every key
left behind by a refactor fails the same way: quietly, at the moment it matters.

A related class fails late instead of silently. A tenant-resolution configuration that names an unknown strategy, a
path-based route without the tenant parameter, or an invalid host pattern was only discovered by the first request that
resolved a tenant — after health checks had already reported the instance ready.

## Decision

**A key that maps to no option stops the host from starting, and so does a value the server cannot honor. Both are
checked before the host accepts traffic.**

1. **Strict binding.** Every options type the server binds from configuration is bound with the binder's
   `ErrorOnUnknownConfiguration`, at every depth, through one helper (`BindStrictly`) that names the offending section
   and key. This covers the OpenID section, `BootstrapAdmin`, `Frontend`, `ReverseProxy`, `ConformanceSeed`, and
   `DeveloperInteractiveSeed`.
2. **The OpenID section has one owner.** `AddConfiguration` declares the whole shape of the OpenID section and rejects
   any other subsection:
   - `Server` → `OpenIdServerOptions`, whose `Settings` member carries the server's settings subsection as-is (settings
     are an open vocabulary typed by their descriptors when loaded, and the section is watched for reloads);
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
4. **Unselected strategies are inert.** Only the selected strategy's options are validated. JSON configuration layers
   cannot delete a key from a lower layer, so rejecting another strategy's section would make it impossible for an
   environment overlay to switch strategies.

## Options considered

- **Bind `OpenId:Tenant` as an alias of `OpenId:TenantResolution`.** Rejected: `OpenId:Tenant` already means
  materialization, so the alias would give one key two meanings and leave every other misplaced key silent.
- **Fix only the host's `appsettings.json`.** Rejected: it repairs the instance and keeps the defect class.
- **Strict binding, but fail on first use.** Rejected: an instance passes its readiness checks and fails on live
  traffic, which is the worst time to learn about a configuration error.
- **Reject options for unselected strategies.** Rejected for the layering reason above.

## Consequences

- A misspelled or misplaced key stops the host with an error naming the section, the options type, and the key. The
  reference host and the conformance launch profile use the canonical keys.
- Extensions must not add keys under a section the server binds. A custom tenant strategy binds its own options from
  its own section, not from `OpenId:TenantResolution`.
- The server factory reads its settings from the bound `OpenIdServerOptions.Settings`; there is no separate
  section-name option to keep in step with the binding.
- Setting **names** under `OpenId:Server:Settings` are still an open vocabulary; an unknown name loads as an ad-hoc
  setting. Rejecting unknown names is a separate decision (backlog `F52`).

## References

- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer the static tenant is pinned with.
- [ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md) — the tenant route the validator checks.
- Code: `NCode.Identity.Server.IdentityServerBuilderExtensions.AddConfiguration`,
  `NCode.Identity.Server.Configuration.StrictConfigurationBindingExtensions`,
  `NCode.Identity.OpenId.Tenants.TenantResolutionOptionsValidator`.
