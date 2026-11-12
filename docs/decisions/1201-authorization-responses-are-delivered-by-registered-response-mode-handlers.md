# 1201. Authorization responses are delivered by registered response-mode handlers

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

An authorization server delivers a response by a response mode: `query`, `fragment` or `form_post`. A package that
needs another mode, such as `web_message`, must be able to add one without editing the library. A hard-coded `switch`
over the three values, or a `response_mode` setting rule that lists them, would close a vocabulary that nothing makes
provably closed ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

JWT Secured Authorization Response Mode (JARM) adds `query.jwt`, `fragment.jwt`, `form_post.jwt` and the `jwt`
shorthand. The response parameters travel as the claims of one JWT in a single `response` parameter. The JWT is issued
by the authorization server, addressed to the client, short-lived, signed and optionally encrypted. FAPI 2.0 does not
need JARM: its responses carry only `code` and `iss`. FAPI 1.0 Advanced (issue
[#122](https://github.com/NCodeGroup/identity/issues/122)) uses JARM as one of its two permitted responses, and JARM
is useful on its own because it lets a client verify the response's origin and integrity.

## Decision

Response modes are an open vocabulary of registered handlers, and one derived delivery tells every endpoint how a
response is sent.

1. **Handlers.** The public `IAuthorizationResponseModeHandler` names the `response_mode` it delivers and writes the
   response. A package adds a mode by registering a handler, and the built-in handlers register with
   `TryAddEnumerable`. The `response_mode` setting rule accepts any whitespace-free name, so it does not list the
   vocabulary.
2. **Provider.** The handlers resolve through one internal `IAuthorizationResponseModeProvider`, composed once.
   Composition refuses a duplicate mode, two handlers securing one mode, a `SecuredResponseMode` that names no handler
   and a handler named `jwt`.
3. **Traits.** A handler declares `SecuredResponseMode` (the plain mode whose JWT-secured form it delivers) and
   `DeliversInQuery`. Nothing is inferred from its name.
4. **Unserved modes.** The authorization endpoint answers `400` with no redirect when no registered handler serves the
   request's response mode, because that mode could not carry even an error back to the client. A request object that
   names an unserved mode is `invalid_request`.
5. **Narrowing.** A tenant's `response_modes_supported` narrows the served modes. It narrows both the value as written
   (so `jwt` must be listed to be requested) and the resolved mode, explicit or default. The check happens once, when
   the delivery is derived, and a refusal is `400`, not redirected.
6. **JARM handlers.** The four JARM handlers (`query.jwt`, `fragment.jwt`, `form_post.jwt`, `jwt`) build the response
   JWT and write the single `response` parameter through the same redirect or form-post writers as their plain
   counterparts. `jwt` resolves to the secured form of the response type's default response mode: `fragment.jwt` for a
   response type that returns a token or an ID token, `query.jwt` otherwise (JARM §2.3.4). A handler whose
   `DeliversInQuery` is set is refused for a token-returning response type, so `query.jwt` is refused as `query` is: a
   token never travels in a query string, even inside a JWT
   ([ADR-1141](1141-a-response-type-is-a-whole-value-and-the-listed-values-alone-control-front-channel-tokens.md)).
7. **The response JWT.** It is issued by the tenant (`iss`), addressed to the client (`aud` is the `client_id`) and
   expires after the client setting `authorization_response_lifetime`, counted from `iat`. The setting defaults to 5
   minutes, ranges from 1 second to 10 minutes (JARM §2.1) and merges as a `Min`. Its claims are every serialized
   response parameter except the RFC 9207 `iss`, which is the JWT's own `iss`. An error response is secured the same
   way.
8. **Protection follows the client's metadata.** `ClientJwtProtectionSettings.Authorization` carries the JARM client
   metadata `authorization_signed_response_alg` (default `RS256`), `authorization_encrypted_response_alg` and
   `authorization_encrypted_response_enc` (default `A128CBC-HS256`), within the discoverable ceilings
   `authorization_signing_alg_values_supported`, `authorization_encryption_alg_values_supported` and
   `authorization_encryption_enc_values_supported`. The ceilings are asymmetric only, as for the other JWTs addressed to
   a client ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). The response is always
   signed and is encrypted only when the client registers an encryption algorithm. Dynamic Client Registration accepts
   and reports the metadata. The `fapi2` profile caps signing at `PS256`/`ES256` and defaults the client's to `PS256`.
9. **One delivery.** The internal `IAuthorizationResponseDeliveryResolver` derives the public
   `AuthorizationResponseDelivery`, which holds the redirect URI, the handler, the state and the securing credentials,
   once for the authorization endpoint, the PAR endpoint and the continuation. It flows through
   `ValidateAuthorizationRequestCommand`, `AuthorizationRequestProfileContext` and `AuthorizationResult`, which carries
   its `OpenIdContext`, its `OpenIdClient` and the delivery. A handler that protects the response needs the tenant's
   keys and the client's registration, so every result carries both. The executor calls the delivery's handler and
   keeps no registry of its own.
10. **Credential selection.** The credentials are selected when the delivery is derived, before the user
    authenticates, and the handler only reads them from the delivery. A credential that cannot be selected, such as a
    registered algorithm the tenant does not permit or a missing key, is a `server_error` (`500`, not redirected), so a
    response is never sent with weaker protection than the client registered.

## Options considered

- **A hard-coded `switch` over the three plain modes.** It is the smallest code, but it closes the vocabulary and a
  package cannot add a mode without editing the library.
- **Infer a handler's traits from its name.** A suffix check such as `.jwt` is brittle and hides the rule, so a handler
  declares `SecuredResponseMode` and `DeliversInQuery` explicitly.
- **Select the signing credentials when the response is written.** A response that cannot be secured would then fail
  after the user authenticated and a code was issued. Selecting them when the delivery is derived fails up front.
- **Registered handlers, one provider and one derived delivery (chosen).** The vocabulary stays open, composition
  validates it as a whole, and every endpoint shares one derivation.

## Consequences

- The default discovery document advertises the four JARM response modes and the authorization signing and encryption
  algorithms. The conformance coverage row `sec-jarm` therefore reports JARM as implemented.
- FAPI 1.0 Advanced ([#122](https://github.com/NCodeGroup/identity/issues/122)) can default its tenants to
  `jwt`-secured responses without further response-mode work.
- A custom response mode, such as `web_message`, is a handler registration and a value in `response_modes_supported`.
  It needs no edit to the library.
- A selector failure is a `server_error` before the user authenticates or a code is issued.
- Every `AuthorizationResult` requires its context, client and delivery, which makes the result larger and every
  producer of one supply all three.

## References

- [ADR-1141](1141-a-response-type-is-a-whole-value-and-the-listed-values-alone-control-front-channel-tokens.md): the
  response type and front-channel tokens.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): open vocabularies.
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): JWTs addressed to a client.
- JWT Secured Authorization Response Mode for OAuth 2.0 (JARM), RFC 9207, issue
  [#122](https://github.com/NCodeGroup/identity/issues/122).
