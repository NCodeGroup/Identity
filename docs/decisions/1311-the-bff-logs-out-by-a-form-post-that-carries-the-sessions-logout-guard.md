# 1311. The BFF logs out by a form post that carries the session's logout guard

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
- **Supersedes:** decision 1 of
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)

## Context

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 2 allows
no state change on a `GET`. A `GET` link that ends the session runs on a browser's prefetch, and a link that carries
the guard puts it into history, server logs and the `Referer` header. A `fetch` cannot follow the redirect to the
provider's end-session endpoint, so logout cannot be a script-driven request either.

[ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md) decision 1 made
logout a `GET` that carries the guard in its query string.

## Decision

**`POST /bff/logout` ends the session only when its form body carries the session's logout guard, then sends the
browser to the provider's end-session endpoint.**

- The request is a form post. `sid` must appear once and equal the logout guard, compared in constant time.
  `returnUrl` is optional, appears at most once and is allow-listed. A request that is not a form post answers `400`,
  and a `sid` in the query string is ignored.
- `/bff/user` returns `logoutUrl` (a path only) and `logoutSid`, both reserved field names. A frontend renders
  `<form method="post" action="{logoutUrl}">` with a hidden `sid` input that holds `logoutSid`.
- The guard is the form's synchronizer token
  ([project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 3).
  A cross-site post carries no session cookie, and a same-site one does not know the guard.
- A form post is a top-level navigation, so the browser follows the redirect to the provider.
- Everything else in ADR-1293 stands: the idempotent logout without a session, the revocation, the audit record and the
  end-session redirect.

## Options considered

- Keep `GET` with the guard in the query string (ADR-1293). Rejected: it changes state on a `GET`
  ([project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 2).
- A `fetch` `POST` with the static anti-forgery header. Rejected: a `fetch` cannot follow the redirect to the
  provider's end-session endpoint.
- A form post that carries the guard (chosen).

## Consequences

- A frontend renders a form instead of a link.
- `GET /bff/logout` answers `405`.
- The guard no longer appears in a URL, so it stays out of history, logs and `Referer`.

## References

- [`BffLogoutHandler.cs`](../../src/NCode.Identity.Bff/Logout/BffLogoutHandler.cs),
  [`BffUserHandler.cs`](../../src/NCode.Identity.Bff/Endpoints/BffUserHandler.cs) and
  [`BffEndpointRouteBuilderExtensions.cs`](../../src/NCode.Identity.Bff/Endpoints/BffEndpointRouteBuilderExtensions.cs).
- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.8.
- [NCodeGroup/Identity#442](https://github.com/NCodeGroup/Identity/issues/442).
