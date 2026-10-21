# 202. FAPI 1.0 Advanced is a second registered security profile

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

FAPI 2.0 is the OpenID Foundation's recommendation for new deployments, and `fapi2` is the family's primary profile
([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)). FAPI 1.0 Part 2: Advanced (Final, 2021)
is still certified, and UK Open Banking, Australia's Consumer Data Right, and Brazil's Open Finance still mandate it.
A tenant serving those ecosystems needs it.

FAPI 1.0 Advanced §5.2.2 permits two authorization responses: `code id_token`, whose ID token is a detached signature
over the response, or `code` with a JWT-secured authorization response (JARM,
[ADR-0201](0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)). It requires signed
request objects, sender-constrained access tokens, and strong client authentication, and it allows only PS256 and ES256.

While scoping the profile we found that, with a request object, the authorization request fell back to the outer query
for any parameter the object omitted, as OpenID Connect Core §6.1 merges them. FAPI 1.0 Advanced §5.2.2-10, FAPI 2.0
Message Signing, and RFC 9101 §6.3 all require using only the request object's parameters. Under the merge, an unsigned
outer parameter such as `response_mode` could change a signed request.

## Decision

1. **`fapi1-advanced` is a second registered `ISecurityProfile`.** `Fapi1AdvancedSecurityProfile` sits beside
   `Fapi2SecurityProfile` and is registered the same way. It is defined only by ceilings, defaults, and request-time
   requirements, so no enforcement point has a profile-specific branch.
2. **Ceilings.** The profile sets the following:
   - `response_types_supported` is `code` and `code id_token`;
   - `code_challenge_methods_supported` is `S256`, and `require_code_challenge` is on;
   - `require_pushed_authorization_requests` and `require_request_object` are on;
   - `token_endpoint_auth_methods_supported` is `private_key_jwt`, `tls_client_auth`, and
     `self_signed_tls_client_auth`;
   - every signing-algorithm list is PS256 and ES256, with no EdDSA (§8.6);
   - the authorization code lives at most 60 seconds and a PAR `request_uri` at most 90 seconds;
   - clock skew is at most 60 seconds and the client-assertion lifetime at most 5 minutes;
   - a request object must carry `nbf`, live at most 60 minutes, and be addressed to the issuer
     ([ADR-0187](0187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md)).

   PAR is optional in FAPI 1.0 Advanced, which requires PKCE `S256` whenever PAR is used. Requiring both matches the
   Consumer Data Right and the conformance suite's `pushed` variant. Because the server baseline lists only `code`
   ([ADR-0142](0142-listed-response-types-are-the-only-front-channel-token-control.md)), the effective default is code
   with JARM. A tenant serving a hybrid-flow ecosystem lists `code id_token` itself. The hybrid ID token already
   carries `s_hash` whenever the request sends `state`.
3. **Access tokens are bound through mutual TLS, not DPoP.** FAPI 1.0 names mutual TLS (RFC 8705) as its
   sender-constraining mechanism, and DPoP is not part of it. The ceiling sets both
   `access_token_sender_constraint_required` and `tls_client_certificate_bound_access_tokens`, so a token request
   without a client certificate fails even if it carries a DPoP proof. A client that wants DPoP selects `fapi2`.
4. **Defaults.** PS256 is the default for signed ID tokens, introspection responses, and authorization responses,
   because the protocol default (RS256) is outside the ceiling.
5. **Requirements.** `ConfidentialClientRequirement` refuses a public client at the authorization endpoint. The new
   `JwtSecuredAuthorizationResponseRequirement` refuses, with `invalid_request`, a request whose response type returns
   no ID token through the front channel unless its resolved response mode is JWT-secured. JARM names every such mode
   by adding `.jwt`, so the requirement does not list them. The client-assertion audience is not narrowed to the
   issuer, because that rule belongs to FAPI 2.0 alone. `AuthorizationRequestProfileContext` now carries the composed
   `IAuthorizationRequest` so that a requirement can judge the response type and response mode.
6. **A request object's parameters are the only parameters (RFC 9101 §6.3), for every request object.** When a
   request object is present, `AuthorizationRequest` reads every value, and its `Parameters` collection, from the
   object alone. Three outer values are still used when the object omits them:
   - `client_id`, which the object must also carry and match;
   - `response_type`, which OpenID Connect Core requires outside the object and which must match the object's value;
   - `scope`, which OpenID Connect Core also requires outside the object.

   Any other parameter sent only outside the object is ignored. The rule is not limited to FAPI profiles, because no
   client should be able to change a signed request with an unsigned parameter.

## Consequences

- A tenant or client selects `fapi1-advanced` through `security_profiles`, as it selects `fapi2`, and a client
  inherits the profile's ceilings wherever it selects the profile in the cascade.
- An OpenID Connect client that put parameters such as `response_mode` or `nonce` only in the outer query alongside a
  request object loses them. It must move them into the request object, as RFC 9101 requires.
- Running the conformance suite's `fapi1-advanced-final-test-plan` (with `private_key_jwt` and `mtls` client
  authentication, the `pushed` request method, and the `jarm` and `plain_response` response modes) is part of `T1`.
  The spec-coverage matrix gains a `profile-fapi1-advanced` row derived from discovery metadata.
