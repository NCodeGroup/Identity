# 1431. The BFF requests offline access only when its deployment lists it

- **Status:** Accepted (amends
  [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) and
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md))
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A BFF session belongs to a user who is present: it starts at a login at the OpenID Provider (OP), it is bound to the
OP's login session by the ID token's `sid`, and it ends when the user logs out at the BFF or when the OP sends a
back-channel logout token for that session
([ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)). The session
refreshes its access token with the refresh token from the code exchange
([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)).

The `offline_access` scope asks for something else. OpenID Connect Core 1.0 §11 defines it as a request for a refresh
token that reaches the user's data "even when the End-User is not present (not logged in)", and OpenID Connect
Back-Channel Logout 1.0 §2.7 says that refresh tokens issued with `offline_access` normally are not revoked when the
user logs out. An OP may therefore keep an offline grant apart from the login session, and Keycloak does: when a login
session is created for a request that includes `offline_access`, Keycloak moves the client to an offline session and
removes the login session, so ending the user's sessions at Keycloak sends the BFF no back-channel logout token, and the
BFF session outlives the OP session. Providers differ in what a refresh token takes: Keycloak issues one to a
confidential client's code exchange without `offline_access`, while the NCode OP
([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)) and several hosted providers issue one
only for `offline_access`.

## Decision

**The BFF requests `openid` and the scopes in `Bff:Scopes`, each once, and nothing else. A deployment lists
`offline_access` in `Bff:Scopes` only when its OP issues a refresh token for that scope alone.**

- The login request never adds `offline_access` on its own. Requesting an offline grant is a decision about the
  provider, so it belongs to the deployment's configuration.
- Against an OP that issues a refresh token without `offline_access`, the BFF's grant stays inside the OP's login
  session, so the OP's logout reaches the BFF through back-channel logout.
- Against an OP that needs `offline_access` for a refresh token, the deployment lists it. The BFF still ends its own
  session at its absolute lifetime, at its idle timeout and on every back-channel logout the OP sends.
- A session that holds no refresh token ends once its access token needs refreshing, and answers `401`
  ([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) point 5).
- This amends ADR-1291 point 6, which requested `offline_access` at every login, and ADR-1294's list of a BFF client's
  registration, where the `offline_access` scope is registered only for a BFF that lists it.

## Options considered

- **Always request `offline_access`.** Rejected: it asks every OP for a grant that outlives the user's login session,
  which is more than a user-present session needs, and an OP that keeps offline grants apart from login sessions, as
  Keycloak does, never tells the BFF that the user logged out.
- **Request `offline_access` only when the OP's discovery document lists it in `scopes_supported`.** Rejected:
  `scopes_supported` says that the OP knows the scope, not whether a refresh token needs it, and Keycloak lists it.
- **List `offline_access` in `Bff:Scopes` when the OP needs it.** Chosen: the deployment knows its OP, the default asks
  for the least the session needs, and the setting already exists.

## Consequences

- A BFF against Keycloak, or any OP that issues refresh tokens for the code exchange, keeps the OP's login session and
  receives its back-channel logout. The end-to-end interop suite proves this against Keycloak.
- A BFF against the NCode OP, or another OP that issues refresh tokens only for `offline_access`, lists it in
  `Bff:Scopes`; without it the session ends when its first access token expires.

## References

- OpenID Connect Core 1.0 §11, Offline Access.
- OpenID Connect Back-Channel Logout 1.0 §2.7, Back-Channel Logout Actions.
- `BffLoginHandler`, `BffOptions.Scopes` in `src/NCode.Identity.Bff`.
- [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1294](1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md).
