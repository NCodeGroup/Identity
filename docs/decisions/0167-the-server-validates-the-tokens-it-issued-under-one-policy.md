# 167. The server validates the tokens it issued under one policy

- **Status:** Accepted (amends [ADR-0066](0066-resource-server-settings-and-secret-owner.md),
  [ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md), and
  [ADR-0101](0101-oauth-token-exchange-rfc8693.md); closes the follow-ups of
  [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) and
  [ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md); realizes the
  introspection clause of [ADR-0112](0112-authorization-code-replay-revokes-session.md))
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

[ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) made every caller of the
JWT validator state its algorithm and key policy, and
[ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md) encrypts an access token
to the party that reads it — a resource server that decrypts its own tokens, or else the tenant. Both left the same
follow-up: the server's own readers of access tokens name no decryption keys and no algorithm lists, so every access
token the server encrypted to itself was rejected by the server.

The readers had drifted further than that, because each built its own `JwtValidationParameters`:

- **No algorithm allow-lists.** Resource-request authentication (UserInfo, the management API), introspection, token
  exchange, and the end-session `id_token_hint` accepted any algorithm the validator knew.
- **No token type.** Introspection and token exchange did not check `typ`, so introspection reported an ID token as an
  active access token, and token exchange accepted a logout token as a `jwt` subject token.
- **No session check at introspection.** [ADR-0112](0112-authorization-code-replay-revokes-session.md) says introspection
  rejects a token whose session was revoked; only resource-request authentication did.
- **A system resource server could be given keys.** `urn:ncode:openid` and `urn:ncode:management` tokens are read by the
  server itself. A key added to one would make issuance encrypt to it, and UserInfo and the management API could no
  longer read their own tokens.

## Decision

**Every reader of a token the tenant issued validates it through one seam, `IIssuedTokenValidator`, which states the
tenant's policy for that kind of token. A system resource server never holds keys.**

1. **One seam.** `IIssuedTokenValidator` (public, with an internal default) validates an access token, an ID token, and
   an `id_token_hint`. It returns a `JwtValidationResult` and never throws for a bad token; cancellation still
   propagates. It validates against the `OpenIdContext` it is given, never against ambient routing. A replacement
   resolves the policy through the seam rather than reimplementing it, as `IAccessTokenPolicyService` does for
   issuance (ADR-0158).
2. **The access-token policy.**
   - **Algorithms:** signature algorithms come from the tenant's effective `access_token_signing_alg_values_supported`,
     and key-management and content-encryption algorithms from `access_token_encryption_{alg,enc}_values_supported`.
     Client and resource-server lists narrow the tenant's by intersection, so the tenant's lists admit every token it
     issued and nothing it does not permit. Compression is never accepted.
   - **Keys:** validation keys are the tenant's keys. Decryption keys are the tenant's keys, plus any keys the caller
     adds — introspection adds the calling resource server's keys
     ([ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).
   - **Claims:** `typ` is `at+jwt`, `iss` is the tenant's issuer, the token is within its lifetime, and, when the
     caller names audiences, `aud` contains one of them.
   - **Session:** a token whose `sid` names an explicitly revoked session is rejected. An absent (aged-out) session is
     not a revocation; the token's own short lifetime bounds it.
3. **The ID-token policy.** Signature algorithms come from `id_token_signing_alg_values_supported`, `typ` is `JWT`, and
   `iss` is the tenant's issuer. No decryption keys are named: an ID token is encrypted only to its client's public key,
   so an encrypted ID token is rejected. The lifetime is checked for an ID token, but not for an `id_token_hint`, which
   OpenID Connect RP-Initiated Logout §2 allows to be expired. That section also lets a relying party re-encrypt the
   signed ID token to the server, so a hint — and only a hint — is decrypted with the tenant's keys under the tenant's
   `id_token_encryption_alg_values_supported` and `id_token_encryption_enc_values_supported`, with no compression; the
   enclosed ID token's checks are unchanged. The tenant's JSON Web Key Set already publishes the public keys a relying
   party encrypts to, as it does for request objects; a tenant without an encryption key accepts only a signed hint.
4. **Token exchange picks the policy by subject-token type** (amends
   [ADR-0101](0101-oauth-token-exchange-rfc8693.md)):
   - `access_token` uses the access-token policy;
   - `id_token` uses the ID-token policy;
   - `jwt` is accepted only when it validates as an access token or an ID token, so introspection responses
     (`token-introspection+jwt`) and logout tokens (`logout+jwt`) are refused — the cross-JWT confusion RFC 9701 §8.1
     warns about;
   - any other type is `invalid_request`.
5. **Callers.** Resource-request authentication (UserInfo, the management API, every first-party resource endpoint),
   introspection, token exchange, and the end-session `id_token_hint` all validate through the seam. No production code
   builds `JwtValidationParameters` for a token the tenant issued.
6. **A system resource server holds no keys** (amends [ADR-0066](0066-resource-server-settings-and-secret-owner.md) and
   [ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md)). The management API refuses to create or import a
   secret for one (`409 Conflict`), and issuance never treats a system resource server as decrypting its own tokens, so a
   key already stored on one is ignored and existing data needs no migration. `ResolvedResourceServer` carries
   `IsSystem` and the resource server's `Scopes`.

## Options considered

- **Give each reader the decryption keys and algorithm lists it lacks.** Rejected: it fixes the follow-up but keeps four
  copies of a security policy, which is how the type check and the session check went missing.
- **Validate an access token against the requesting client's or resource server's algorithm lists.** Rejected: the
  reader is not always the party the token was protected for (UserInfo reads tokens issued to any client). The tenant's
  lists are the union bound every issued token lies within.
- **Decrypt ID tokens with the tenant's keys.** Rejected: an ID token is never encrypted to the server, so a decryption
  key would only widen what the validator accepts. Only an `id_token_hint`, which its client may re-encrypt to the
  server, is decrypted.
- **Strip keys from system resource servers when they are seeded.** Rejected: it is a data migration that a deploy can
  race, and the management API would still accept new keys. Ignoring them at issuance and refusing them at write time is
  enforced where the decision is made.

## Consequences

- The server reads every access token it encrypted to itself: UserInfo, the management API, introspection, and token
  exchange accept them.
- A tenant encrypts access tokens to itself only with an encryption key it holds. None is provisioned by default, so
  such a tenant lists a key-management algorithm in `OpenId:Tenant:KeyProvisioning:Keys` or imports a key
  ([ADR-0169](0169-every-tenant-is-provisioned-with-persisted-keys.md)).
- Each reader enforces the token type, the tenant's algorithm allow-lists, and session revocation: introspection no
  longer reports an ID token or a revoked-session token as active, and token exchange no longer accepts an introspection
  response or a logout token.
- A token encrypted to a resource server's own key is read only where that resource server's keys are supplied — at
  introspection by that resource server. Token exchange cannot exchange it; it was meant for that resource server alone.
- The management API authenticates against the control-plane tenant by passing that tenant's context to the seam,
  which needed no change ([ADR-0191](0191-the-management-api-authenticates-against-the-control-plane-tenant.md)).
- **Breaking:** the new `IIssuedTokenValidator` and `AccessTokenValidationOptions`; `ResolvedResourceServer` requires
  `IsSystem` and `Scopes`; the management API returns `409` for a secret written to a system resource server; the
  retired `AccessTokenValidationError` log event's ID is not reused.

## References

- [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) §3.1, §3.11 (explicit typing); [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068)
  §2.1 (`at+jwt`), §4; [RFC 8693](https://www.rfc-editor.org/rfc/rfc8693) §2.1, §3;
  [RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) §8.1; OpenID Connect RP-Initiated Logout 1.0 §2.
- [ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md) — the introspection caller that
  adds its resource server's keys.
- Code: `NCode.Identity.OpenId.Authentication.Tokens.IIssuedTokenValidator`, `AccessTokenValidationOptions`,
  `DefaultIssuedTokenValidator`, `Tokens.Handlers.DefaultAuthenticateAccessTokenHandler`,
  `Endpoints.Token.TokenExchange.DefaultSubjectTokenValidator`,
  `Endpoints.EndSession.Handlers.DefaultResolveEndSessionSubjectHandler`, `Tokens.DefaultAccessTokenPolicyService`,
  `ResourceServers.ResolvedResourceServer`,
  `NCode.Identity.OpenId.Management.Endpoints.ResourceServers.ResourceServerApiEndpointHandler`.
