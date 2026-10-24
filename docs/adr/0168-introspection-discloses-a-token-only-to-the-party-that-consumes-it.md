# 168. Introspection discloses a token only to the party that consumes it

- **Status:** Accepted (amends [ADR-0029](0029-token-introspection-endpoint.md); extends
  [ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md) and
  [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md))
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

[ADR-0029](0029-token-introspection-endpoint.md) let any authenticated client introspect any token and deferred an
audience rule. The specifications do not leave that rule optional:

- **RFC 7662 §4:** the authorization server **MUST** determine whether the token can be used at the resource server
  making the call, and **SHOULD** limit the scopes it returns to those that apply to that resource server.
- **RFC 9701 §3:** the authorization server **MUST** identify, authenticate, and authorize the resource servers that
  call introspection, and treats each one as an OAuth client.
- **RFC 9701 §5:** a token not intended for the calling resource server is reported `active: false`.

[ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md) makes the server read every access token it
encrypted. Under ADR-0029's rule a token's own client could then introspect it and read exactly what encryption hides
from it ([RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §6). Nothing identified a resource server as a caller: a
resource server was a settings and secret owner ([ADR-0066](0066-resource-server-settings-and-secret-owner.md)), never an
authenticating principal.

## Decision

**A resource server introspects through a client bound to it, and introspection reports a token active only to the party
that consumes it.**

1. **Resource-server clients.** A client may be bound to one resource server of its tenant — a *resource-server client*
   — the way RFC 9701 §3 treats a resource server as a client. It authenticates through the ordinary client
   authentication path, so every method, the registered-method rule
   ([ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md)), key policy, `jwks_uri`, and mTLS apply
   unchanged. A resource-server client must be confidential, and its resource server enabled; otherwise introspection
   answers `invalid_client`.
2. **The binding is set once, through the management API.**
   - `CreateClientRequest.resourceServerId` binds the client, and the binding cannot change later. Dynamic client
     registration binds a client to the resource server its initial access token is scoped to
     ([ADR-0192](0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)).
   - The resource server must exist in the client's tenant and must not be a system resource server (`400`).
   - The caller must be authorized to update that resource server (`401` / `403`), since a bound client reads every token
     issued to it.
   - The binding is a relational foreign key: a resource server with bound clients cannot be deleted (`409`).
   - `ClientResource.resourceServerId` reports the binding, and `GET …/clients?resourceServerId=` lists a resource
     server's clients.
3. **A resource-server client is accepted only where an endpoint opts in.** The client resolver refuses a bound client
   unless the matched endpoint carries `IOpenIdEndpointResourceServerClientMetadata` with
   `AcceptsResourceServerClients` (`AcceptResourceServerClients()` on the route). Only introspection opts in, so a bound
   client cannot obtain tokens, authorize, or call any endpoint added later; the restriction fails closed. A request
   without an endpoint, such as background work, never resolves a bound client.
4. **The disclosure rule.**
   - An **access token** is validated through `IIssuedTokenValidator` for a resource-server client only, requiring the
     resource server's identifier in `aud` and adding the resource server's own keys for decryption.
   - A **refresh token** is active only for the client it was issued to.
   - Every other token, and every token presented by a caller it was not meant for, is `active: false`, with no other
     member.
5. **Narrowing.** An active access token's response carries `aud` reduced to the calling resource server's identifier
   and `scope` reduced to that resource server's scopes, and omits `scope` when none remain. A refresh token's `sub` is
   projected for its client, as the server projects subjects everywhere else; an access token already carries the
   projected `sub`.
6. **JWT responses (RFC 9701).**
   - **Selection:** a resource-server client receives `application/token-introspection+jwt` when it lists that media type
     in `Accept`.
   - **Shape:** `typ` is `token-introspection+jwt`. The top-level claims are `iss` (the tenant), `aud` (the resource
     server's identifier), and `iat`, and the response sits under `token_introspection`. There is no top-level `sub` or
     `exp`, so the response cannot pass for an access token.
   - **Protection:** the response is signed with the client's `introspection_signed_response_alg` (default `RS256`, per
     RFC 9701 §6) and encrypted when the client registers `introspection_encrypted_response_{alg,enc}`. The same
     credential selection and ceilings as ID tokens and UserInfo responses apply
     ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)); FAPI 2.0 adds an introspection
     signing ceiling.
   - **Refusals (`406`):** an ordinary client asking for a JWT, since RFC 9701 defines it for resource servers only; and
     a resource-server client that registered encryption but asks for JSON, since a response is never weaker than the
     client registered.
7. **Discovery.** The server advertises `introspection_{signing_alg,encryption_alg,encryption_enc}_values_supported`. It
   also mirrors `token_endpoint_auth_methods_supported` and `token_endpoint_auth_signing_alg_values_supported` into the
   `introspection_endpoint_*` and `revocation_endpoint_*` members (RFC 8414 §2), because one client-authentication
   service serves every endpoint.

## Options considered

- **Keep ADR-0029's rule and add an audience check for callers whose client ID matches an audience.** Rejected: a
  resource server's identifier is a URI, not a client, and matching the two would invent an implicit binding nobody
  registered.
- **Authenticate the resource server as its own principal**, with credentials on the resource server. Rejected: it
  duplicates client authentication and its hardening on a second principal type, and RFC 9701's response metadata is
  client metadata.
- **Let a resource-server client use other endpoints.** Rejected: RFC 9701 §3 requires an introspecting resource server
  to be restricted to introspection, and a bound client that could request tokens would mint the tokens it is trusted to
  read.
- **Restrict bound clients endpoint by endpoint.** Rejected: an endpoint added later would forget the check. Opt-in
  metadata fails closed.
- **Return the token's full claim set to its resource server.** Rejected: RFC 7662 §4 and RFC 9701 §5 ask the server to
  limit the response to the caller, and another audience's scopes are not this resource server's business.

## Consequences

- A resource server sees only the tokens meant for it, and only the part of each meant for it. A client sees only its
  own refresh tokens.
- A resource server reads encrypted access tokens through introspection when they were encrypted to the tenant or to its
  own keys. When a token is encrypted to an imported, public-only key, the server cannot decrypt it and reports it
  inactive; that resource server validates its tokens itself.
- Under FAPI 2.0, whose ceiling excludes `RS256`, a resource-server client registers an explicit
  `introspection_signed_response_alg`; this is the same gap backlog `F68` tracks for ID tokens.
- Each introspection call by a resource-server client resolves its resource server and deserializes its keys.
- **Breaking:** an integration that introspects access tokens as an ordinary client now receives `active: false` and
  must register a resource-server client. `IntrospectTokenCommand` gains the caller's `ResolvedResourceServer`;
  `OpenIdClient` gains `ResourceServerId`; `IOpenIdClientFactory` creates clients from an `OpenIdClientDefinition`;
  `PersistedClient`, `IClientStore`, `CreateClientRequest`, and `ClientResource` gain the binding; the client store
  gains a migration.

## References

- [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) §2.2, §4; [RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) §3,
  §4, §5, §6, §8.1; [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) §2;
  [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §6; FAPI 2.0 Security Profile.
- [ADR-0029](0029-token-introspection-endpoint.md) — the endpoint whose caller rule this replaces.
- [ADR-0167](0167-the-server-validates-the-tokens-it-issued-under-one-policy.md) — the validation seam introspection
  calls.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Introspection.DefaultIntrospectionEndpointProvider`,
  `Endpoints.Introspection.Handlers.DefaultIntrospectTokenHandler`, `Clients.DefaultOpenIdClientResolver`,
  `Endpoints.IOpenIdEndpointResourceServerClientMetadata`, `OpenIdEndpointConventionBuilderExtensions`,
  `Clients.ClientJwtProtectionSettings`, `Endpoints.Discovery.Handlers.DefaultDiscoverMetadataHandler`,
  `NCode.Identity.OpenId.Persistence.DataContracts.PersistedClient`,
  `NCode.Identity.OpenId.Management.Endpoints.Clients.ClientApiEndpointHandler`, `DefaultClientValidator`.
