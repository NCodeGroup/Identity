# 124. JWT lifetime is capped from its earliest asserted start; client assertions default to five minutes

- **Status:** Accepted (request objects capped by
  [ADR-0187](./0187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md))
- **Date:** 2026-10-17
- **Deciders:** NCode.Identity maintainers

## Context

A `private_key_jwt` / `client_secret_jwt` client assertion (RFC 7523, OpenID Connect Core §9) was accepted for any `exp`
that had not yet passed — including one at the top of the `NumericDate` range (year 9999). Its `jti` was then held in
the replay cache effectively forever, and a pre-minted assertion stayed usable for as long as its signer chose. RFC 7523
§3 lets the server reject an assertion whose `exp` is "unreasonably far in the future" and one whose `iat` is
"unreasonably far in the past", and mainstream providers bound assertion lifetime (Auth0 accepts at most five minutes;
Okta rejects an `exp` more than an hour ahead). [ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md)
fixed the overflow such an `exp` caused but left the lifetime unbounded (backlog `F39`).

Three adjacent defects shared the cause:

- **`iat` was never validated.** The JWT lifetime requirement read `nbf` and `exp` only, so a garbage or far-future
  `iat` passed. FAPI 2.0 §5.3.2.1 requires the authorization server to reject a JWT whose `iat` **or** `nbf` is more
  than 60 seconds in the future; only the `nbf` half was enforced.
- **An `iat`-based cap is gameable** unless `iat` itself is validated, because the client chooses it.
- **The replay record outlived `exp` by a fixed five minutes**, while the assertion is accepted until `exp` plus the
  _configured_ `clock_skew`. A server-wide skew above five minutes left a window in which a replay was no longer in the
  cache but the assertion still validated.

## Decision

**Lifetime capping is a general JWT-validation capability, measured from the token's earliest asserted start, and
client assertions use it through a lattice-bounded setting.**

- **`iat` is a validated time claim.** `TokenLifetimeRequirement` now reads `iat` alongside `nbf` and `exp`: a present
  `iat` must be a valid `NumericDate` (fail closed, per ADR-0123) and may not be later than the clock skew in the future
  — the same rule as `nbf`. This applies to every `ValidateTokenLifeTime` consumer.
- **Optional maximum lifetime.** `TokenLifetimeRequirement.MaxLifetime` (set through
  `ValidateTokenLifeTimeOptions.MaxLifetime`; `null` means no maximum) bounds `exp − start`, where **start is the
  earlier of `iat` and `nbf`, or — when the token asserts neither — the validation time plus the clock skew**. A set
  maximum also requires `exp`, since a token without one has an unbounded lifetime.
- **`client_assertion_max_lifetime`.** A non-discoverable `TimeSpan` setting, default **five minutes** (Auth0's
  maximum), merged as a `Min` ceiling ([ADR-0038](0038-setting-merge-lattice-duals.md)) so a tenant or client may only
  shorten it; an operator who must accept longer-lived assertions raises the server value. The client-assertion handlers
  pass it as the `MaxLifetime`, so an over-long assertion is rejected as `invalid_client` (audit reason
  `assertion_invalid`; the logged validation failure names the exceeded maximum).
- **FAPI 2.0 pins the default.** The `fapi2` profile injects a five-minute `client_assertion_max_lifetime` ceiling
  ([ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md)), so raising the server-wide value for other
  clients never extends to a FAPI client. FAPI 2.0 defines no client-assertion lifetime, so the profile does not go below
  the default: doing so would reject spec-conformant FAPI clients with no normative basis. The existing 60-second FAPI
  `clock_skew` ceiling now also covers the `iat` half of §5.3.2.1.
- **Replay retention follows the effective clock skew.** The replay record is retained until `exp` plus the client's
  effective `clock_skew` (floored at zero and saturating at `DateTimeOffset.MaxValue`), the last instant the assertion
  can validate. With the cap, retention is bounded by roughly the maximum lifetime plus twice the skew.

## Options considered

- **`exp − now` only** (Okta's rule, Nimbus's `expMaxAhead`). Bounds the remaining window but accepts an arbitrarily
  stale pre-minted assertion. Subsumed: with no asserted start, the chosen rule reduces to exactly this bound.
- **`exp − iat`, falling back to `exp − now`** (the backlog's sketch). Gameable while `iat` is unvalidated — a client can
  set `iat` just below `exp` — and ignores an earlier `nbf`. Subsumed once `iat` is validated and the earlier of
  `iat`/`nbf` is used.
- **`now − iat`** (an age bound). Rejects stale assertions but leaves a far-future `exp` — and its replay record —
  unbounded. Subsumed: the earliest-start rule bounds age as well, because a non-expired token's age is at most its
  lifetime plus the skew.
- **Enforce the cap inside the client-assertion handler.** Rejected: lifetime policy belongs to the JWT validation
  requirement ([ADR-0120](0120-jwt-validation-requirements-and-handlers.md)), where request objects can reuse it.
- **A one-hour default** (Okta parity). Rejected as looser than the secure, Auth0-parity default; the setting can be
  raised where a client library mints longer assertions.
- **A 60-second FAPI ceiling.** Rejected for the normative reason above.

## Consequences

- A client assertion valid for more than five minutes — from its `iat`/`nbf`, or from now when it carries neither — is
  rejected with `invalid_client`. Client libraries that mint longer assertions by default need either a shorter
  assertion lifetime (preferred) or a raised server-wide `client_assertion_max_lifetime`.
- Every lifetime-validated JWT (client assertions, request objects, CIBA request objects, access tokens at protected
  resources, introspected tokens, token-exchange subject tokens) is now rejected when its `iat` is unreadable or later
  than the clock skew in the future. Tokens this server issues stamp `iat` at issuance, so they are unaffected.
- The far-future-`exp` replay-cache growth that motivated `F39` is closed.
- Signed request objects (JAR and CIBA) apply `MaxLifetime` through their own `request_object_max_lifetime` setting,
  with the claim rules FAPI 2.0 Message Signing, FAPI 1.0 Advanced, and CIBA impose
  ([ADR-0187](./0187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md)).

## References

- `src/NCode.Identity.Jwt/Validation/Handlers/TokenLifetimeRequirementHandler.cs`
- `src/NCode.Identity.Jwt.Abstractions/Validation/Requirements/TokenLifetimeRequirement.cs`
- `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/ClientAssertionClientAuthenticationHandler.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/Fapi2SecurityProfile.cs`
- [RFC 7523 §3 — JWT assertion processing](https://datatracker.ietf.org/doc/html/rfc7523#section-3)
- [FAPI 2.0 Security Profile §5.3.2.1](https://openid.net/specs/fapi-security-profile-2_0-final.html)
- [Auth0 — Authenticate with Private Key JWT](https://auth0.com/docs/get-started/authentication-and-authorization-flow/authenticate-with-private-key-jwt)
- Backlog `F39` and `F40` ([`BACKLOG-COMPLETED.md`](../../BACKLOG-COMPLETED.md))
