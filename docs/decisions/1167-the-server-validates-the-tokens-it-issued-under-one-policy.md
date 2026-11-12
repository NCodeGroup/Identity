# 1167. The server validates the tokens it issued under one policy

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The server reads the tokens its tenants issue: it authenticates a resource request with an access token, introspects a
token, accepts a subject token for token exchange, and reads the `id_token_hint` of an end-session request. A policy for
reading a token has many parts: the signature and encryption algorithms, the keys, the token type, the issuer, the
lifetime, the audience and the session. When each reader builds its own `JwtValidationParameters`, each states a
different subset, and a check that one reader omits is a hole that only that reader has.

Three forces shape the policy:

- **The tenant bounds every token it issued.** Its effective `access_token_signing_alg_values_supported`,
  `access_token_encryption_{alg,enc}_values_supported` and `id_token_signing_alg_values_supported` admit every token the
  tenant issued and nothing it does not permit, whichever client or resource server the token was issued for. The party
  that reads a token is not always the party the token was protected for: UserInfo reads tokens issued to any client.
- **A token has a kind, and the kinds are not interchangeable.** An access token is `at+jwt`, an ID token is `JWT`, and
  an introspection response (`token-introspection+jwt`) and a logout token (`logout+jwt`) are other JWTs that must
  never pass for either, the cross-JWT confusion RFC 9701 §8.1 warns about.
- **A system resource server is read by the server itself.** Tokens for `urn:ncode:openid` and `urn:ncode:management`
  are read by the server. A key held by one would make issuance encrypt to it, and UserInfo and the management API could
  not read their own tokens.

## Decision

**Every reader of a token the tenant issued validates it through one seam, `IIssuedTokenValidator`, which states the
tenant's policy for that kind of token. A system resource server never holds keys.**

1. **One seam.** `IIssuedTokenValidator` (public, with an internal default) validates an access token
   (`ValidateAccessTokenAsync`), an ID token (`ValidateIdTokenAsync`) and an `id_token_hint`
   (`ValidateIdTokenHintAsync`). It returns a `JwtValidationResult` and never throws for a bad token; cancellation still
   propagates. It validates against the `OpenIdContext` it is given, never against ambient routing. A replacement
   resolves the policy through the seam rather than reimplementing it, as `IAccessTokenPolicyService` does for
   issuance ([ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md)).
2. **The access-token policy.**
   - **Algorithms:** signature algorithms come from the tenant's effective `access_token_signing_alg_values_supported`,
     and key-management and content-encryption algorithms from `access_token_encryption_{alg,enc}_values_supported`.
     Compression is never accepted.
   - **Keys:** validation and decryption keys are the tenant's keys within their validity windows
     ([ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md)),
     plus any keys the caller adds. Introspection adds the calling resource server's keys
     ([ADR-1168](1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).
   - **Claims:** `typ` is `at+jwt`, `iss` is the tenant's issuer, the token is within its lifetime, and, when the
     caller names audiences, `aud` contains one of them.
   - **Session:** a token whose `sid` names an explicitly revoked session is rejected
     ([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)). An
     absent (aged-out) session is not a revocation; the token's own short lifetime bounds it.
3. **The ID-token policy.** Signature algorithms come from `id_token_signing_alg_values_supported`, `typ` is `JWT`, `iss`
   is the tenant's issuer and the lifetime is checked. No decryption keys are named: an ID token is encrypted only to its
   client's public key, so an encrypted ID token is rejected.
4. **The `id_token_hint` policy.** A hint skips the lifetime check, which OpenID Connect RP-Initiated Logout §2 allows
   to be expired. That section also lets a relying party re-encrypt the signed ID token to the server, so a hint, and
   only a hint, is decrypted with the tenant's keys under the tenant's `id_token_encryption_alg_values_supported` and
   `id_token_encryption_enc_values_supported`, with no compression; the enclosed ID token's checks are unchanged. The
   tenant's JSON Web Key Set publishes the public keys a relying party encrypts to, as it does for request objects; a
   tenant without an encryption key accepts only a signed hint.
5. **Token exchange picks the policy by subject-token type:**
   - `access_token` uses the access-token policy;
   - `id_token` uses the ID-token policy;
   - `jwt` is accepted only when it validates as an access token or an ID token, so `token-introspection+jwt` and
     `logout+jwt` are refused;
   - any other type is `invalid_request`.
6. **Callers.** Resource-request authentication (UserInfo, the management API and every first-party resource endpoint),
   introspection, token exchange and the end-session `id_token_hint` all validate through the seam. No production code
   builds `JwtValidationParameters` for a token the tenant issued.
7. **A system resource server holds no keys.** The management API answers `409 Conflict` to creating or importing a
   secret for one, and issuance never treats a system resource server as decrypting its own tokens, so a key stored on
   one is ignored. `ResolvedResourceServer` carries `IsSystem` and the resource server's `Scopes`.

## Options considered

- **Give each reader the decryption keys and algorithm lists it needs.** Rejected: it keeps one copy of the security
  policy per reader, which is how a type check or a session check goes missing from one of them.
- **Validate an access token against the requesting client's or resource server's algorithm lists.** Rejected: the
  reader is not always the party the token was protected for. The tenant's lists are the bound every issued token lies
  within.
- **Decrypt ID tokens with the tenant's keys.** Rejected: an ID token is never encrypted to the server, so a decryption
  key would only widen what the validator accepts. Only an `id_token_hint`, which its client may re-encrypt to the
  server, is decrypted.
- **Strip keys from system resource servers when they are seeded.** Rejected: the management API would still accept new
  keys. Ignoring them at issuance and refusing them at write time is enforced where the decision is made.
- **One seam with the tenant's policy per kind of token** (chosen): one place states each policy, every reader inherits
  it, and a replacement overrides the seam rather than a copy.

## Consequences

- The server reads every access token it encrypted to itself: UserInfo, the management API, introspection and token
  exchange accept them.
- A tenant encrypts access tokens to itself only with an encryption key it holds. None is provisioned by default, so
  such a tenant lists a key-management algorithm in `OpenId:Tenant:KeyProvisioning:Keys` or imports a key
  ([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)).
- Each reader enforces the token type, the tenant's algorithm allow-lists and session revocation: introspection reports
  neither an ID token nor a revoked-session token as active, and token exchange accepts neither an introspection
  response nor a logout token.
- A token encrypted to a resource server's own key is read only where that resource server's keys are supplied, at
  introspection by that resource server. Token exchange cannot exchange it; it is meant for that resource server alone.
- The management API authenticates against the control-plane tenant by passing that tenant's context to the seam
  ([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)).
- A system resource server's tokens stay readable by UserInfo and the management API, because no key can be stored on it.

## References

- [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) §3.1, §3.11 (explicit typing); [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068)
  §2.1 (`at+jwt`), §4; [RFC 8693](https://www.rfc-editor.org/rfc/rfc8693) §2.1, §3;
  [RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) §8.1; OpenID Connect RP-Initiated Logout 1.0 §2.
- [ADR-1168](1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md): the introspection caller that
  adds its resource server's keys.
- Code: `NCode.Identity.OpenId.Authentication.Tokens.IIssuedTokenValidator`, `AccessTokenValidationOptions`,
  `DefaultIssuedTokenValidator`, `Tokens.Handlers.DefaultAuthenticateAccessTokenHandler`,
  `Endpoints.Token.TokenExchange.DefaultSubjectTokenValidator`,
  `Endpoints.EndSession.Handlers.DefaultResolveEndSessionSubjectHandler`, `Tokens.DefaultAccessTokenPolicyService`,
  `ResourceServers.ResolvedResourceServer`,
  `NCode.Identity.OpenId.Management.Endpoints.ResourceServers.ResourceServerApiEndpointHandler`.
