# 145. A client authenticates only with its registered method

- **Status:** Accepted (amends [ADR-0046](0046-api-reference-openapi-document-and-scalar.md),
  [ADR-0063](0063-dynamic-client-registration.md), [ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md),
  [ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md),
  [ADR-0140](0140-setting-values-are-validated-against-their-domain.md), and
  [ADR-0144](0144-authorization-codes-are-bound-to-pkce-by-default.md); extended by
  [ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md): a client bound to a resource
  server authenticates the same way and is accepted only at introspection; amended by
  [ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md): the registered method
  descriptors, not the handlers, name the methods)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A client registers the one method it authenticates with — `token_endpoint_auth_method` (RFC 7591 §2, defaulting to
`client_secret_basic`) — and that method decides its type (RFC 6749 §2.1): `none` registers a public client, which has
no credentials, and any other method a confidential client, which must authenticate wherever the server authenticates
clients (RFC 6749 §2.3, §3.2.1). A request may use only one method (RFC 6749 §2.3).

The server did not hold the registration to that:

- The client-authentication service checked the method a client used only against its effective
  `token_endpoint_auth_methods_supported`, whose default is every registered method, `none` included. A client
  registered as confidential — every dynamically registered `client_secret_*` client — could present only its
  `client_id` and be served as public: at the token endpoint for the refresh-token, device-code, and back-channel
  grants, at the pushed authorization request endpoint, and at introspection and revocation. It could equally use any
  other permitted method than the one it registered.
- The client type was two facts: the registered type, which the authorization endpoint reads because it does not
  authenticate the client, and the authenticated type, which the token endpoint reads. They could disagree.
- `none` was spread over three handlers: a query-string handler used only to identify the client at the authorization
  endpoint, and the `client_secret_post` and `client_secret_basic` handlers, which turned a request without a secret
  into a public client. Because the authorization endpoint identified the client through client authentication, it
  refused any client whose ceiling excludes `none` — every FAPI 2.0 client.
- An empty `token_endpoint_auth_methods_supported` — what two disjoint ceilings intersect to — was read as
  unrestricted.
- An unset registration was public at the authorization endpoint and any method at the token endpoint.

## Decision

**The registered `token_endpoint_auth_method` is the single fact of how a client authenticates: it is the only method
that authenticates the client, at every endpoint that authenticates clients, and it decides the client type.**

1. **Every endpoint that authenticates a client accepts only its registered method** — the token, pushed authorization
   request, introspection, revocation, device-authorization, and back-channel-authentication endpoints, through the one
   `IClientAuthenticationService`. A client registered as confidential that presents only its `client_id`, or presents
   the credential of another method, is `invalid_client`. A public client that presents a secret is `invalid_client`.
2. **An unset registration is `client_secret_basic`**, the RFC 7591 default, which dynamic client registration also
   applies. It is the setting's default, so a server or tenant may configure another default for the clients that
   register none (the setting merges by replacement). A client provisioned without a method — through the management API
   or a seed — is therefore confidential and cannot authenticate until it holds a secret: the default fails closed. A
   public client registers `none`. The setting's domain is the names of the registered method descriptors
   ([ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md)); the empty string is not a
   value.
3. **`token_endpoint_auth_methods_supported` is the ceiling over the registered method**, imposed by the server, tenant,
   and security profile (it merges by intersection). A client whose registered method its ceiling excludes cannot
   authenticate, an empty ceiling permits nothing, and dynamic client registration refuses a method the tenant's ceiling
   excludes with `invalid_client_metadata`.
4. **The rule is enforced twice.** Each built-in handler authenticates only a client registered for its method, so a
   refusal is precise — a `client_secret_basic` attempt fails with `401` and a `WWW-Authenticate` challenge (RFC 6749
   §5.2) — and a secret is never compared for a method the client did not register. The client-authentication service
   then refuses any result whose method is not the registered one and permitted by the ceiling, whichever handler
   produced it, so the guarantee holds for a custom `IClientAuthenticationHandler` too.
5. **`none` is one handler.** It owns a form post that carries a `client_id` and no `client_secret` or
   `client_assertion`, from a client registered as public. The same request from a client registered for another method
   is not `none` — it is a mutual-TLS request, whose credential is the certificate, or a confidential client that
   omitted its credential — so the handler leaves it to the others. `client_secret_post` requires a `client_secret`, and
   Basic credentials without a colon are malformed (RFC 7617 §2).
6. **The authorization endpoint identifies the client; it never authenticates it.** It resolves the client named by
   `client_id` and refuses a request that carries a `client_secret`, which RFC 6749 §2.3.1 confines to the body of a
   back-channel request. A rule there that depends on the client type reads the type the client is registered as; an
   endpoint that authenticates the client reads the type it authenticated as, and by rule 1 the two agree.
7. **Seeded clients register their method.** The developer single-page applications register `none`; a conformance
   client registers its configured method and no per-client ceiling; the bootstrap administrator registers
   `client_secret_basic` — the method RFC 6749 §2.3.1 requires servers to support and recommends over body credentials
   — and the API reference's client-credentials login presents it with HTTP Basic.
8. **Stored documents are migrated to the new domain.** A document that registers no method (absent or empty) and whose
   `token_endpoint_auth_methods_supported` lists exactly one method — the earlier way to pin a client's method, such as
   the developer seed's `["none"]` — registers that method; any other empty registration is removed, so it takes the
   default.

## Options considered

- **Keep checking only the ceiling, and set each client's ceiling to its registered method.** Rejected: it keeps two
  facts that can drift, every writer (the management API, every seed, dynamic registration) must remember both, and a
  ceiling is operator policy, not a client's registration.
- **Treat an unset registration as public.** Rejected: a client provisioned without a method would authenticate with no
  credential, which fails open, and it contradicts the RFC 7591 default that dynamic registration already applies.
- **Require a method at registration, with no default.** Rejected: RFC 7591 defines the default, a configurable default
  is a useful data-driven control for a tenant, and the default is safe — a confidential client without a secret cannot
  authenticate.
- **Enforce only in the client-authentication service.** Rejected as the only layer: a Basic failure must be a `401`
  with a challenge, and a handler should not compare a secret for a method the client did not register. It is kept as
  the authoritative backstop.
- **Keep identifying the client at the authorization endpoint through client authentication, with an exception for
  `none`.** Rejected: the authorization endpoint does not authenticate clients, and the exception would keep a credential
  path open in the front channel.
- **Make `OpenIdClient.IsConfidential` mean the registered type everywhere.** Rejected: it guards `ConfidentialClient`,
  the credential the request authenticated with, which exists only where the client authenticates. With the registered
  method enforced, the authenticated type equals the registered type wherever the client authenticates, so the two
  predicates read one fact from what each endpoint knows.

## Consequences

- A client registered as confidential is never served as public. Refresh tokens, device codes, back-channel
  authentication, pushed authorization requests, introspection, and revocation all require the registered credential.
  ADR-0144's binding check at code redemption still reads how the client authenticated, which is now always its
  registered type.
- A FAPI 2.0 client reaches the authorization endpoint, where the profile's rules (PAR, a confidential client) apply.
- A management-API client that registers no method is a `client_secret_basic` client; one that authenticates otherwise
  must register its method. A tenant can change the default for its clients.
- Dynamic client registration accepts every method the tenant's effective `token_endpoint_auth_methods_supported`
  permits (amendment below), so a dynamically registered client can use a strong method.
- The bootstrap administrator authenticates with HTTP Basic, and the API reference sends header credentials.
- The authorization endpoint refuses a request that carries a `client_secret` with `invalid_request`.
- The `RegisterClientAuthenticationMethod` migration rewrites stored settings documents; a deployment storing them
  elsewhere applies the same rule.

## Amendment (2026-10-21): registration accepts the strong methods

Dynamic Client Registration no longer keeps its own list of `none`, `client_secret_basic`, and `client_secret_post`.
The method vocabulary is open: the settings validation accepts every method a registered method descriptor names
([ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md)), and the tenant's effective
`token_endpoint_auth_methods_supported` narrows it, so a `fapi2` tenant refuses `client_secret_*` when the client
registers rather than at its first token request. Registration then requires what the method verifies against:

- `private_key_jwt` requires the client's public keys in `jwks` or `jwks_uri` (RFC 7523 §2.2).
- `self_signed_tls_client_auth` requires its certificates in `jwks` or `jwks_uri`; a `jwks` given by value needs a key
  with an `x5c` certificate (RFC 8705 §2.2).
- `tls_client_auth` requires exactly one `tls_client_auth_subject_dn`, `_san_dns`, `_san_uri`, `_san_ip`, or
  `_san_email` (RFC 8705 §2.1.2); those parameters are refused for every other method. A registering client never
  supplies trust anchors: its certificate chains to the tenant's anchors, and with none configured the method fails
  closed ([ADR-0173](0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)).
- Only the shared-secret methods (`client_secret_basic`, `client_secret_post`, `client_secret_jwt`) are issued a
  `client_secret`. A key- or certificate-based client gets none, so it holds no second, weaker credential.

The registration, read, and update responses report the `tls_client_auth_*` identity. An update that changes the
method keeps the client's stored secret and issues none; switching a client to a shared-secret method is a management
API operation.

## References

- [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749) §2.1, §2.3, §2.3.1, §3.2.1, §5.2;
  [RFC 7591](https://datatracker.ietf.org/doc/html/rfc7591) §2; [RFC 7617](https://datatracker.ietf.org/doc/html/rfc7617) §2;
  [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700) §2.5.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — an unset `*_supported` list is unrestricted; an
  empty one restricts.
- [ADR-0107](0107-mutual-tls-client-authentication.md) — the mutual-TLS handlers already owned a request only for a
  client registered for their method; every handler now does.
- [ADR-0144](0144-authorization-codes-are-bound-to-pkce-by-default.md) — the registered and authenticated client types.
- `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientAuthenticationService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/OpenIdClientRegistrationExtensions.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/DefaultAuthorizationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Settings/DefaultClientSettingDescriptorDataSource.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Host.Database/Migrations/OpenId/20261019163430_RegisterClientAuthenticationMethod.cs`.
