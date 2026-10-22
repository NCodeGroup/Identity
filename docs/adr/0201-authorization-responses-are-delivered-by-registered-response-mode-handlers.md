# 201. Authorization responses are delivered by registered response-mode handlers

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

The authorization result executor delivered a response with a hard-coded `switch` over `query`, `fragment`, and
`form_post`. A package could not add a response mode, and the `response_mode` setting rule listed the three values, so
the vocabulary was closed although nothing made it provably closed
([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).

JWT Secured Authorization Response Mode (JARM) adds `query.jwt`, `fragment.jwt`, `form_post.jwt`, and the `jwt`
shorthand. The response parameters travel as the claims of one JWT in a single `response` parameter. The JWT is issued
by the authorization server, addressed to the client, short-lived, signed, and optionally encrypted. FAPI 2.0 does not
need JARM: its responses carry only `code` and `iss`. FAPI 1.0 Advanced (backlog `F83`) uses JARM as one of its two
permitted responses, and JARM is useful on its own because it lets a client verify the response's origin and integrity.

## Decision

1. **Response modes are an open vocabulary of registered handlers.** The public
   `IAuthorizationResponseModeHandler` names the `response_mode` it delivers and writes the response. A package adds a
   mode by registering a handler, and the built-in handlers are registered with `TryAddEnumerable`. The `response_mode`
   setting rule accepts any whitespace-free name, so it no longer lists the vocabulary. The handlers resolve through
   one internal `IAuthorizationResponseModeProvider`, composed once. Composition refuses a duplicate mode, two handlers
   securing one mode, a `SecuredResponseMode` that names no handler, and a handler named `jwt`. A handler declares its
   traits, `SecuredResponseMode` (the plain mode whose JWT-secured form it delivers) and `DeliversInQuery`; nothing is
   inferred from its name.
2. **An unserved response mode is refused before anything is redirected.** The authorization endpoint answers with
   `400` and no redirect when no registered handler serves the request's response mode, because that mode could not
   carry even an error back to the client. A request object that names an unserved mode is refused as
   `invalid_request`. A tenant's `response_modes_supported` still narrows the served modes: it narrows both the
   requested value as written (so `jwt` must be listed to be requested) and the resolved mode, for an explicit and a
   default mode alike. The check happens once, when the delivery is derived, and a refusal is `400`, not redirected.
3. **The JWT forms derive from the plain forms.** Each JARM handler builds the response JWT and writes the single
   `response` parameter through the same redirect or form-post writer as its plain counterpart. `jwt` resolves to the
   handler that secures the response type's default response mode, which is `fragment.jwt` for a response type that
   returns a token or an ID token and `query.jwt` otherwise (JARM §2.3.4). A handler whose `DeliversInQuery` is set is
   refused for a token-returning response type, so `query.jwt` is refused as `query` is: a token never travels in a
   query string, even inside a JWT ([ADR-0141](0141-response-types-are-whole-values.md)).
4. **The response JWT is short-lived and carries every response parameter.** It is issued by the tenant (`iss`),
   addressed to the client (`aud` = `client_id`), and expires after the client setting
   `authorization_response_lifetime`, counted from `iat`. The setting defaults to 5 minutes and is capped at 10 minutes
   (JARM §2.1), and it merges as a `Min`. Its claims are every serialized response parameter except the RFC 9207 `iss`,
   which is the JWT's own `iss`. An error response is secured in the same way.
5. **The JWT follows the client's registered metadata.** `ClientJwtProtectionSettings.Authorization` adds the JARM
   client metadata `authorization_signed_response_alg` (default `RS256`), `authorization_encrypted_response_alg`, and
   `authorization_encrypted_response_enc` (default `A128CBC-HS256`), plus the discoverable ceilings
   `authorization_signing_alg_values_supported`, `authorization_encryption_alg_values_supported`, and
   `authorization_encryption_enc_values_supported`. The baselines are asymmetric only, as for the other JWTs addressed
   to a client ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). The response is always
   signed and is encrypted only when the client registers an encryption algorithm. Dynamic Client Registration accepts
   and reports the client metadata. The `fapi2` profile caps the signing algorithms at `PS256`/`ES256` and defaults the
   client's to `PS256`. The credentials are selected when the delivery is derived, before the user authenticates, so a
   response that cannot be secured is refused up front, and the handler only reads them from the delivery.
6. **One `AuthorizationResponseDelivery` says how the response is sent.** The internal
   `IAuthorizationResponseDeliveryResolver` derives the public `AuthorizationResponseDelivery`, which holds the
   redirect URI, the handler, the state, and the securing credentials, for the authorization endpoint, the PAR
   endpoint, and the continuation. It is derived once and flows through the processor,
   `ValidateAuthorizationRequestCommand`, `AuthorizationRequestProfileContext`, and `AuthorizationResult`, which
   requires its `OpenIdContext`, its `OpenIdClient`, and the delivery. A handler that protects the response needs the
   tenant's keys and the client's registration, so every result carries both. The executor calls the delivery's
   handler and keeps no registry of its own.

## Consequences

- The default discovery document advertises the four JARM response modes and the authorization signing and encryption
  algorithms. The conformance coverage row `sec-jarm` therefore reports JARM as implemented.
- FAPI 1.0 Advanced (`F83`) can default its tenants to `jwt`-secured responses without further response-mode work.
- A custom response mode, such as `web_message`, is a handler registration and the value in
  `response_modes_supported`. It needs no edit to the library.
- A selector failure, such as a registered algorithm the tenant does not permit or a missing key, raises a
  `server_error` (500, not redirected) when the delivery is derived, before the user authenticates or a code is issued.
  A response is never sent with weaker protection than the client registered.
