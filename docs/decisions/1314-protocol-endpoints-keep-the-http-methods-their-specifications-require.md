# 1314. Protocol endpoints keep the HTTP methods their specifications require

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md),
  decision 2 only, for the endpoints below

## Context

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 2 allows
no state change on a `GET`. The OAuth 2.0 and OpenID Connect specifications require `GET` at two endpoints of an OpenID
Provider:

- RFC 6749 §3.1 and OpenID Connect Core 1.0 §3.1.2.1 require `GET` at the authorization endpoint, and allow `POST`.
- OpenID Connect RP-Initiated Logout 1.0 §2 requires `GET` and `POST` at the `end_session_endpoint`.

A relying party reaches both by redirecting the browser, which is a `GET`. A provider that answers only `POST` breaks
every conforming relying party.

## Decision

The authorization endpoint, `/oauth2/continue` and the `end_session_endpoint` accept `GET`.

- `/oauth2/continue` resumes the same stored authorization request after sign-in, and the interactive surface reaches it
  by redirect.
- Their protections are the protocols': PKCE, exact redirect URIs and `state` at the authorization endpoint, and
  `id_token_hint` and registered `post_logout_redirect_uris` at the `end_session_endpoint`
  ([ADR-1074](1074-session-management-and-logout.md)).
- Every other browser-facing change uses `POST` with antiforgery: interactive login, consent and logout, the account
  pages' revocations, and the BFF's logout ([ADR-1311](1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md)).

## Options considered

- `POST` only. Rejected: it breaks every conforming relying party.
- A confirmation page for every logout that arrives without an `id_token_hint`. Not chosen here.

## Consequences

- A cross-site top-level navigation to the `end_session_endpoint` without an `id_token_hint` ends the OP session from
  its cookie. RP-Initiated Logout §2 recommends asking the end-user first, and that gap is recorded as pending work in
  the issue tracker.
- A reader sees the exception in one record, and AGENTS.md lists it with the other overrides of template records.

## References

- [RFC 6749](https://www.rfc-editor.org/rfc/rfc6749) §3.1,
  [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) §3.1.2.1 and
  [RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) §2.
- [`DefaultEndSessionEndpointProvider.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/DefaultEndSessionEndpointProvider.cs).
- [NCodeGroup/identity#442](https://github.com/NCodeGroup/identity/issues/442).
