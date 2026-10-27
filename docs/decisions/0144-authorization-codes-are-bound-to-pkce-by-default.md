# 144. Authorization codes are bound to PKCE by default

- **Status:** Accepted (amends [ADR-0079](0079-extensible-pkce-code-challenge-methods.md),
  [ADR-0083](0083-secure-by-default-discovery-metadata.md), and
  [ADR-0140](0140-setting-values-are-validated-against-their-domain.md); amended by
  [ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md): a client authenticates only with its
  registered method, so the type it authenticates as at the token endpoint is the type it is registered as)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

PKCE (RFC 7636) binds an authorization code to the user agent that started the transaction: the client sends a
`code_challenge` with the authorization request and proves it with the `code_verifier` when it redeems the code. It
protects a public client's code from interception and every client's code from injection. The OAuth 2.0 Security BCP
(RFC 9700 §2.1.1) requires it of public clients and recommends it for confidential ones; OAuth 2.1 (draft-16 §4.1.1,
§4.1.2.1, §7.5.1.1) makes the authorization server enforce it for every client, with one exception: a confidential
client, where the server has reasonable assurance — in the specific deployment and the specific request — that the
client implements the OpenID Connect `nonce` mechanism correctly. OAuth 2.1 §7.5.2 also forbids the `plain` method,
which sends the verifier itself in the authorization request, and makes `code_challenge_method` required.

The server fell short of that in several ways:

- `require_pkce` defaulted to `false`, so a code request without a challenge was accepted unless the client ran the FAPI
  2.0 profile or was the developer seed client. When it was required, the refusal was `unauthorized_client`, where
  RFC 7636 §4.4.1 specifies `invalid_request`, and it was applied to every response type, including an implicit one
  that issues no code.
- A request with a `code_challenge` and no method was given RFC 7636's implicit `plain` default, and `plain` remained a
  value an operator could configure: `code_challenge_methods_supported` was a fixed vocabulary of `S256` and `plain`,
  although ADR-0079 promised that a registered transformation is enough to support a method.
- The token endpoint re-read `require_pkce` but did not consider how the client authenticated, so nothing stopped a
  code that was not bound to PKCE from being redeemed by anyone who knew the `client_id` of a client permitted to
  authenticate with `none`.
- Neither endpoint checked the RFC 7636 syntax of the challenge or the verifier (43 to 128 unreserved characters).
- A refusal from the pushed authorization request endpoint carried no status code and was sent as `200 OK`.

## Decision

**An authorization code is bound to an `S256` PKCE challenge unless every condition of OAuth 2.1's exception holds.**
One policy, `CodeChallengeRequirement`, decides it for both endpoints:

1. **`require_pkce` defaults to `true`.** It is a floor ([ADR-0038](0038-setting-merge-lattice-duals.md)), so only the
   server's configuration can relax it, and a tenant or client can require it again. Relaxing it is the deployment's
   statement that it permits the nonce alternative; on its own it exempts nobody.
2. **A client is exempt only when it is attested.** `pkce_nonce_exemption` (default `false`, override merge) is an
   administrator's statement that a particular client implements the nonce mechanism correctly — the "reasonable
   assurance" OAuth 2.1 asks for. Dynamic client registration never sets it, so a self-registered client cannot claim
   it.
3. **The client must be confidential.** At the authorization and pushed authorization request endpoints, where the
   client does not authenticate, this is the client type it is registered as: a registered `token_endpoint_auth_method`
   other than `none` (RFC 7591 §2). The FAPI 2.0 profile's confidential-client rule uses the same predicate. At the
   token endpoint it is how the token request actually authenticated, so a code that is not bound to PKCE can be
   redeemed only by a client that authenticates. A public client is always bound to PKCE.
4. **The request must use the nonce.** It requests the `openid` scope and carries a `nonce`; at the token endpoint the
   scopes are the ones the token request is effectively for, so a token request that drops `openid` — and with it the
   ID Token that returns the nonce — can redeem only a bound code.
5. **The requirement applies to response types that issue a code.** A missing challenge is `invalid_request`
   (RFC 7636 §4.4.1) at the authorization and pushed authorization request endpoints.

**PKCE means `S256` or a registered transformation an operator lists; `plain` is gone.** The `plain` transformation and
its constant are removed. `code_challenge_methods_supported` is described by the PKCE slice: its domain is the
registered `ICodeChallengeMethod` transformations (as ADR-0140 does for other strategy-defined domains), and its default
is `S256` alone, so a transformation is never offered merely because it is registered — an operator lists it. A
`code_challenge` names its `code_challenge_method`; there is no default. A data migration removes `plain` from every
stored `code_challenge_methods_supported` list; a list that named only `plain` becomes empty, which offers no method.

**Both PKCE values are checked for their RFC 7636 syntax.** A malformed `code_challenge` is `invalid_request`; a
malformed `code_verifier` is `invalid_grant`, as are a missing or mismatched verifier and a verifier presented for a code
that is not bound (the PKCE downgrade of RFC 9700 §4.8.2).

**A protocol error that names no status code is a `400`.** The shared exception handler gives such an error
`400 Bad Request` (RFC 6749 §5.2, RFC 9126 §2.3), so the pushed authorization request endpoint refuses a request with
the status every other OAuth error response uses.

## Options considered

- **Exempt any confidential client whose OpenID Connect request carries a nonce, without attestation.** Rejected:
  carrying a nonce does not show that the client validates it. A deployment-wide switch would also exempt every client
  that registers itself dynamically, which is not the per-client assurance OAuth 2.1 requires.
- **Offer no exemption.** Rejected: confidential OpenID Connect clients that rely on the nonce and do not send PKCE are
  common (several mainstream relying-party libraries send PKCE only for public clients), and the OpenID Foundation's
  Basic OP certification suite is one of them. Interoperating with them is a deliberate, attested deployment choice.
- **Let a relaxed deployment accept a code request that uses neither PKCE nor a nonce.** Rejected: such a request is
  not protected against code injection at all, which is below the safe baseline no setting may cross, and OAuth 2.1
  §7.5.1.1 requires the assurance "in the specific request" — a request without a nonce gives none. OpenID Connect
  Core 1.0, which predates the Security BCP, makes the nonce optional in the code flow, and the OpenID Foundation's
  Basic OP module that sends neither (`oidcc-ensure-request-without-nonce-succeeds-for-code-flow`) follows it, so that
  module fails against this server by design. RFC 9700 permits either behavior of the server; where the published
  guidance diverges, this server follows the newer and stricter one, and the conformance baseline records the module
  as an expected failure rather than the server weakening the control.
- **Keep `plain` available to operators.** Rejected: OAuth 2.1 forbids it, and RFC 9700 §2.1.1 calls it a downgrade
  vector the server cannot tell from a legitimate request.
- **Default `code_challenge_methods_supported` to every registered transformation.** Rejected: registration describes a
  capability; offering it is an operator decision, and the interface cannot guarantee that a transformation keeps the
  verifier secret.

## Consequences

- A default deployment refuses an authorization code request without an `S256` challenge, for every client, with
  `invalid_request`. The FAPI 2.0 profile keeps its own `require_pkce` ceiling, so a profile client is never exempt.
- A deployment that must serve legacy confidential OpenID Connect clients sets `require_pkce` to `false` in its server
  configuration and `pkce_nonce_exemption` to `true` on exactly those clients. The conformance host does both
  server-wide, because the OpenID Foundation suite is a known, nonce-validating relying party that registers itself;
  the one Basic OP module that sends neither PKCE nor a nonce is recorded as an expected failure.
- `OpenIdConstants.CodeChallengeMethods.Plain` and the developer seed's explicit `require_pkce` leave the code;
  `AuthorizationRequest.CodeChallengeMethod` is `null` when the request names no method.
- The pushed authorization request endpoint returns `400` for every validation refusal, not only PKCE's.
- The token endpoint authenticates a client only with its registered `token_endpoint_auth_method`
  ([ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md)), so a client registered as confidential
  cannot redeem a code as public. The binding check above reads how the client authenticated, which is therefore its
  registered type.

## References

- [ADR-0079](0079-extensible-pkce-code-challenge-methods.md) — the PKCE transformation strategy collection; its
  domain and default are amended here.
- [ADR-0083](0083-secure-by-default-discovery-metadata.md) — secure-by-default metadata; `plain` is no longer an
  operator option.
- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) — setting domains; PKCE methods move from a
  fixed vocabulary to the registered transformations, and narrowing a domain ships with a data migration.
- [ADR-0038](0038-setting-merge-lattice-duals.md) — `require_pkce` is a floor.
- [ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md) — the FAPI 2.0 profile requires PKCE.
- OAuth 2.1 (draft-ietf-oauth-v2-1-16) §4.1.1, §4.1.2.1, §4.1.3, §7.5.1.1, §7.5.2; RFC 9700 §2.1.1, §4.8.2;
  RFC 7636 §4.1, §4.2, §4.4.1, §4.6; RFC 7591 §2; RFC 6749 §5.2; RFC 9126 §2.3.
- Code: `NCode.Identity.OpenId.Authentication.Pkce.CodeChallengeRequirement`,
  `NCode.Identity.OpenId.Authentication.Pkce.DefaultPkceSettingDescriptorDataSource`,
  `DefaultValidateAuthorizationRequestHandler.ValidateCodeChallenge`, `DefaultValidateAuthorizationCodeGrantHandler`,
  and the `RetirePlainCodeChallengeMethod` migration in `NCode.Identity.OpenId.Host.Database`.
