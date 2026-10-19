# 109. FAPI 2.0 behavioral rules and discovery/validation details

- **Status:** Accepted (confidential-client rule amended by
  [ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md): a client that registers no
  `token_endpoint_auth_method` is a `client_secret_basic` client, so only `none` registers a public client; the profile
  registry amended by [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md): the catalog resolves
  every profile a scope's effective `security_profiles` selects, runs all of their requirements, and a profile
  contributes its ceiling through `ISecurityProfileCeilingBuilder`; the sender-constrained-token rule amended by
  [ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md): it is the ceiling
  `access_token_sender_constraint_required = true`, and `SenderConstrainedAccessTokenRequirement` is removed)
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

FAPI 2.0 readiness (backlog `F16`) is implemented in phases. The merge-lattice ceiling injection
([ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md)) covers every FAPI constraint that can be
expressed as a floor/ceiling setting (algorithms, PKCE/PAR, response types, lifetimes, permitted client-auth methods).
What remains are the FAPI behavioral rules that are **not** expressible as a single setting ceiling — they depend on
request-time state or compare values the merge lattice does not relate — plus a handful of discovery/validation details
the OP must get right for a relying party to trust it.

The rules in scope:

- The `client_assertion` audience under `fapi2` must equal the **issuer** string, not the token-endpoint URL.
- A **sender-constrained** access token (DPoP **or** mutual-TLS) must be **required** under `fapi2` — this is the
  disjunction of two independent bindings, so it is a request-time check rather than one setting.
- A **public client** must be rejected at the **authorization** endpoint under `fapi2` (the token endpoint already
  rejects it via permitted-method enforcement from ADR-0108).
- The OP must **advertise** `authorization_response_iss_parameter_supported` (RFC 9207) now that the `iss` parameter is
  emitted in every authorization response.
- Signed request objects and client assertions must be validated with a **tight future tolerance** (FAPI 2.0 caps the
  `nbf`/`iat` clock skew at 60 seconds).
- The authorization response redirect must use **303 See Other** (not 302), there must be **no 307**, the authorization
  code must be **single-use**, and `redirect_uri` must be **required** and exact-matched.

## Decision

**Model a security profile's behavioral rules as an open, extensible registry of requirement objects — not a fixed set
of `if (securityProfile == "fapi2")` checks sprinkled through the handlers. Each requirement is a self-contained rule
object that carries both its definition and its assertion; a profile composes its requirements; and each enforcement
point resolves and runs the active profile's requirements for its phase through one catalog call. Express the clock-skew
tightening through the same merge-lattice ceiling mechanism as the other FAPI constraints (ADR-0108); and correct the
redirect status to the spec-required 303 unconditionally (it is correct for every client, not only FAPI).**

- **The profile registry.** A profile is an `ISecurityProfile` (its activating `security_profile` name, its ceiling
  settings via `ApplyCeilingSettings`, and its composed `Requirements`). `ISecurityProfileCatalog` is the single
  registry over the registered `ISecurityProfile` strategy collection; it resolves the profile a scope's effective
  `security_profile` selects, expands that profile's ceiling settings into the merge lattice (subsuming the former
  `ISecurityProfileSettingsExpander`, now deleted), and runs its requirements. `Fapi2SecurityProfile` is the one place
  that composes the FAPI 2.0 ceiling **and** the three behavioral requirement objects below.

- **Requirements are an open, typed set (the key extensibility choice).** `ISecurityProfileRequirement` is an open
  marker; `ISecurityProfileRequirement<TContext>` adds a strongly typed `EvaluateAsync(TContext, …)` seam for one
  enforcement **phase**, and a named alias (`IAuthorizationRequestProfileRequirement`, `ITokenRequestProfileRequirement`,
  `IClientAuthenticationProfileRequirement`) specializes it per phase for readability. Each enforcement point calls
  `catalog.EvaluateRequirementsAsync(settings, context, ct)`; the `TContext` type selects which of the active profile's
  requirements run, in order, and a requirement throws an `OpenId` exception to reject. This is the ASP.NET Core
  Authorization `IAuthorizationRequirement`/handler shape adapted to the OP: **a new requirement kind is purely additive**
  (a new class a profile includes — no change to the catalog, the marker, existing profiles, or any enforcement site),
  and a new profile is one new `ISecurityProfile` registration. A fixed `SecurityProfileRequirements` flag record was
  considered and rejected: it is closed over requirement kinds (a third party could not introduce one the core did not
  predefine), whereas the typed-requirement registry is backward- and forward-compatible by construction.

- **The three FAPI behavioral requirements.** `ConfidentialClientRequirement` (authorization-request phase) rejects a
  public client — one whose `token_endpoint_auth_method` is unset or `none` — with `unauthorized_client`, closing the
  front channel symmetrically with the token endpoint's permitted-method ceiling (ADR-0108).
  `SenderConstrainedAccessTokenRequirement` (token-request phase) rejects with `invalid_request` a token request that
  carries **neither** a DPoP `jkt` **nor** a mutual-TLS `x5t#S256` confirmation, keeping the two bindings interchangeable
  without inventing a "require binding" setting. `IssuerOnlyAssertionAudienceRequirement` (client-authentication phase)
  rejects a `client_assertion` whose `aud` is anything other than the issuer identifier (FAPI 2.0 §5.3.2.1), tightening
  the base handler's `[issuer, token-endpoint URL]` acceptance so the assertion cannot be replayed against a
  differently-named endpoint of the same OP.

- **Advertise `authorization_response_iss_parameter_supported`.** A new boolean setting (default `true`, standard-
  discoverable, `Or` floor) advertises the RFC 9207 behavior the OP already performs — the authorization ticket sets
  `iss` to the tenant issuer on every response. The flag is advertised, not conditional: a relying party can always
  validate `iss`.

- **Clock-skew tightening via the merge lattice.** The FAPI 2.0 ceiling (ADR-0108) now also injects a 60-second
  `clock_skew` ceiling (`Min`), so a `fapi2` scope validates signed request objects / client assertions with at most a
  60-second future tolerance and a child may only reduce it. Making this bite required a correctness fix: the
  `client_assertion` handler and the JAR request-object loader previously built `ValidateJwtParameters` **without**
  setting `ClockSkew`, so they silently used the validator's 5-minute default and ignored the operator-configured
  `clock_skew` setting entirely. Both now read the client's effective `clock_skew` setting, so the setting is honored
  for all tenants (not only FAPI) and FAPI's 60-second ceiling flows through automatically.

- **303 See Other for the authorization response.** `DefaultAuthorizationResultExecutor.ExecuteUsingRedirect` now emits
  `303 See Other` with an explicit `Location` header instead of the framework's default `302 Found`. 303 forces the
  user-agent to `GET` the `redirect_uri` regardless of the original request method, which is the correct and
  spec-required behavior (FAPI 2.0 §5.2.2 / OAuth 2.1); it is applied to every client, not gated on `fapi2`. The
  interactive login **challenge** — a separate ASP.NET Core `Challenge` redirect to the login UI — is unchanged and
  remains a 302; only the authorization **response** back to the client is a 303. No code path emits a 307. The
  authorization code is already single-use (atomic `ConsumeOnceOrDefault`), and `redirect_uri` is already required
  (`ValidateRequestMessage`) and exact-matched against the registered set.

## Consequences

- A `fapi2` client gets issuer-scoped assertion audiences, mandatory sender-constrained tokens, front-channel public-
  client rejection, and a 60-second signed-JWT future tolerance — all driven by the one `security_profile` setting from
  ADR-0108, with no new per-rule settings.
- **All FAPI behavioral logic is consolidated and discoverable.** Everything a profile requires — its ceiling settings
  and its request-time requirement objects — lives in its `ISecurityProfile` definition, not scattered across handlers.
  The enforcement points are profile-agnostic one-liners, and adding a FAPI 1.0 profile, a custom organizational
  profile, or a brand-new requirement kind is additive, with no edits to existing code.
- The authorization response is now a 303 for every client. Any consumer or test that asserted the old 302 for the
  authorization **response** (the error-redirect-to-client path) must assert 303; the login-challenge redirect stays 302.
- New public surface (replacing the removed `ISecurityProfileSettingsExpander`): `ISecurityProfile`,
  `ISecurityProfileRequirement` and its generic `ISecurityProfileRequirement<TContext>`, `ISecurityProfileCatalog`, the
  three phase requirement interfaces and their evaluation contexts, and the `authorization_response_iss_parameter_supported`
  setting name/key.
- The latent bug where `clock_skew` was ignored by JWT validation is fixed, so operators who configured a non-default
  `clock_skew` now have it honored for client assertions and request objects.
- Not implemented here (backlog `F16` follow-up): an explicit "no CORS on the authorization endpoint" guard
  (FAPI §5.2.3). The repository ships only a development-only, explicit-origin CORS policy that is not applied to the
  authorization endpoint in production, so there is no current exposure; a defense-in-depth guard that asserts no CORS
  policy can ever attach to the front-channel endpoints remains a low-priority hardening item.
