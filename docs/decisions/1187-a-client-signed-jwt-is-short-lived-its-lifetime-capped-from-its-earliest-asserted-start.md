# 1187. A client-signed JWT is short-lived, its lifetime capped from its earliest asserted start

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A client signs a JWT for the server in three places: the client assertion of `private_key_jwt` and `client_secret_jwt`
(RFC 7523, OpenID Connect Core §9), the request object of the authorization endpoint, inline as `request` or by
reference as `request_uri` (RFC 9101, JAR), and the signed authentication request of the CIBA backchannel endpoint
(CIBA Core §7.1.1). A token that is valid until whatever `exp` its signer chose stays usable that long, and a client
assertion's `jti` stays in the replay cache until then. RFC 7523 §3 lets the server reject a token whose `exp` is
unreasonably far in the future or whose `iat` is unreasonably far in the past, and mainstream providers bound assertion
lifetime: Auth0 accepts at most five minutes, and Okta rejects an `exp` more than an hour ahead.

Several forces shape the bound:

- **`iat` has to be validated.** FAPI 2.0 §5.3.2.1 requires the server to reject a JWT whose `iat` or `nbf` is more than
  60 seconds in the future. An `iat`-based cap is gameable unless `iat` itself is validated, because the client chooses
  it.
- **The replay record has to outlive the assertion.** An assertion is accepted until `exp` plus the configured
  `clock_skew`, so a replay record that expires earlier leaves a window in which the cache has dropped the record but
  the assertion still validates.
- **The specifications set rules for request objects.** FAPI 1.0 Advanced §5.2.2 and FAPI 2.0 Message Signing §5.3.1
  require `nbf` and `exp`, with `exp` no more than 60 minutes after `nbf` and `nbf` no more than 60 minutes in the past,
  and `aud` to be the issuer. RFC 9101 §4 and OpenID Connect Core §6.1 say the `aud` of a request object should be the
  authorization server's issuer. CIBA Core §7.1.1 requires `aud`, `iss`, `exp`, `iat`, `nbf` and `jti` on a signed
  authentication request.
- **The explicit `typ` is not universal.** RFC 9101 §10.8 recommends `oauth-authz-req+jwt`, but OpenID Connect Core
  clients, the OpenID conformance suite and FAPI 2.0 Message Signing do not send or require it.

## Decision

**Lifetime capping is a general JWT-validation capability, measured from the token's earliest asserted start, and each
kind of client-signed JWT uses it through a lattice-bounded setting.**

- **`iat` is a validated time claim.** `TokenLifetimeRequirement` validates `iat` like `nbf`: a present `iat` must be a
  valid `NumericDate` (fail closed, per [ADR-1123](1123-a-numericdate-and-a-whole-number-a-remote-party-sends-are-read-strictly-through-shared-readers.md)) and may not
  be later than the clock skew in the future. This holds for every `ValidateTokenLifeTime` consumer.
- **Optional maximum lifetime.** `MaxLifetime` (set through `ValidateTokenLifeTimeOptions.MaxLifetime`; `null` means no
  maximum) bounds `exp − start`, where the start is the earlier of `iat` and `nbf`, or the validation time plus the
  clock skew when the token asserts neither. A set maximum requires `exp`, since a token without one has an unbounded
  lifetime. Lifetime policy lives in the JWT validation requirement
  ([ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)), where every consumer reuses it.
- **A requirement can make `nbf` and `iat` mandatory.** `RequireNotBefore` and `RequireIssuedAt` on
  `TokenLifetimeRequirement` and `ValidateTokenLifeTimeOptions` make those claims mandatory, and a token without a
  required claim fails with a message that names it.
- **`client_assertion_max_lifetime` bounds a client assertion.** It is a non-discoverable `TimeSpan` setting with a
  `Min` merge ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)),
  so a tenant or client may only shorten it, and an operator who must accept longer-lived assertions raises the server
  value. It defaults to 5 minutes, Auth0's maximum. The client-assertion handlers pass it as `MaxLifetime`, so an
  over-long assertion is refused `invalid_client` with the audit reason `assertion_invalid`, and the logged validation
  failure names the exceeded maximum.
- **Replay retention follows the effective clock skew.** An assertion's `jti` replay record is kept until `exp` plus the
  client's effective `clock_skew`, floored at zero and saturating at `DateTimeOffset.MaxValue`: the last instant the
  assertion can validate. With the cap, retention is bounded by roughly the maximum lifetime plus twice the skew.
- **`request_object_max_lifetime` bounds a request object.** It is a client-scoped `TimeSpan` setting with a `Min`
  merge, so a tenant or a security profile sets a ceiling no client can raise, and it defaults to 60 minutes, the FAPI
  bound. The authorization endpoint's JAR loader and the CIBA loader apply it as `MaxLifetime`. Because the lifetime is
  measured from the earlier of `iat` and `nbf`, with `exp` not yet passed it also bounds how far in the past `nbf` can
  be.
- **`request_object_nbf_required` makes `nbf` mandatory at the authorization endpoint.** It is a client-scoped `bool`
  with an `Or` merge. It defaults to `false`, because OpenID Connect Core does not require `nbf`.
- **The FAPI profiles pin the ceilings.** The `fapi1-advanced` and `fapi2` profiles
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md))
  pin a 5-minute `client_assertion_max_lifetime`, a 60-minute `request_object_max_lifetime` and
  `request_object_nbf_required` `true` as ceilings, so raising a platform-wide value never extends to a FAPI client.
  FAPI defines no client-assertion lifetime, so the profiles do not go below the default. Their 60-second `clock_skew`
  ceiling covers the `iat` half of FAPI 2.0 §5.3.2.1.
- **The authorization endpoint always validates a request object's `aud`.** It expects `request_object_expected_audience`
  when that is set and the tenant's issuer otherwise, and refuses a request object without it with
  `invalid_request_object`. Under `fapi1-advanced` and `fapi2`, `IssuerOnlyRequestObjectAudienceRequirement` refuses a
  client that sets `request_object_expected_audience`
  ([ADR-1202](1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md)).
- **A CIBA signed authentication request carries every claim CIBA requires.** The loader requires `iat`, `nbf`, `exp`
  and a non-empty `jti`, applies `request_object_max_lifetime` and validates with the client's `clock_skew`, the same as
  the authorization endpoint.
- **The explicit `typ` is not required.** A request object is bound to its purpose by `iss` equal to the client, `aud`
  equal to the issuer and the client's signature. Requiring `oauth-authz-req+jwt` would refuse every OpenID Connect Core
  client and the conformance suite.

## Options considered

- **`exp − now` only** (Okta's rule, Nimbus's `expMaxAhead`). Bounds the remaining window but accepts an arbitrarily
  stale pre-minted token. Subsumed: with no asserted start, the chosen rule reduces to exactly this bound.
- **`exp − iat`, falling back to `exp − now`.** Gameable while `iat` is unvalidated, since a client can set `iat` just
  below `exp`, and it ignores an earlier `nbf`. Subsumed once `iat` is validated and the earlier of `iat` and `nbf` is
  used.
- **An age bound, `now − iat`.** Rejects stale tokens but leaves a far-future `exp`, and its replay record, unbounded.
  Subsumed: the earliest-start rule bounds age as well, because a non-expired token's age is at most its lifetime plus
  the skew.
- **Enforce the cap inside the client-assertion handler.** Rejected: lifetime policy belongs to the JWT validation
  requirement, where request objects reuse it.
- **A one-hour client-assertion default** (Okta parity). Rejected as looser than the secure, Auth0-parity default; the
  setting can be raised where a client library mints longer assertions.
- **A FAPI ceiling below five minutes for client assertions.** Rejected: FAPI defines no such lifetime, so it would
  refuse specification-conformant clients with no normative basis.
- **Require `typ` `oauth-authz-req+jwt`.** Rejected as the default. It breaks interoperability with OpenID Connect Core
  clients, and the audience and issuer checks already keep a request object from being confused with another JWT the
  client signs.
- **A `request_object_max_lifetime` default below 60 minutes.** Rejected. Clients pre-generate request objects and host
  them at a `request_uri`. Sixty minutes is the bound FAPI sets, and a tenant can lower it.
- **Validate `aud` only when `request_object_expected_audience` is configured.** Rejected. RFC 9101 and FAPI expect the
  issuer, and an unchecked `aud` lets a request object meant for another authorization server be replayed here.
- **The chosen option** is a general lifetime cap from the earliest asserted start, with a lattice-bounded setting per
  kind of token and FAPI ceilings.

## Consequences

- A client assertion valid for more than five minutes, from its `iat` or `nbf` or from now when it carries neither, is
  refused `invalid_client`. Client libraries that mint longer assertions need a shorter assertion lifetime (preferred)
  or a raised server-wide `client_assertion_max_lifetime`.
- Every lifetime-validated JWT (client assertions, request objects, CIBA request objects, access tokens at protected
  resources, introspected tokens, token-exchange subject tokens) is refused when its `iat` is unreadable or later than
  the clock skew in the future. Tokens this server issues stamp `iat` at issuance.
- A far-future `exp` cannot hold a replay record forever, so the replay cache is bounded.
- A request object without the issuer in `aud`, or valid for more than 60 minutes, is refused.
- Under the FAPI profiles, a request object without `nbf` is refused, and no client can raise the 60-minute ceiling.
- A CIBA signed authentication request without `iat`, `nbf` or `jti` is refused, and the CIBA loader uses the client's
  clock skew.

## References

- `src/NCode.Identity.Jwt/Validation/Handlers/TokenLifetimeRequirementHandler.cs`
- `src/NCode.Identity.Jwt.Abstractions/Validation/Requirements/TokenLifetimeRequirement.cs`
- `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultLoadAuthorizationRequestHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Endpoints/BackchannelAuthentication/Handlers/DefaultLoadBackchannelAuthenticationRequestHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/Fapi2SecurityProfile.cs`
- [RFC 7523 §3 — JWT assertion processing](https://datatracker.ietf.org/doc/html/rfc7523#section-3)
- [RFC 9101 — JWT-Secured Authorization Request](https://datatracker.ietf.org/doc/html/rfc9101)
- [FAPI 2.0 Security Profile §5.3.2.1](https://openid.net/specs/fapi-security-profile-2_0-final.html)
- [FAPI 2.0 Message Signing](https://openid.net/specs/fapi-2_0-message-signing.html)
- [FAPI 1.0 Advanced §5.2.2](https://openid.net/specs/openid-financial-api-part-2-1_0.html)
- [OpenID Connect CIBA Core §7.1.1](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html)
- [Auth0 — Authenticate with Private Key JWT](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authenticate-with-private-key-jwt)
- Issues [#78](https://github.com/NCodeGroup/identity/issues/78) and [#79](https://github.com/NCodeGroup/identity/issues/79)
