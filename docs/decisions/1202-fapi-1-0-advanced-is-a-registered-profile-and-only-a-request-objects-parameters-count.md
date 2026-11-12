# 1202. FAPI 1.0 Advanced is a registered profile, and only a request object's parameters count

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

FAPI 2.0 is the OpenID Foundation's recommendation for new deployments, and `fapi2` is the family's primary profile
([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
FAPI 1.0 Part 2: Advanced (Final, 2021) is still certified, and UK Open Banking, Australia's Consumer Data Right and
Brazil's Open Finance still mandate it. A tenant serving those ecosystems needs it.

FAPI 1.0 Advanced §5.2.2 permits two authorization responses: `code id_token`, whose ID token is a detached signature
over the response, or `code` with a JWT-secured authorization response (JARM,
[ADR-1201](1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)). It requires signed
request objects, sender-constrained access tokens and strong client authentication, and it allows only PS256 and ES256.

OpenID Connect Core §6.1 merges the outer query into a request object, so an unsigned outer parameter such as
`response_mode` could change a signed request. FAPI 1.0 Advanced §5.2.2-10, FAPI 2.0 Message Signing and RFC 9101 §6.3
all require using only the request object's parameters.

## Decision

`fapi1-advanced` is a registered security profile whose request-time rules read only a request object's own parameters.

1. **The profile is registered like `fapi2`.** `Fapi1AdvancedSecurityProfile` is an `ISecurityProfile` made only of
   ceilings, defaults and requirements, so no enforcement point branches on it.
2. **Ceilings.** The profile sets the following:
   - `response_types_supported` is `code` and `code id_token`;
   - `code_challenge_methods_supported` is `S256`, and `require_pkce` is on;
   - `require_pushed_authorization_requests` and `require_request_object` are on;
   - `token_endpoint_auth_methods_supported` is `private_key_jwt`, `tls_client_auth` and
     `self_signed_tls_client_auth`;
   - the ID token, access token, UserInfo, introspection, authorization-response, request-object and client-assertion
     signing lists are `PS256` and `ES256`, with no EdDSA (§8.6);
   - the authorization code lives at most 60 seconds, a PAR `request_uri` at most 90 seconds and a client assertion at
     most 5 minutes;
   - `clock_skew` is at most 60 seconds;
   - `redirect_uri_kinds_supported` is `https` only and `allow_loopback_redirect` is off
     (FAPI 1.0 Part 1 §5.2.2-20 and §7.5, [ADR-1203](1203-redirect-uris-are-matched-exactly.md));
   - a request object must carry `nbf` and live at most 60 minutes
     ([ADR-1187](1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md)).

   PAR is optional in FAPI 1.0 Advanced, which requires PKCE `S256` whenever PAR is used. Requiring both matches the
   Consumer Data Right and the conformance suite's `pushed` variant. Because the baseline lists only `code`
   ([ADR-1141](1141-response-types-are-whole-values.md)), the effective default is `code` with JARM. A tenant serving a
   hybrid-flow ecosystem lists `code id_token` itself, and the hybrid ID token carries `s_hash`.
3. **Access tokens are bound through mutual TLS, not DPoP.** FAPI 1.0 names mutual TLS (RFC 8705) as its
   sender-constraining mechanism. The ceiling sets `access_token_sender_constraint_required` and
   `require_tls_client_certificate_bound_access_tokens`, so a token request without a client certificate fails even
   when it carries a DPoP proof. A client that wants DPoP selects `fapi2`.
4. **Defaults.** The profile defaults `tls_client_certificate_bound_access_tokens`, the server's advertised support, to
   `true` where the platform, a team or a tenant selects it, because FAPI 1.0 Advanced §5.2.2-6 requires a server to
   support certificate-bound access tokens. It is a default and not a ceiling, so an operator can turn it off, and
   `CertificateBoundAccessTokenCoherenceRule` reports the clients that the profile makes require binding while it is off
   ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)). The profile also defaults the
   signed ID token, introspection response and authorization response to `PS256`, because the protocol default (RS256)
   is outside the ceiling.
5. **Requirements.**
   - `ConfidentialClientRequirement` refuses a public client at the authorization endpoint.
   - `JwtSecuredAuthorizationResponseRequirement` answers `invalid_request` when a response type returns no
     front-channel ID token and the resolved response mode is not JWT-secured. It judges the handler's
     `SecuredResponseMode`, not a `.jwt` suffix, so it lists no mode and infers nothing from a name.
   - `NonceOrStateRequirement` requires `nonce` with the `openid` scope and `state` without it (§5.2.2.2, §5.2.2.3), so
     every response is bound to the request that asked for it.
   - `IssuerOnlyRequestObjectAudienceRequirement`, which the FAPI 2.0 profile also composes, answers
     `invalid_request_object` when the client sets `request_object_expected_audience`. That client-scoped setting is
     replace-merged, so no ceiling can cap it.

   The client-assertion audience is not narrowed to the issuer, because that rule belongs to FAPI 2.0.
   `AuthorizationRequestProfileContext` carries the `IAuthorizationRequest` and the `AuthorizationResponseDelivery`, so a
   requirement can judge the response type and response mode.
6. **A request object's parameters are the only parameters (RFC 9101 §6.3), for every request object.**
   `AuthorizationRequest` reads every value from the object. `client_id` alone may come from outside when the object
   omits it, and the object must carry a matching one. The object must carry `response_type`, `scope`, `redirect_uri`
   and `client_id`, or the request is refused with `invalid_request_object`. The rule is not limited to FAPI profiles,
   because no client should be able to change a signed request with an unsigned parameter.

   An outer `response_type`, `redirect_uri` or `client_id` is optional, but one that is sent must equal the object's
   value, so a code is never sent to one URI and redeemable only with another. The `redirect_uri` is compared as
   written, because the equality of a parsed `Uri` ignores the case of the host. An outer `scope` is ignored rather
   than compared, because OpenID Connect Core §6.1 lets the object's value supersede it. The rules are one table,
   `RequestObjectParameterPolicy.Rules`.
7. **The response delivery is derived once per request.** `IAuthorizationResponseDeliveryResolver` derives the redirect
   target, the response mode and the state as one `AuthorizationResponseDelivery`.
   - Without an outer `redirect_uri`, the authorization endpoint verifies the object first and the resolver takes the
     three values from it; an error raised before then is shown to the user agent, because no safe redirect target is
     known.
   - With an outer `redirect_uri`, errors go to that URI, and once the object is verified its `state` and
     `response_mode` apply to every later response, so a request whose `state` and `response_mode=jwt` appear only in
     the object gets a signed error that carries its `state` (RFC 6749 §4.1.2.1).
   - The PAR endpoint derives the delivery when the request is pushed, so a `redirect_uri` that is not registered is
     refused at PAR (RFC 9126 §2.1).
8. **Mutual TLS is host composition.** A host that terminates TLS calls `AllowAnyClientCertificate()` in its own
   `ConfigureKestrel(k => k.ConfigureHttpsDefaults(...))`, as the reference host's `HostHttpsDefaults` does. An endpoint
   that requests a client certificate (`Kestrel:EndpointDefaults:ClientCertificateMode`) then accepts any certificate
   at the handshake, and the OP validates it where its meaning is known: the client-authentication handler matches it
   to the client's registered key or validates its chain against the configured trust anchors, and a
   certificate-bound access token is bound to its thumbprint. It is not a library registration, because
   `ConfigureHttpsDefaults` keeps only its last action, so a package that called it would silently replace, or be
   replaced by, the host's own HTTPS defaults.

## Options considered

- **A profile branch in each enforcement point.** Rejected: every point would know the profile by name. Ceilings,
  defaults and requirements keep the profile data.
- **Merging the outer query into the request object, as OpenID Connect Core §6.1 does.** Rejected: an unsigned outer
  parameter could change a signed request, and FAPI 1.0 Advanced, FAPI 2.0 Message Signing and RFC 9101 §6.3 forbid it.
- **DPoP as the sender constraint.** Rejected: FAPI 1.0 names mutual TLS, so a client that wants DPoP selects `fapi2`.
- **A registered profile with request-object-only parameters**, chosen: it needs no new mechanism and applies the same
  rule to every request object.

## Consequences

- A tenant or client selects `fapi1-advanced` through `security_profiles`, as it selects `fapi2`, and a client inherits
  the profile's ceilings wherever it selects the profile in the cascade.
- A parameter sent only outside a request object is lost and belongs inside it, as RFC 9101 requires.
- The conformance harness runs the suite's `fapi1-advanced-final-test-plan` with `run-conformance.ps1 -Fapi1Advanced`:
  `private_key_jwt` and mtls client authentication, the `pushed` request method, and the `jarm` and `plain_response`
  response modes. The seed accepts `Jwks` and `SecurityProfiles`, keys and certificates go to a gitignored folder so no
  private key is committed, and the coverage matrix has a `profile-fapi1-advanced` row. The first end-to-end run is
  pending in [#124](https://github.com/NCodeGroup/identity/issues/124).
- A host that omits `AllowAnyClientCertificate()` has Kestrel refuse every self-signed certificate and every
  certificate from a tenant's own PKI before the OP sees it.

## References

- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md),
  [ADR-1201](1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md),
  [ADR-1203](1203-redirect-uris-are-matched-exactly.md),
  [ADR-1187](1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md),
  [ADR-1141](1141-response-types-are-whole-values.md),
  [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)
- FAPI 1.0 Part 2: Advanced §5.2.2; RFC 9101 §6.3; RFC 9126 §2.1; RFC 8705; RFC 6749 §4.1.2.1
- Issue [#124](https://github.com/NCodeGroup/identity/issues/124)
