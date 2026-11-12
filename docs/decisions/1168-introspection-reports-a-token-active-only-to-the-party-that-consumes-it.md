# 1168. Introspection reports a token active only to the party that consumes it

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

[RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) defines an introspection endpoint that tells a protected resource
whether a token is active and returns its metadata. The response is a flat JSON object with a required boolean `active`
member and any number of optional claims, and an inactive token returns just `{"active": false}`, so the endpoint never
discloses why a token failed. The server issues two token shapes with different natures
([ADR-1028](1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md)): an access
token is a self-contained, signed JWT, and a refresh token is a persisted grant whose active state is the status of a
server-side row. Introspection must answer for both, and it composes services the server already has.

The specifications do not leave the caller rule optional:

- **RFC 7662 §4:** the authorization server **MUST** determine whether the token can be used at the resource server
  making the call, and **SHOULD** limit the scopes it returns to those that apply to that resource server.
- **RFC 9701 §3:** the authorization server **MUST** identify, authenticate, and authorize the resource servers that
  call introspection, and treats each one as an OAuth client.
- **RFC 9701 §5:** a token not intended for the calling resource server is reported `active: false`.

[ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md) makes the server read every access token
it encrypted. If any authenticated client could introspect any token, a token's own client could read exactly what
encryption hides from it ([RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §6). Nothing identified a resource server
as a caller: a resource server owns scopes, settings and keys
([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)) and
is never an authenticating principal.

## Decision

**A resource server introspects through a client bound to it, and introspection reports a token active only to the party
that consumes it.**

1. **The endpoint.** `/oauth2/introspect` follows the mediator convention
   ([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)): a thin
   provider, `DefaultIntrospectionEndpointProvider`, authenticates the client, loads an `ITokenIntrospectionRequest` and
   dispatches an `IntrospectTokenCommand`. Discovery advertises it as `introspection_endpoint`.
   - `DefaultIntrospectTokenHandler` runs at `High` priority and does nothing once an earlier handler marked the token
     active. Further `ICommandHandler<IntrospectTokenCommand>` registrations enrich the result.
   - `IntrospectionResult` carries `active` and flattens its claims into the top-level response through
     `[JsonExtensionData]`, the shape discovery uses for its metadata.
   - A missing `token` is `400 invalid_request`. Every other request is answered `200`, and an inactive token is exactly
     `{"active": false}`.
2. **Resource-server clients.** A client may be bound to one non-system resource server of its tenant, a
   *resource-server client*, the way RFC 9701 §3 treats a resource server as a client. It authenticates through the
   ordinary client authentication path, so every method, the registered-method rule
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method.md)), key policy, `jwks_uri` and mTLS apply
   unchanged. A resource-server client must be confidential, and its resource server enabled; otherwise introspection
   answers `invalid_client`.
3. **The binding is set once, through the management API.**
   - `CreateClientRequest.resourceServerId` binds the client, and the binding cannot change later. Dynamic client
     registration binds a client to the resource server its initial access token is scoped to
     ([ADR-1192](1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)).
   - The resource server must exist in the client's tenant and must not be a system resource server (`400`).
   - The caller must be authorized to update that resource server (`401` / `403`), since a bound client reads every token
     issued to it.
   - The binding is a relational foreign key, `ClientResource.resourceServerId` reports it, and
     `GET …/clients?resourceServerId=` lists a resource server's clients.
4. **A resource-server client is accepted only where an endpoint opts in.** The client resolver refuses a bound client
   unless the matched endpoint carries `IOpenIdEndpointResourceServerClientMetadata` with
   `AcceptsResourceServerClients` (`AcceptResourceServerClients()` on the route). Only introspection opts in, so a bound
   client cannot obtain tokens, authorize, or call any endpoint added later; the restriction fails closed. A request
   without an endpoint, such as background work, never resolves a bound client.
5. **The disclosure rule.**
   - An **access token** is validated through `IIssuedTokenValidator`
     ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)) for a resource-server client only,
     requiring the resource server's identifier in `aud` and adding the resource server's own decryption keys. A token
     whose session is revoked is inactive.
   - A **refresh token** is active only for the client it was issued to, only while its grant is active and before its
     family's horizon
     ([ADR-1297](1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md)).
   - Every other token, and every token presented by a caller it was not meant for, is `{"active": false}`, with no
     other member.
6. **Narrowing.** An active access token's response carries `aud` reduced to the calling resource server's identifier
   and `scope` reduced to that resource server's scopes, and omits `scope` when none remain. A refresh token's `sub` is
   projected for its client
   ([ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md)); an access
   token already carries the projected `sub`.
7. **JWT responses (RFC 9701).**
   - **Selection:** a resource-server client receives `application/token-introspection+jwt` when it lists that media type
     in `Accept`.
   - **Shape:** `typ` is `token-introspection+jwt`. The top-level claims are `iss` (the tenant), `aud` (the resource
     server's identifier) and `iat`, and the response sits under `token_introspection`. There is no top-level `sub` or
     `exp`, so the response cannot pass for an access token.
   - **Protection:** the response is signed with the client's `introspection_signed_response_alg` (default `RS256`, per
     RFC 9701 §6; the FAPI 2.0 profile defaults it to `PS256`) and encrypted when the client registers
     `introspection_encrypted_response_{alg,enc}`. The credential selection and ceilings are those of ID tokens and
     UserInfo responses
     ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).
   - **Refusals (`406`):** an ordinary client asking for a JWT, since RFC 9701 defines it for resource servers only; and
     a resource-server client that registered encryption but asks for JSON, since a response is never weaker than the
     client registered.
8. **Discovery.** The server advertises `introspection_{signing_alg,encryption_alg,encryption_enc}_values_supported`. It
   also mirrors `token_endpoint_auth_methods_supported` and `token_endpoint_auth_signing_alg_values_supported` into the
   `introspection_endpoint_*` and `revocation_endpoint_*` members (RFC 8414 §2), because one client-authentication
   service serves every endpoint.

## Options considered

- **Let any authenticated client introspect any token.** Rejected: it contradicts RFC 7662 §4 and RFC 9701 §5, and a
  token's own client could read what encryption hides from it.
- **Match a caller's client ID against an audience.** Rejected: a resource server's identifier is a URI, not a client,
  and matching the two would invent an implicit binding nobody registered.
- **Authenticate the resource server as a principal of its own**, with credentials on the resource server. Rejected: it
  duplicates client authentication and its hardening on a second principal type, and RFC 9701's response metadata is
  client metadata.
- **Let a resource-server client use other endpoints.** Rejected: RFC 9701 §3 requires an introspecting resource server
  to be restricted to introspection, and a bound client that could request tokens would mint the tokens it is trusted to
  read.
- **Restrict bound clients endpoint by endpoint.** Rejected: an endpoint added later would forget the check. Opt-in
  metadata fails closed.
- **Return a token's full claim set.** Rejected: RFC 7662 §4 and RFC 9701 §5 ask the server to limit the response to the
  caller, and another audience's scopes are not this resource server's business.
- **A fixed, strongly typed response DTO.** Rejected: claims vary by token and by extension handler, and the extension
  dictionary keeps the result open for enrichment without a public-API change per claim.
- **A resource server introspects through a bound client, and a token is active only for its consumer.** Chosen.

## Consequences

- A resource server sees only the tokens meant for it, and only the part of each meant for it. A client sees only its
  own refresh tokens.
- Introspection reflects the same lifetimes and revocation state as the rest of the server: a revoked refresh token and
  a token whose session is revoked report inactive.
- A token encrypted to an imported, public-only key is reported inactive, because the server cannot decrypt it; that
  resource server validates its tokens itself.
- Each introspection call by a resource-server client resolves the resource server and deserializes its keys.
- New claims or richer policies arrive as additional `ICommandHandler<IntrospectTokenCommand>` registrations, not as
  changes to the endpoint or the response type.

## References

- [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662) §2.2, §4; [RFC 9701](https://www.rfc-editor.org/rfc/rfc9701) §3,
  §4, §5, §6, §8.1; [RFC 8414](https://www.rfc-editor.org/rfc/rfc8414) §2;
  [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §6; FAPI 2.0 Security Profile.
- [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md): the validation seam introspection
  calls.
- [ADR-1028](1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md): the
  sibling endpoint whose token-shape analysis this record reuses.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Introspection.DefaultIntrospectionEndpointProvider`,
  `Endpoints.Introspection.Handlers.DefaultIntrospectTokenHandler`, `Clients.DefaultOpenIdClientResolver`,
  `Endpoints.IOpenIdEndpointResourceServerClientMetadata`, `OpenIdEndpointConventionBuilderExtensions`,
  `Clients.ClientJwtProtectionSettings`, `Endpoints.Discovery.Handlers.DefaultDiscoverMetadataHandler`,
  `NCode.Identity.OpenId.Persistence.DataContracts.PersistedClient`,
  `NCode.Identity.OpenId.Management.Endpoints.Clients.ClientApiEndpointHandler`, `DefaultClientValidator`.
