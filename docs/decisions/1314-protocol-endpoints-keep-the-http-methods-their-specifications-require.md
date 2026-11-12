# 1314. Protocol endpoints keep the HTTP methods their specifications require

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md), decision 2, for the authorization, continue and end-session endpoints

## Context

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 2 allows
no state change on a `GET`. The OAuth 2.0 and OpenID Connect specifications require `GET` at two endpoints of an OpenID
Provider:

- RFC 6749 §3.1 and OpenID Connect Core 1.0 §3.1.2.1 require `GET` at the authorization endpoint, and allow `POST`.
- OpenID Connect RP-Initiated Logout 1.0 §2 requires `GET` and `POST` at the `end_session_endpoint`.

A relying party reaches both by redirecting the browser, which is a `GET`. A provider that answers only `POST` breaks
every conforming relying party.

## Decision

The authorization endpoint, `/oauth2/continue` and the `end_session_endpoint` accept `GET` as well as `POST`, whatever
the template's rule says about state changes on a `GET`.

- `/oauth2/continue` resumes the stored authorization request after sign-in, and the interactive surface reaches it by
  redirect, so it accepts `GET`.
- At the authorization endpoint the protections are the protocol's own: PKCE, exact redirect URIs and `state`.
- At the `end_session_endpoint` they are `id_token_hint`, registered `post_logout_redirect_uris`, and the end user's
  confirmation of a logout that arrives without a valid hint for the current session
  ([ADR-1380](1380-a-logout-without-a-valid-id-token-hint-for-the-current-session-is-confirmed.md)).
- Interactive login, consent and logout, the account pages' revocations and the BFF's logout
  ([ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)) use `POST`
  with antiforgery.

## Options considered

- `POST` only. Rejected: it breaks every conforming relying party.
- `GET` at the authorization, continue and end-session endpoints, with `POST` and antiforgery at every other
  browser-facing change. Chosen: it follows the specifications where they require `GET` and keeps the template's rule
  everywhere else.

## Consequences

- A reader finds the exception in this one record, and AGENTS.md lists it with the other overrides of template records.
- The endpoints that accept `GET` rely on the protocol's own protections rather than on antiforgery.

## References

- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749) §3.1,
  [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) §3.1.2.1 and
  [RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) §2.
- [`DefaultEndSessionEndpointProvider.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/DefaultEndSessionEndpointProvider.cs).
