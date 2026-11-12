# 1144. Authorization codes are bound to an S256 PKCE challenge unless an attested client uses the nonce

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

PKCE (RFC 7636) binds an authorization code to the user agent that started the transaction: the client sends a
`code_challenge` with the authorization request and proves it with the `code_verifier` when it redeems the code. It
protects a public client's code from interception and every client's code from injection. The OAuth 2.0 Security BCP
(RFC 9700 §2.1.1) requires it of public clients and recommends it for confidential ones; OAuth 2.1 (draft-16 §4.1.1,
§4.1.2.1, §7.5.1.1) makes the authorization server enforce it for every client, with one exception: a confidential
client, where the server has reasonable assurance, in the specific deployment and the specific request, that the
client implements the OpenID Connect `nonce` mechanism correctly. OAuth 2.1 §7.5.2 also forbids the `plain` method,
which sends the verifier itself in the authorization request, and makes `code_challenge_method` required.

Several forces shape the design:

- PKCE verification needs a seam for transformations: a deployment adds a method by registering it, and the verifier
  has no hard-coded `switch` over method names.
- The set of methods the server supports is a discoverable capability (`code_challenge_methods_supported`, RFC 8414),
  and an operator, not mere registration, decides what is offered.
- Whether a code request must carry a challenge depends on the response type, on the client type and on whether the
  request uses the nonce. At the authorization and pushed authorization request endpoints the client has not
  authenticated, so its type is the one it registered; at the token endpoint it is how the request actually
  authenticated.
- A code that is not bound to PKCE could otherwise be redeemed by anyone who knows the `client_id` of a client
  permitted to authenticate with `none`.
- RFC 7636 fixes the syntax of the challenge and the verifier (43 to 128 unreserved characters) and the error each
  failure maps to.
- Confidential OpenID Connect clients that rely on the nonce and send no PKCE are common, and the OpenID Foundation's
  Basic OP certification suite is one of them.

## Decision

**An authorization code is bound to an `S256` PKCE challenge unless every condition of OAuth 2.1's exception holds.**

### Transformations

PKCE transformations are a strategy collection ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)).
`ICodeChallengeMethod` (in `NCode.Identity.OpenId.Authentication.Abstractions`) exposes a `Method` name and
`ComputeChallenge(codeVerifier)`, and `ICodeChallengeMethodCollectionProvider` resolves a transformation by its
`code_challenge_method` value. The default `S256CodeChallengeMethod` is registered with `TryAddEnumerable`, so a
deployment adds a method by registering another `ICodeChallengeMethod`. There is no `plain` transformation (OAuth 2.1
§7.5.2). The verifier compares the computed challenge with the stored one in constant time
(`CryptographicOperations.FixedTimeEquals`).

### The supported methods

`code_challenge_methods_supported` is owned by the PKCE slice (`DefaultPkceSettingDescriptorDataSource`). It is a
setting of tenants and clients, merged with `Intersect`, so a child can only narrow it, and it is discoverable. Its
domain is the registered `ICodeChallengeMethod` transformations ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md))
and its default is `S256` alone, so a transformation is never offered merely because it is registered: another
registered method is offered only when an operator lists it.

A `code_challenge` must name its `code_challenge_method`; there is no default, and
`AuthorizationRequest.CodeChallengeMethod` is `null` when the request names none. The authorization endpoint refuses a
method outside the list, and the token endpoint refuses a method with no registered transformation.

### Who must bind

`CodeChallengeRequirement` is the one policy that decides binding for the authorization, pushed authorization request
and token endpoints. It applies only to response types that issue a code, and it always binds a public client.

1. **`require_pkce` defaults to `true`.** It is a floor ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)),
   so only the platform's configuration can relax it, and a tenant or client can require it again. Relaxing it is the
   deployment's statement that it permits the nonce alternative; on its own it exempts nobody.
2. **A client is exempt only when it is attested.** `pkce_nonce_exemption` (a client setting, default `false`, merged
   with `Replace`) is an administrator's statement that a particular client implements the nonce mechanism correctly,
   the "reasonable assurance" OAuth 2.1 asks for. Dynamic client registration never sets it, so a self-registered
   client cannot claim it.
3. **The client must be confidential.** At the authorization and pushed authorization request endpoints this is the
   client type it is registered as: a registered `token_endpoint_auth_method` other than `none` (RFC 7591 §2). The
   FAPI 2.0 profile's confidential-client rule uses the same predicate. At the token endpoint it is how the token
   request actually authenticated, so a code that is not bound to PKCE can be redeemed only by a client that
   authenticates. A client authenticates only with its registered method
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)), so a client registered as confidential
   cannot redeem a code as public.
4. **The request must use the nonce.** It requests the `openid` scope and carries a `nonce`. At the token endpoint the
   scopes are the ones the token request is effectively for, so a token request that drops `openid`, and with it the ID
   Token that returns the nonce, can redeem only a bound code.

### Errors

- A missing challenge (RFC 7636 §4.4.1) or a malformed one (not 43 to 128 unreserved characters) is `invalid_request`
  at the authorization and pushed authorization request endpoints.
- A malformed, missing or mismatched `code_verifier`, a verifier presented for a code that is not bound (the PKCE
  downgrade of RFC 9700 §4.8.2) and a method with no registered transformation are `invalid_grant`.
- A protocol error that names no status code is `400 Bad Request` (RFC 6749 §5.2, RFC 9126 §2.3), so the pushed
  authorization request endpoint refuses a request with the status every other OAuth error response uses.

### Profiles and conformance

The FAPI 2.0 profile ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md))
keeps its own `require_pkce` ceiling, so a profile client is never exempt. The conformance host relaxes `require_pkce`
server-wide and attests its clients, because the OpenID Foundation suite is a known, nonce-validating relying party
that registers itself. The one Basic OP module that sends neither PKCE nor a nonce is recorded as an expected failure.

## Options considered

- **Exempt any confidential client whose OpenID Connect request carries a nonce, without attestation.** Rejected:
  carrying a nonce does not show that the client validates it. A deployment-wide switch would also exempt every client
  that registers itself dynamically, which is not the per-client assurance OAuth 2.1 requires.
- **Offer no exemption.** Rejected: confidential OpenID Connect clients that rely on the nonce and do not send PKCE are
  common (several mainstream relying-party libraries send PKCE only for public clients), and the OpenID Foundation's
  Basic OP certification suite is one of them. Interoperating with them is a deliberate, attested deployment choice.
- **Let a relaxed deployment accept a code request that uses neither PKCE nor a nonce.** Rejected: such a request is
  not protected against code injection at all, which is below the safe baseline no setting may cross, and OAuth 2.1
  §7.5.1.1 requires the assurance "in the specific request", which a request without a nonce does not give. OpenID
  Connect Core 1.0, which predates the Security BCP, makes the nonce optional in the code flow, and the OpenID
  Foundation's Basic OP module that sends neither (`oidcc-ensure-request-without-nonce-succeeds-for-code-flow`)
  follows it, so that module fails against this server by design. RFC 9700 permits either behavior of the server;
  where the published guidance diverges, this server follows the newer and stricter one, and the conformance baseline
  records the module as an expected failure rather than the server weakening the control.
- **Keep `plain` available to operators.** Rejected: OAuth 2.1 forbids it, and RFC 9700 §2.1.1 calls it a downgrade
  vector the server cannot tell from a legitimate request.
- **Offer every registered transformation by default.** Rejected: registration describes a capability; offering it is
  an operator decision, and the interface cannot guarantee that a transformation keeps the verifier secret.
- **Chosen:** `S256` by default, further transformations by registration plus an operator's listing, and a per-client
  attested exemption for confidential nonce-validating OpenID Connect clients.

## Consequences

- Adding a PKCE transformation is a DI registration plus a `code_challenge_methods_supported` entry, with no edit to
  the verifier.
- The server advertises its true PKCE capability in discovery, and a child tenant or client can only narrow it.
- A default deployment refuses an authorization code request without an `S256` challenge, for every client, with
  `invalid_request`.
- A deployment that must serve legacy confidential OpenID Connect clients sets `require_pkce` to `false` in its
  platform configuration and `pkce_nonce_exemption` to `true` on exactly those clients.
- The pushed authorization request endpoint returns `400` for every validation refusal, not only PKCE's.
- A "you chose wrong" smell: a client with `pkce_nonce_exemption` set that does not validate its `nonce`, or an
  operator who lists a transformation that exposes the verifier.
- The tests cover the requirement's conditions at each endpoint, the syntax checks of the challenge and verifier, the
  method list's domain and default, and the `400` of the pushed authorization request endpoint.

## References

- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): the strategy collection.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md): `require_pkce` is a floor.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md): the setting's domain is the
  registered transformations.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md): a client authenticates only with its
  registered method.
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md):
  the FAPI 2.0 profile requires PKCE.
- OAuth 2.1 (draft-ietf-oauth-v2-1-16) §4.1.1, §4.1.2.1, §4.1.3, §7.5.1.1, §7.5.2; RFC 9700 §2.1.1, §4.8.2;
  RFC 7636 §4.1, §4.2, §4.4.1, §4.6; RFC 7591 §2; RFC 6749 §5.2; RFC 9126 §2.3; RFC 8414.
- Code: `NCode.Identity.OpenId.Authentication.Pkce.CodeChallengeRequirement`,
  `NCode.Identity.OpenId.Authentication.Pkce.DefaultPkceSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Abstractions.ICodeChallengeMethod`, `S256CodeChallengeMethod`,
  `DefaultValidateAuthorizationRequestHandler.ValidateCodeChallenge` and `DefaultValidateAuthorizationCodeGrantHandler`.
