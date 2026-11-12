# 1155. JWTs exchanged with a client follow its registered metadata within its effective ceilings

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A client and the server exchange JWTs in both directions. The server addresses ID tokens, logout tokens, UserInfo
responses and JWT introspection responses to the client; the client sends request objects (RFC 9101) and signed CIBA
authentication requests to the server. OpenID Connect Dynamic Client Registration 1.0 §2 lets a client register how each
is protected: `id_token_signed_response_alg` (`RS256` when omitted), `id_token_encrypted_response_alg` / `_enc`
(`A128CBC-HS256` when only the `alg` is registered), the same three for UserInfo (a UserInfo response is plain JSON
unless one is registered, OpenID Connect Core §5.3.2), and `request_object_signing_alg` /
`request_object_encryption_alg` / `_enc`. Discovery advertises the `*_values_supported` lists that bound them.

The forces:

- A client can only verify with the algorithm it registered and decrypt with a key it published. A token signed with
  whichever tenant key some listed algorithm could use, or encrypted to the tenant's own key, is unusable by the client.
- Request-object and UserInfo algorithm lists must bind clients, not only tenants
  ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), so a security
  profile's ceiling on request-object signing holds and discovery advertises only what the server implements.
- OpenID Connect Core §5.3 defines UserInfo as an OAuth 2.0 protected resource. A session cookie names no client to
  address a protected response to.
- The encryption lists must not offer `RSA1_5`, which RFC 8725 §3.2 says to avoid, nor the shared-key algorithms
  (`A*KW`, `A*GCMKW`, `PBES2-*`), which need a key both parties hold: a client's registration establishes no such key
  for a response addressed to it, nor the server's for a request object.
- RFC 8725 §3.6 says not to combine compression with encryption, and CIBA Core §7.1.1 defines no encrypted
  authentication request: a signed one uses an asymmetric signature.
- `ITokenService` is public so a deployment can replace it, and the ID token is one of its products. A replacement that
  cannot reach the credential selector must either reproduce the policy and drift from it, or protect the ID token more
  weakly than the client registered. The access token has the same shape, and `IAccessTokenPolicyService` is the public
  seam a replacement resolves
  ([ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md)).

## Decision

**A JWT the server addresses to a client is protected exactly as the client registered, within the ceilings of the
client's effective settings; a JWT the server accepts from a client must use an algorithm the client's effective
settings permit. The server never substitutes an algorithm, never encrypts to a key the client did not publish, and
never compresses.**

1. **Registered response metadata are client settings.** `id_token_signed_response_alg`,
   `id_token_encrypted_response_alg`, `id_token_encrypted_response_enc`, `userinfo_signed_response_alg`,
   `userinfo_encrypted_response_alg` and `userinfo_encrypted_response_enc` are scalar settings that apply to clients and
   merge by `Replace`, so a server or tenant sets the default for its clients and a client overrides it. Their defaults
   are the registration defaults: `RS256` for the ID token signature, `A128CBC-HS256` for an `enc` whose `alg` is
   registered, and no UserInfo protection and no ID token encryption unless registered.
2. **The `*_values_supported` lists are ceilings.** They merge by `Intersect` and apply to tenants and clients, so a
   client's effective lists carry its tenant's lists and the ceiling of every security profile it selects
   ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
   A registered value must be in its list, and a security profile supplies a permitted default (FAPI 2.0 supplies
   `PS256`), so a client under that profile that registers none gets `PS256`.
3. **A registered coherence rule checks the writes.** `ResponseAlgorithmCoherenceRule`
   ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md))
   enforces decision 2:
   - Dynamic client registration refuses an out-of-list value with `invalid_client_metadata`, checking the tenant's
     effective default `id_token_signed_response_alg` as well.
   - A management API client write refuses a registered ID token, UserInfo or introspection algorithm, or the default
     `enc` of a registered encryption, that its list excludes, with a problem naming the setting.
   - A platform, team or tenant settings write merges each client it governs onto its effective settings before and
     after the change and refuses a change that newly excludes an algorithm a client registers. A mismatch that predates
     the change is not reported, so it never blocks an unrelated write. A platform write merges onto the settings that
     `OpenIdPlatform.ConfiguredSettingsProvider` exposes.
   - The merge reads tenants and clients as untracked, secret-free projections and only when a setting the rule reads
     changed, so a change to an unrelated setting costs no client walk (issue
     [#212](https://github.com/NCodeGroup/identity/issues/212)).
   - Issuance checks again, because settings change after they are written, and fails with `server_error` rather than
     choose another algorithm. A change to the host's configured settings is not checked against clients; issuance is
     the backstop.
4. **One public selector protects every JWT addressed to a client.** `IClientJwtCredentialSelector`, the
   `ClientJwtCredentials` it returns and the `ClientJwtProtectionSettings` that describe each kind of JWT are public in
   `NCode.Identity.OpenId.Authentication.Abstractions`, in the `NCode.Identity.OpenId.Authentication.Clients`
   namespace, beside `ITokenService`.
   - The selector returns the tenant key for the registered signature algorithm and an asymmetric public key from the
     client's `jwks` or `jwks_uri` (`IClientKeyResolver`) for the registered key-management and content-encryption
     algorithms. A token both signed and encrypted is a Nested JWT
     ([ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)).
   - Every issuer of a client-addressed JWT resolves the selector. A replacement `ITokenService` protects its ID tokens
     with the credentials the selector returns for `ClientJwtProtectionSettings.IdToken`.
   - The default, `DefaultClientJwtCredentialSelector`, is internal and registered only when no other is, so a
     deployment can extend the selection, for example to choose among the client's keys by a further rule. A replacement
     must not weaken the protection the client registered.
   - `ClientJwtProtectionSettings` is an open description
     ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): `IdToken` (which the logout token follows,
     OpenID Connect Back-Channel Logout 1.0 §2.4), `UserInfo`, `Introspection`
     ([ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md)) and `Authorization`
     ([ADR-1201](1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)) are the server's
     kinds, and a package that addresses a further kind of JWT to a client describes it with a new instance.
   - `id_token_encryption_required` requires a registered `id_token_encrypted_response_alg`; it never picks one. A
     logout token the server cannot protect as registered is not sent, and the skip is logged.
5. **Encryption to a party uses its public key.** The ID token, UserInfo and request-object encryption lists admit only
   asymmetric key management (`RSA-OAEP*`, `ECDH-ES*`), and `RSA1_5` is outside every algorithm setting's domain. There
   is no compression. Dynamic client registration requires `jwks` or `jwks_uri` (not both) when a client registers an
   encrypted response, and an `*_enc` only with its `*_alg`.
6. **UserInfo is a protected resource.** It authenticates only an access token, in the `Authorization` header or the
   form body, whose audience includes the OpenID system resource server (`AuthenticateResourceRequestCommand`;
   `AuthenticateSubjectCommand` authenticates only the session). The response is protected as the client named by the
   token's `client_id` registers; an unknown or disabled client yields `401` `invalid_token`. A JWT response is
   `application/jwt` with `iss`, `aud` (the client), `iat`, `nbf` and an `exp` of the client's ID token lifetime.
7. **Inbound algorithms are an allow-list that fails closed.** The lists come from the client's effective settings:
   - A request object is verified with the client's keys and decrypted with the tenant's, under
     `request_object_signing_alg_values_supported` and `request_object_encryption_{alg,enc}_values_supported`.
   - A signed CIBA request is verified under `backchannel_authentication_request_signing_alg_values_supported`, whose
     domain is asymmetric signatures; an encrypted one is refused.
   - A client assertion (`private_key_jwt`, `client_secret_jwt`) is verified under
     `token_endpoint_auth_signing_alg_values_supported`.
   - A registered single algorithm (`request_object_signing_alg`, `request_object_encryption_alg` / `_enc`,
     `token_endpoint_auth_signing_alg`, `backchannel_authentication_request_signing_alg`) narrows its list to that one
     value.
   - At these trust boundaries an **unset** list permits nothing (`GetPermittedAlgorithms`), the exception to
     [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)'s
     "unset is unrestricted": the root settings always supply each list from the registered algorithms, so an unset list
     can only be a removed one, and it must not widen acceptance to every algorithm a JOSE implementation registers.
8. **Dynamic registration carries this metadata.** It carries the response and request-object metadata, the
   `token_endpoint_auth_signing_alg` and CIBA's (CIBA Core 1.0 §4): `backchannel_token_delivery_mode` is checked against
   the tenant's `backchannel_token_delivery_modes_supported`; `backchannel_client_notification_endpoint` is stored as the
   client's `client_notification_endpoint`, which `ping` and `push` require; and `backchannel_user_code_parameter` is
   stored as the client's `backchannel_user_code_parameter_supported`, which the tenant's value bounds, so a client
   claims the parameter only where the tenant supports it
   ([ADR-1085](1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md)).

## Options considered

- **Stop advertising and retire the UserInfo algorithm settings** (the alternative issue
  [#102](https://github.com/NCodeGroup/identity/issues/102) offered). Rejected: signed and encrypted UserInfo responses
  are part of OpenID Connect Core and of the Auth0-parity-plus surface
  ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md));
  implementing them is the complete model.
- **Keep choosing algorithms from the lists.** Rejected: a client cannot predict the result, so it cannot pin the
  algorithm it verifies with, and a list says nothing about which party's key receives an encrypted response.
- **Fall back to a permitted algorithm, or pick an encryption algorithm when encryption is required.** Rejected: the
  client verifies and decrypts with what it registered; a silent substitution either fails at the client or weakens what
  it expects. Failing at registration or issuance names the misconfiguration.
- **Encrypt with a key derived from the client secret** (OpenID Connect Core §10.2). Rejected: it ties confidentiality
  to a shared secret the server also stores and that a client may not have (`private_key_jwt`, mutual TLS); an
  asymmetric key the client publishes is available to every client type.
- **Keep the session cookie at UserInfo.** Rejected: an ambient credential at a protected resource, and no client to
  address a protected response to.
- **Offer compression or CIBA request encryption as opt-ins.** Rejected: both are unsafe or non-standard, and an opt-in
  invites enabling them.
- **Keep the selector internal and document the policy.** Rejected: every replacement reimplements the policy, and the
  copies diverge as the policy changes.
- **Expose only the default implementation.** Rejected: a concrete class fixes the policy's dependencies into the public
  API, and a deployment could not replace the policy without replacing every issuer.
- **A public selector seam with an internal default, protecting every client-addressed JWT, and registered metadata
  bounded by effective ceilings.** Chosen.

## Consequences

- Discovery advertises only what the server enforces and implements; the FAPI 2.0 ceilings bind request objects, ID
  tokens and UserInfo responses for every client that selects the profile.
- A client receives an encrypted ID token or UserInfo response it can decrypt, and verifies a signature with the
  algorithm it registered.
- A tenant whose ceiling excludes `RS256` sets `id_token_signed_response_alg` for its clients, or each client registers
  one; otherwise registration and a management write are refused.
- A platform, team or tenant write that narrows a list beneath a client's registered value is refused, at the cost of
  reading every client the write governs. A seeded client registers no response algorithm, so it can only inherit a
  mismatch from the settings above it, which their writes check.
- A relying party calls UserInfo with an access token; the end-user's session cookie authenticates nothing there.
- A dynamically registered client can narrow its assertion algorithm and use CIBA without the management API.
- A replacement `ITokenService` protects ID tokens exactly as the default does without depending on internals.
- The three selector types are public API.

## References

- [OpenID Connect Dynamic Client Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html) §2;
  [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) §5.3, §6, §10;
  [OpenID Connect Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html) §2.4;
  [CIBA Core 1.0](https://openid.net/specs/openid-client-initiated-backchannel-authentication-core-1_0.html) §4, §7.1.1;
  [RFC 9101](https://www.rfc-editor.org/rfc/rfc9101); [RFC 8725](https://www.rfc-editor.org/rfc/rfc8725) §3.2, §3.6.
- [ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) (the validator the
  inbound policies configure),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md),
  [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md),
  [ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md) (the
  access-token counterpart),
  [ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md),
  [ADR-1201](1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md).
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientJwtCredentialSelector`, `ClientJwtCredentials`,
  `ClientJwtProtectionSettings` (Authentication.Abstractions); `DefaultClientJwtCredentialSelector`,
  `NCode.Identity.OpenId.Authentication.Clients.Coherence.ResponseAlgorithmCoherenceRule`,
  `NCode.Identity.OpenId.Authentication.Settings.PermittedAlgorithmExtensions`,
  `NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.DefaultUserInfoEndpointProvider`,
  `NCode.Identity.OpenId.Authentication.Subject.AuthenticateResourceRequestCommand`, `DefaultLoadAuthorizationRequestHandler`,
  `DefaultLoadBackchannelAuthenticationRequestHandler`, `ClientAssertionClientAuthenticationHandler`,
  `DefaultClientRegistrationEndpointHandler`, `DefaultTokenService`, `DefaultBackChannelLogoutNotifier`,
  `NCode.Identity.OpenId.Settings.SettingAlgorithms` and `DefaultSettingDescriptorDataSource`.
