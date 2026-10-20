# 155. JWTs exchanged with a client follow its registered metadata

- **Status:** Accepted (extended by
  [ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md): a JWT introspection response
  follows the client's registered introspection metadata; extended by [ADR-0181](0181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md): the
  credential selector is a public seam every issuer of a client-addressed JWT resolves)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A client and the server exchange JWTs in both directions. The server addresses ID tokens, logout tokens, and UserInfo
responses to the client; the client sends request objects (RFC 9101) and signed CIBA authentication requests to the
server. OpenID Connect Dynamic Client Registration 1.0 §2 lets a client register how each is protected:
`id_token_signed_response_alg` (`RS256` when omitted), `id_token_encrypted_response_alg` / `_enc` (`A128CBC-HS256` when
only the `alg` is registered), the same three for UserInfo (a UserInfo response is plain JSON unless one is registered,
OpenID Connect Core §5.3.2), and `request_object_signing_alg` / `request_object_encryption_alg` / `_enc`. Discovery
advertises the `*_values_supported` lists that bound them.

The server honored almost none of it:

- No setting held the registered algorithms. An ID token was signed with the first tenant key any listed algorithm
  could use, and — when `id_token_encryption_required` was set — encrypted to the **tenant's** key, which the client
  cannot decrypt. A logout token followed the same path.
- The request-object and UserInfo algorithm lists were read only by discovery, so they applied to tenants only
  ([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)). A request object's `alg` was checked only against
  `none`, so the FAPI 2.0 profile's ceiling on request-object signing did not bind, and the UserInfo endpoint returned
  only JSON while discovery advertised signed and encrypted responses (backlog `F63`).
- The UserInfo endpoint accepted the end-user's session cookie in place of an access token. OpenID Connect Core §5.3
  defines it as an OAuth 2.0 protected resource, and a cookie names no client to address a protected response to.
- The encryption lists offered every registered key-management algorithm: `RSA1_5`, which RFC 8725 §3.2 says to avoid,
  and the shared-key algorithms (`A*KW`, `A*GCMKW`, `PBES2-*`), which need a key both parties hold — and a client's
  registration establishes no such key for a response addressed to it, nor the server's for a request object.
- Non-standard `*_encryption_zip_values_supported` lists offered `DEF` compression, which RFC 8725 §3.6 says not to
  combine with encryption, and the CIBA settings offered encrypted authentication requests, which CIBA Core §7.1.1 does
  not define: a signed authentication request uses an asymmetric signature.

## Decision

**A JWT the server addresses to a client is protected exactly as the client registered, within the ceilings of the
client's effective settings; a JWT the server accepts from a client must use an algorithm the client's effective
settings permit. The server never substitutes an algorithm, never encrypts to a key the client did not publish, and
never compresses.**

1. **Registered response metadata are client settings.** `id_token_signed_response_alg`,
   `id_token_encrypted_response_alg`, `id_token_encrypted_response_enc`, `userinfo_signed_response_alg`,
   `userinfo_encrypted_response_alg`, and `userinfo_encrypted_response_enc` are scalar settings that apply to clients
   and merge by `Replace`, so a server or tenant sets the default for its clients and a client overrides it. Their
   defaults are the registration defaults: `RS256` for the ID token signature, `A128CBC-HS256` for an `enc` whose `alg`
   is registered, and no UserInfo protection and no ID token encryption unless registered.
2. **The `*_values_supported` lists are ceilings.** They merge by `Intersect` and apply to tenants and clients, so a
   client's effective lists carry its tenant's lists and the ceiling of every security profile it selects
   ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)). A registered value must be in its list.
   Dynamic client registration rejects one that is not with `invalid_client_metadata`, checking the tenant's effective
   default `id_token_signed_response_alg` as well; issuance checks again, because settings change after registration,
   and fails with `server_error` rather than choose another algorithm.
3. **One selector protects every JWT addressed to a client.** `IClientJwtCredentialSelector` resolves, for an ID token
   (which a logout token follows, OpenID Connect Back-Channel Logout 1.0 §2.4) or a UserInfo response: the tenant key
   for the registered signature algorithm, and an asymmetric public key from the client's `jwks` or `jwks_uri`
   (`IClientKeyResolver`) for the registered key-management and content-encryption algorithms. A token both signed
   and encrypted is a Nested JWT ([ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)).
   `id_token_encryption_required` requires a registered `id_token_encrypted_response_alg`; it never picks one. A logout
   token the server cannot protect as registered is not sent, and the skip is logged.
4. **Encryption to a party uses its public key.** The ID token, UserInfo, and request-object encryption lists admit only
   asymmetric key management (`RSA-OAEP*`, `ECDH-ES*`), and `RSA1_5` is outside every algorithm setting's domain.
   Dynamic client registration requires `jwks` or `jwks_uri` (not both) when a client registers an encrypted response,
   and an `*_enc` only with its `*_alg`.
5. **UserInfo is a protected resource.** It authenticates only an access token — in the `Authorization` header or the
   form body — whose audience includes the OpenID system resource server (`AuthenticateResourceRequestCommand`); the
   session cookie no longer authenticates it. The response is protected as the client named by the token's `client_id`
   registers; an unknown or disabled client yields `401` `invalid_token`. A JWT response is `application/jwt` with
   `iss`, `aud` (the client), `iat`, `nbf`, and an `exp` of the client's ID token lifetime.
6. **Inbound algorithms are an allow-list that fails closed.** A request object is verified with the client's keys and
   decrypted with the tenant's, under `request_object_signing_alg_values_supported` and
   `request_object_encryption_{alg,enc}_values_supported` read from the client's effective settings; a registered
   `request_object_signing_alg` / `request_object_encryption_alg` / `_enc` narrows the client's list to that one value.
   A registered `token_endpoint_auth_signing_alg` and `backchannel_authentication_request_signing_alg` narrow the
   lists named below the same way.
   A signed CIBA request is verified under `backchannel_authentication_request_signing_alg_values_supported`, whose
   domain is asymmetric signatures, and an encrypted one is rejected; a client assertion (`private_key_jwt`,
   `client_secret_jwt`) under `token_endpoint_auth_signing_alg_values_supported`. At these trust boundaries an **unset**
   list permits nothing (`GetPermittedAlgorithms`) — the one exception to ADR-0010's "unset is unrestricted": the root
   settings always supply each list from the registered algorithms, so an unset list can only be a removed one, and it
   must not widen acceptance to every algorithm a JOSE implementation registers.
7. **Retired settings.** The four `*_encryption_zip_values_supported` lists and
   `backchannel_authentication_request_encryption_{alg,enc,zip}_values_supported` are removed. The
   `RetireUnsafeJoseSettingValues` migration removes them from every stored settings document and strips the values the
   narrowed domains no longer admit, keeping an emptied list empty.

## Options considered

- **Stop advertising and retire the UserInfo algorithm settings** (the alternative `F63` offered). Rejected: signed and
  encrypted UserInfo responses are part of OpenID Connect Core and of the Auth0-parity-plus surface
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)); implementing them is the complete model.
- **Keep choosing algorithms from the lists.** Rejected: a client cannot predict the result, so it cannot pin the
  algorithm it verifies with, and a list says nothing about which party's key receives an encrypted response.
- **Fall back to a permitted algorithm, or pick an encryption algorithm when encryption is required.** Rejected: the
  client verifies and decrypts with what it registered; a silent substitution either fails at the client or weakens what
  it expects. Failing at registration or issuance names the misconfiguration.
- **Encrypt with a key derived from the client secret** (OpenID Connect Core §10.2). Rejected: it ties confidentiality
  to a shared secret the server also stores and that a client may not have (`private_key_jwt`, mutual TLS); an
  asymmetric key the client publishes is available to every client type.
- **Keep the session-cookie fallback at UserInfo.** Rejected: an ambient credential at a protected resource, and no
  client to address a protected response to.
- **Keep compression or CIBA request encryption behind an opt-in.** Rejected: both are unsafe or non-standard, and an
  opt-in invites enabling them.

## Consequences

- Discovery advertises only what the server enforces and implements; the FAPI 2.0 ceilings bind request objects, ID
  tokens, and UserInfo responses for every client that selects the profile.
- A client receives an encrypted ID token or UserInfo response it can decrypt, and verifies a signature with the
  algorithm it registered.
- A tenant whose ceiling excludes `RS256` — the FAPI 2.0 profile's does — sets `id_token_signed_response_alg` for its
  clients, or each client registers one; otherwise registration is refused and issuance fails with `server_error`
  (backlog `F68`).
- A relying party that called UserInfo with only the end-user's session cookie must present an access token.
- A dynamically registered client can narrow its assertion algorithm and use CIBA without the management API. Its
  `backchannel_token_delivery_mode` is checked against the tenant's `backchannel_token_delivery_modes_supported`; its
  `backchannel_client_notification_endpoint` is stored as its `client_notification_endpoint`, which `ping` and `push`
  require; and its `backchannel_user_code_parameter` is stored as its own `backchannel_user_code_parameter_supported`,
  which the tenant's value bounds, so a client claims the parameter only where the tenant supports it.
- **Breaking:** the six registration settings are new; the seven retired settings are removed;
  `AuthenticateSubjectCommand` authenticates only the session and `AuthenticateResourceRequestCommand` is new;
  `RegisterClientRequest` and `RegisterClientResponse` gain the response and request-object metadata, the
  `token_endpoint_auth_signing_alg`, and the CIBA registration metadata (CIBA Core 1.0 §4). A deployment that
  stores settings documents in its own database applies the same migration there.
- Follow-ups: request-object audience, typing, and lifetime (backlog `F40`); validating client key material at every
  entry point (`F65`); the server validating access tokens it encrypted
  (`F67`); and checking a client's registered response algorithms when its settings are written (`F68`).

## References

- [OpenID Connect Dynamic Client Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html) §2;
  [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) §5.3, §6, §10;
  [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) §2.4;
  [CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html) §7.1.1;
  [RFC 9101](https://www.rfc-editor.org/rfc/rfc9101); [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) §3.2, §3.6.
- [ADR-0154](0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) — the validator the
  inbound policies configure.
- [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md) (amended: the request-object, UserInfo, and ID token
  lists apply to clients), [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) (amended: an unset list at a
  trust boundary permits nothing), [ADR-0083](0083-secure-by-default-discovery-metadata.md),
  [ADR-0085](0085-ciba-grant.md) and [ADR-0090](0090-durable-outbound-delivery-dispatch-adapter.md) (amended: no
  encrypted CIBA requests), [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md).
- Code: `NCode.Identity.OpenId.Authentication.Clients.DefaultClientJwtCredentialSelector`, `ClientJwtProtectionSettings`,
  `NCode.Identity.OpenId.Authentication.Settings.PermittedAlgorithmExtensions`,
  `NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.DefaultUserInfoEndpointProvider`,
  `NCode.Identity.OpenId.Authentication.Subject.AuthenticateResourceRequestCommand`, `DefaultLoadAuthorizationRequestHandler`,
  `DefaultLoadBackchannelAuthenticationRequestHandler`, `ClientAssertionClientAuthenticationHandler`,
  `DefaultClientRegistrationEndpointHandler`,
  `DefaultTokenService`, `DefaultBackChannelLogoutNotifier`, `NCode.Identity.OpenId.Settings.SettingAlgorithms`,
  `DefaultSettingDescriptorDataSource`, and the `RetireUnsafeJoseSettingValues` migration.
