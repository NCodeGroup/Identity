# 91. `acr` emission via a replaceable resolver, and conditional `auth_time`

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** NCode Group

## Context

Two OpenID Connect authentication-context claims were still unimplemented, each blocked on "tracking that does not
yet exist" per the token-claims handlers (issue [#49](https://github.com/NCodeGroup/Identity/issues/49)):

- **`acr` (Authentication Context Class Reference).** OpenID Connect Core 1.0 §2 defines `acr` as an opaque string,
  agreed between the OP and the relying party, that identifies the authentication context class the authentication
  satisfied. The spec **deliberately defers the meaning and the full set of `acr` values to the deployment** — there is
  no universal registry beyond a handful of conventions (the OIDC "`0`" value for "no confidence", the widely-used PAPE
  policy URNs such as `http://schemas.openid.net/pape/policies/2007/06/multi-factor`). Nothing in the codebase recorded
  which authentication context a login satisfied, so `acr` was never emitted. Relatedly, the `amr` (Authentication
  Methods References, RFC 8176) claim was wired through the token pipeline but **never actually recorded** at local sign
  in or ROPC — the only two authentication entry points — so it, too, was effectively absent.
- **`auth_time`.** `DefaultTokenService` force-synthesized `auth_time` into **both** the ID token and the access token
  on every issuance (`// TODO: … should we do this conditionally?`). OpenID Connect Core 1.0 makes `auth_time`
  **mandatory** in the ID token only when a `max_age` request is made or `auth_time` is requested as an essential
  claim; otherwise its inclusion is OPTIONAL. Unconditional forced synthesis (from the cookie's `IssuedUtc` when the
  principal carried no real `auth_time`) was both more than the spec requires and, for the access token (RFC 9068, where
  `auth_time` is only "included when available"), the wrong default.

The session subsystem ([ADR-0074](0074-session-management-and-logout.md)) already carries `auth_time`, `amr`, and `idp`
on the `SessionGrant`, so "authentication-context tracking" is a natural extension of it rather than a new subsystem.

## Decision

**Record the authentication context (`amr` + `acr`) at the authentication entry points, flow it through the existing
session and subject-claims pipeline exactly as `amr`/`idp`/`auth_time` already flow, resolve the `acr` value through a
replaceable seam with a sensible default, and make `auth_time` emission conditional and settings-governed.**

Concretely:

- **`amr` is recorded where authentication happens.** The local-account interactive verifier and the ROPC subject
  handler now stamp `amr=["pwd"]` (RFC 8176) onto the principal — the real, immediately-useful authentication-context
  signal that was previously missing. Only the authenticating component knows what methods it performed, so each owns
  its own `amr`.

- **`acr` is resolved through a replaceable service seam.** Because OpenID Connect defers the `acr` policy to the
  deployment, `acr` determination is an extensibility point: `IAuthenticationContextReferenceResolver` (an **async**
  `TryAddSingleton` service, DI-overridable) maps the satisfied authentication methods (plus the `idp` and the effective
  settings) to an `acr` value, or `null` to omit the claim. The default implementation asserts the PAPE multi-factor
  URN when the methods indicate multiple factors, otherwise the configured `default_acr` baseline (unset ⇒ omitted) —
  honest out of the box and overridable by configuration (the `default_acr` **setting**), by DI (a replacement
  resolver), or by a custom verifier that records its own `amr`. The resolver is async so a replacement can perform IO
  (a policy lookup, an external call) without a breaking signature change.

- **The context is sticky on the principal and the session.** The resolved `acr` is stamped as a principal claim (so it
  flows into tokens through the subject-claims handlers alongside `amr`) and is captured on the `SessionGrant` /
  `CreateSessionRequest` as `AuthenticationContextClassReference` (so a session records the context it was established
  with). The session rides the existing grant store as a JSON payload, so the new nullable field needs no migration.

- **`acr` is emitted as a subject claim, not a synthesized protocol claim.** The ID-token and access-token subject-claims
  handlers copy `acr` from the principal (RFC 9068 includes `acr` in the access token when available); the former
  `// TODO: acr` markers in the payload-claims handlers are removed.

- **`auth_time` is conditional and settings-governed.** A new `always_include_auth_time` setting (default `true`, for
  broad compatibility and to trivially satisfy the spec's MUST cases) controls whether the ID token includes
  `auth_time` whenever it is known; regardless of the setting, `auth_time` is included when **required** — a `max_age`
  request (threaded onto the token request as `AuthTimeRequired` by the authorization ticket handler) or an essential
  `auth_time` claim request. The access token no longer force-synthesizes `auth_time`; it flows only when the principal
  carries it. The forced-synthesis fallback (`EnsureAuthTime`) is now scoped to the **ID token** and runs **only when
  `auth_time` is required** but the principal lacked one.

## Options considered

- **Hardcode an `acr` value (e.g. `"1"`/`"2"` LoA numbers) at login.** Simple and always-emitting, but it fabricates a
  deployment-specific policy decision the spec explicitly defers to the OP, and bakes it where a deployment cannot
  cleanly override it. Rejected in favor of the resolver seam with an honest default (omit unless multi-factor or a
  configured baseline).
- **Resolve `acr` lazily at token issuance from `amr`.** Centralizes the policy in one place, but `acr` is a property of
  the authentication **event** (like `auth_time`), so recording it at authentication and storing it on the session is
  the cohesive, sticky model consistent with `amr`/`idp`. Rejected.
- **A synchronous resolver.** Smaller, but a real deployment policy may need IO (a database or external lookup); making
  the seam async up front avoids a later breaking change. Rejected.
- **Always emit `auth_time` (keep the unconditional behavior) or make it strictly conditional with no toggle.** The
  former ignores the spec's conditional contract and RFC 9068's "when available" guidance for the access token; the
  latter removes a useful, widely-expected default. The chosen middle — a default-`true` toggle plus an
  always-honored "required" computation — is both spec-correct and compatible.

## Consequences

- **Easier:** `acr`/`amr` are emitted and stable; the `acr` policy is a clean, async, DI/settings-overridable seam a
  deployment can own; `auth_time` follows the spec and RFC 9068; no new table or migration (the session's new field is
  an additive JSON property).
- **Harder / follow-ups:** The default resolver omits `acr` for a plain single-factor password login unless a
  `default_acr` baseline is configured (honest, but a deployment that wants an always-present `acr` must configure it or
  replace the resolver). **Enforcement** of a requested essential `acr` (denying when the satisfied `acr` is not among
  the requested essential values) was deferred at the time of this decision and is now **implemented** in
  `DefaultValidateSubjectAuthenticationHandler` (issue [#57](https://github.com/NCodeGroup/Identity/issues/57)): an essential `id_token.acr` request with specific
  `values` (OpenID Connect Core 1.0 §5.5.1.1) that the principal-stamped `acr` does not satisfy now yields
  `access_denied`, while voluntary requests (including the entire `acr_values` parameter) remain best-effort. MFA-driven
  `acr` will become meaningful once a multi-factor authentication subsystem exists; the seam is ready for it today.
