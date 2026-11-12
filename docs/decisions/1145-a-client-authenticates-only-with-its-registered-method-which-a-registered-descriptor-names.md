# 1145. A client authenticates only with its registered method, which a registered descriptor names

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A client registers the one method it authenticates with, `token_endpoint_auth_method` (RFC 7591 §2), and that method
decides its type (RFC 6749 §2.1): `none` registers a public client, which has no credentials, and any other method a
confidential client, which must authenticate wherever the server authenticates clients (RFC 6749 §2.3, §3.2.1). A
request may use only one method (RFC 6749 §2.3).

The forces at play:

- A registration is one fact, but a server that checks only a ceiling of permitted methods lets a confidential client
  present just its `client_id`, or the credential of another method, and be served as public.
- The client type is read at two places. The authorization endpoint does not authenticate the client, so it reads the
  registered type; the token endpoint reads the authenticated type. The two must agree.
- The set of methods is open: a package can add one. The setting domains, the advertised list and the decision whether to
  issue a secret must follow the registered methods without a hard-coded list, and reading them must not construct a
  handler, because a handler's dependency graph can read the options that are being validated.
- RFC 6749 §5.2 requires `401` and a `WWW-Authenticate` challenge when a client that authenticated through the
  `Authorization` header fails; an error that carries only a status code cannot deliver the challenge from a handler to
  the rendered response.
- A method verifies against something the client registers: keys, certificates or a certificate identity. Accepting a
  registration without it defers the failure to the first token request.

## Decision

**The registered `token_endpoint_auth_method` is the single fact of how a client authenticates: it is the only method
that authenticates the client, at every endpoint that authenticates clients, it decides the client type, and a registered
descriptor names each method.**

### The registered method

1. **Every endpoint that authenticates a client accepts only its registered method.** `IClientAuthenticationService`
   serves the token, pushed authorization request, introspection, revocation, device-authorization and
   back-channel-authentication endpoints. A client registered as confidential that presents only its `client_id`, or the
   credential of another method, is `invalid_client`. A public client that presents a secret is `invalid_client`.
2. **An unset registration is `client_secret_basic`**, the RFC 7591 default and the setting's default (the setting merges
   by `Replace`), so a platform, team or tenant may configure another default for the clients that register none. A
   client provisioned without a method, through the management API or a seed, is confidential and fails closed: it
   cannot authenticate until it holds a secret. A public client registers `none`. The empty string is not a value.
3. **`token_endpoint_auth_methods_supported` is the ceiling over the registered method**, imposed by the platform, team,
   tenant and security profile. The setting merges by `Intersect` and its default is every described method. A method
   outside the ceiling cannot authenticate, an empty ceiling permits nothing, and registration refuses a method the
   ceiling excludes with `invalid_client_metadata`.
4. **The rule is enforced twice.** Each built-in handler authenticates only a client registered for its method, so a
   refusal is precise and a secret is never compared for a method the client did not register. The service then refuses
   any result whose method is not the registered one within the ceiling, whichever handler produced it, so the guarantee
   holds for a custom `IClientAuthenticationHandler` too.
5. **The authorization endpoint identifies the client; it never authenticates it.** It resolves the client named by
   `client_id`, refuses a request that carries a `client_secret` with `invalid_request` (RFC 6749 §2.3.1 confines it to
   the body of a back-channel request), and reads the registered type, which equals the authenticated type wherever the
   client authenticates.

### The credentials and the challenge

6. **A failed `client_secret_basic` attempt is `401` with `WWW-Authenticate: Basic realm="<tenant issuer>"`**
   (RFC 6749 §5.2). `ClientSecretBasicClientAuthenticationHandler` overrides the virtual `CreateInvalidClientResult`
   that `CommonClientAuthenticationHandler` funnels every `invalid_client` outcome through (default `400`). Body- and
   assertion-based methods stay `400`: the specification scopes the `401` to the header case.
7. **`IOpenIdError` carries response headers** through `ISupportResponseHeaders`, a sibling of `ISupportStatusCode` in
   `NCode.Identity.AspNetCore.Abstractions`. `DefaultOpenIdResultExecutor` writes them before it renders the body. The
   endpoints default only an unset status to `400`, so the handler's `401` and header survive every
   client-authenticating endpoint with no per-endpoint code.
8. **Basic credentials are standard padded Base64** (RFC 7617), sized by an upper-bound buffer and read at their actual
   decoded length. An undecodable value, or one without a colon, is `invalid_client`. `client_secret_post` requires a
   `client_secret`.
9. **`none` is one handler.** It owns a form post that carries a `client_id` and no `client_secret` or
   `client_assertion`, from a client registered as public. The same request from a client registered for another method
   is a mutual-TLS request, whose credential is the certificate, or a confidential client that omitted its credential, so
   the handler leaves it to the others.

### The method descriptors

10. **A method is a descriptor, not something discovered from a handler.** `ClientAuthenticationMethodDescriptor`
    (`NCode.Identity.OpenId.Authentication.Abstractions`, namespace `NCode.Identity.OpenId.Authentication.Clients`) has the
    required members `Name` (the `token_endpoint_auth_method` value and the `AuthenticationMethod` of its handler),
    `RequiresClientSecret` (whether the method verifies a shared secret) and `UsesClientSecretAsKey` (whether the secret is
    the key of an assertion). A descriptor has no dependency, so reading it constructs nothing. Packages contribute
    descriptors through `ICollectionDataSource<ClientAuthenticationMethodDescriptor>`.
    `DefaultClientAuthenticationMethodDataSource` lists, in the order the server advertises them, `client_secret_basic`,
    `client_secret_post`, `private_key_jwt`, `client_secret_jwt`, `tls_client_auth`, `self_signed_tls_client_auth` and
    `none`. A trait is declared and never inferred from a name ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
11. **A catalog reads the descriptors once.** `IClientAuthenticationMethodCatalog` has `MethodNames`, the registered names
    in registration order, and `TryGet`. It throws for an empty name, a name with whitespace or a duplicate name. Names
    compare ordinally. `DefaultClientSettingDescriptorDataSource` reads the catalog: the domain and default of
    `token_endpoint_auth_methods_supported` and the domain of `token_endpoint_auth_method` are `MethodNames`
    ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)), so options validation
    constructs no handler.
12. **Composition checks that handlers and descriptors correspond.** The constructor of
    `DefaultClientAuthenticationService` runs when the endpoints map at host start, outside options validation. It throws
    an `InvalidOperationException` when a handler lacks a descriptor, when two handlers share a method, or when a descriptor
    lacks a handler: an unnamed method would not be advertised, an ambiguous one would be authenticated by either handler,
    and one without a handler would be advertised but could not authenticate.

### Registration

13. **Registration requires what the method verifies against.** `private_key_jwt` requires `jwks` or `jwks_uri`
    (RFC 7523 §2.2). `self_signed_tls_client_auth` requires its certificates there, and a `jwks` given by value needs a key
    with an `x5c` certificate (RFC 8705 §2.2). `tls_client_auth` requires exactly one of `tls_client_auth_subject_dn`,
    `_san_dns`, `_san_uri`, `_san_ip` or `_san_email` (RFC 8705 §2.1.2); those parameters are refused for every other
    method and reported in the registration, read and update responses. A registering client never supplies trust anchors:
    its certificate chains to the tenant's anchors ([ADR-1173](1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)).
14. **Only a method that requires a secret is issued one**, through `IClientSecretIssuer`
    ([ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)), so a key- or
    certificate-based client holds no second, weaker credential. An update that changes the method keeps the stored secret
    and issues none; moving a client to a shared-secret method is a management API operation.
15. **Seeded clients register their method.** The developer single-page applications register `none`; a conformance client
    registers its configured method and no per-client ceiling; the bootstrap administrator registers `client_secret_basic`,
    the method RFC 6749 §2.3.1 requires servers to support, and the API reference's client-credentials login presents it
    with HTTP Basic.

## Options considered

- **Keep checking only the ceiling, and set each client's ceiling to its registered method.** Rejected: it keeps two
  facts that can drift, every writer must remember both, and a ceiling is operator policy, not a client's registration.
- **Treat an unset registration as public.** Rejected: a client provisioned without a method would authenticate with no
  credential, which fails open, and it contradicts the RFC 7591 default.
- **Require a method at registration, with no default.** Rejected: RFC 7591 defines the default, a configurable default
  is a data-driven control for a tenant, and the default is safe because a confidential client without a secret cannot
  authenticate.
- **Enforce only in the client-authentication service.** Rejected as the only layer: a Basic failure must be a `401` with
  a challenge, and a handler should not compare a secret for a method the client did not register. The service stays the
  authoritative backstop.
- **Authenticate the client at the authorization endpoint, with an exception for `none`.** Rejected: that endpoint does
  not authenticate clients, and the exception would keep a credential path open in the front channel.
- **Discover the methods from the handlers.** Rejected: reading them constructs every handler inside options validation,
  and a handler that depends on a service reading `IOptions<OpenIdOptions>.Value` deadlocks host composition in a cycle the
  container cannot detect ([ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)).
- **A strategy class for each method.** Rejected: it duplicates the handler concept for traits that are data.
- **One `400` for every scheme.** Rejected: it violates the RFC 6749 §5.2 `401` requirement for the header case.

## Consequences

- A client registered as confidential is never served as public. Refresh tokens, device codes, back-channel
  authentication, pushed authorization requests, introspection and revocation all require the registered credential, and
  the code-redemption check of [ADR-1144](1144-authorization-codes-are-bound-to-an-s256-pkce-challenge-unless-an-attested-client-uses-the-nonce.md)
  reads a type that is always the registered one.
- A client bound to a resource server authenticates the same way and is accepted only at introspection
  ([ADR-1168](1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md)).
- A FAPI 2.0 client reaches the authorization endpoint, where the profile's rules apply. A `fapi2` tenant refuses
  `client_secret_*` when the client registers rather than at its first token request.
- A management-API client that registers no method is a `client_secret_basic` client; one that authenticates otherwise
  registers its method. A tenant can change the default for its clients.
- A package that adds a method registers a handler and a descriptor of the same name. The setting domains, the advertised
  list and the issuance of a secret follow without another change, and a host that registers one without the other fails
  at start.
- `none` is the only public method; no other method is inferred to be public or confidential from its name.
- The same `IOpenIdError` seam carries the `WWW-Authenticate: Bearer` challenge that sender-constrained access tokens
  need (issue [#45](https://github.com/NCodeGroup/identity/issues/45)).

## References

- [RFC 6749](https://datatracker.ietf.org/doc/html/rfc6749) §2.1, §2.3, §2.3.1, §3.2.1, §5.2;
  [RFC 7591](https://datatracker.ietf.org/doc/html/rfc7591) §2; [RFC 7617](https://datatracker.ietf.org/doc/html/rfc7617) §2;
  [RFC 7523](https://datatracker.ietf.org/doc/html/rfc7523) §2.2; [RFC 8705](https://datatracker.ietf.org/doc/html/rfc8705) §2;
  [RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700) §2.5.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md): an
  unset `*_supported` list is unrestricted; an empty one restricts.
- [ADR-1107](1107-mutual-tls-client-authentication.md): the mutual-TLS handlers own a request only for a client registered
  for their method.
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md): the
  security profile's ceiling.
- [ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md): dynamic
  client registration.
- [ADR-1481](1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md): the API reference.
- `src/NCode.Identity.OpenId.Authentication/Clients/DefaultClientAuthenticationService.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/OpenIdClientRegistrationExtensions.cs`,
  `src/NCode.Identity.OpenId.Authentication/Clients/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/DefaultAuthorizationEndpointHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Settings/DefaultClientSettingDescriptorDataSource.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs`.
