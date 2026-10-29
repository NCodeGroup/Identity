# 108. FAPI security profile via merge-lattice ceiling injection

- **Status:** Accepted (permitted-method enforcement amended by
  [ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md): the ceiling bounds the client's registered
  `token_endpoint_auth_method`, which is the only method the client authenticates with, and an empty ceiling permits
  nothing; profile selection amended by
  [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md): a scope selects a set of profiles through
  `security_profiles`, merged with `Union` so every profile a parent selects stays in force, and every scope — the
  server included — is expanded; redirect URI kinds amended by [ADR-0203](0203-redirect-uris-are-matched-exactly.md):
  the `fapi2` ceiling also caps `redirect_uri_kinds_supported` at `https`, `loopback`, and `private_use`)
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

FAPI 2.0 readiness (issue [#54](https://github.com/NCodeGroup/Identity/issues/54)) needs a way for a tenant or client to opt into the OpenID Foundation **FAPI 2.0
Security Profile** as a single switch, rather than configuring a dozen individual settings correctly and consistently.
The profile constrains many things the OP already models as settings — signing algorithms, required PKCE/PAR, response
types, code/PAR lifetimes, permitted client-authentication methods — plus a few behavioral rules.

Settings already merge down a `server → tenant → client` cascade through a first-class
[lattice-dual merge policy](0038-setting-merge-lattice-duals.md): collection ceilings use `Intersect`, requirement
floors use `Or`, lifetime ceilings use `Min`. Crucially, **those operations are commutative and idempotent**, so the
order in which a constraint enters the cascade does not change the result.

## Decision

**Model the profile as a `security_profile` setting that, at the scope where it is declared, expands into the profile's
concrete ceiling settings; the existing floor/ceiling merge then carries those constraints down the cascade
un-loosenably, so discovery and request-time enforcement read one source of truth.**

- **`security_profile` is an un-loosenable string setting.** Its merge delegate keeps `fapi2` once any scope in the
  cascade selects it (a tenant can mandate the profile and a child cannot clear it); otherwise the child value wins.
  The recognized value today is `fapi2`.

    > **Superseded by [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md).** A merge that knew
    > one profile let a child clear or replace any other, dropping its requirements. The selection is the list
    > `security_profiles`, merged with `Union`, so every registered profile a scope selects stays in force below it, and
    > every selected profile applies.

- **`ISecurityProfileSettingsExpander` expands a scope's own settings before they enter the cascade.** When a scope
  (tenant or client) declares `security_profile=fapi2`, the expander merges the FAPI 2.0 ceiling settings into that
  scope's settings using the normal descriptor merge. Because the FAPI constraints are all expressed with commutative
  merge operations, injecting them at the declaring scope is equivalent to injecting them anywhere, and a more-specific
  scope cannot loosen them. The expander exposes a single `Expand(JsonElement, JsonSerializerOptions)` entry point so
  every persisted-scope load (the tenant factory's initial + refresh paths and the client resolver) deserializes and
  expands in one call and cannot forget to apply the profile.

    > **Superseded by [ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md).** The standalone
    > `ISecurityProfileSettingsExpander` was folded into `ISecurityProfileCatalog`, the single registry of `ISecurityProfile`
    > definitions; the ceiling settings are now contributed by each profile's `ApplyCeilingSettings`, and the catalog still
    > exposes the same `Expand(…)` entry points. The mechanism and guarantees described here are unchanged.

- **The FAPI 2.0 ceiling.** Signing algorithms → `PS256` / `ES256` / `Ed25519` (`Intersect`; `Ed25519` takes effect
  only where the opt-in EdDSA package is registered, and the polymorphic `EdDSA` is excluded
  ([ADR-0272](0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md))); strong client
  authentication only — `private_key_jwt` / `tls_client_auth` / `self_signed_tls_client_auth` (`Intersect` on
  `token_endpoint_auth_methods_supported`); `response_type=code` only and PKCE `S256` only (`Intersect`); require PKCE
  and require PAR (`Or` floors); a 60-second authorization-code ceiling and a 90-second PAR `request_uri` ceiling
  (`Min`).

- **Permitted-method enforcement makes the client-auth ceiling bite.** `DefaultClientAuthenticationService` now rejects
  an authentication whose method is not in the client's effective `token_endpoint_auth_methods_supported` (an empty set
  means unrestricted, per [ADR-0010](0010-supported-settings-unset-means-unrestricted.md)). This is a general control —
  for a non-profiled client the default set lists every registered method, so nothing changes — but under `fapi2` it
  rejects weak (`client_secret_*`) and public (`none`) authentication at the token endpoint, giving confidential-only,
  strong-authentication enforcement for free.

## Consequences

- A tenant or client opts into FAPI 2.0 with one `security_profile=fapi2` setting; discovery automatically advertises
  the constrained metadata, and request-time checks that already read those settings (PKCE, PAR, algorithms, lifetimes)
  are automatically tightened, with no second source of truth to drift.
- A tenant can mandate `fapi2` for all its clients; a client cannot loosen any of the injected ceilings, nor clear the
  profile flag itself.
- New public surface: the `security_profile` setting, the `OpenIdConstants.SecurityProfiles.Fapi2` constant, and
  `ISecurityProfileSettingsExpander` (the latter superseded by `ISecurityProfileCatalog` in
  [ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md)).
- Remaining FAPI behavioral rules not expressible as setting ceilings are follow-ups (issue [#54](https://github.com/NCodeGroup/Identity/issues/54) Phase 3): the
  client-assertion `aud` must equal the issuer; sender-constrained access tokens must be required (DPoP **or** mTLS
  binding, so not a single setting); and a public client must be rejected at the authorization endpoint (the token
  endpoint already rejects it via permitted-method enforcement).
