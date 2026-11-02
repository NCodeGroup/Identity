# 1310. The interactive surface's cookies are host-only, and a tenant's session is named for its path

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
- **Supersedes:** decision 4 of
  [ADR-1193](1193-the-interactive-surface-is-served-under-the-tenant-route.md)

## Context

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) decision 4 gives
every cookie of a browser-facing surface the `__Host-` prefix. A cookie without the prefix can be set by a sibling
subdomain for the parent domain, which lets that subdomain fix a victim's session. The prefix makes a browser accept
the cookie only with `Secure`, `Path=/` and no `Domain`.

[ADR-1193](1193-the-interactive-surface-is-served-under-the-tenant-route.md) decision 4 separated the tenants of one
origin by scoping the session cookie to `Path=/{tenant}`. The prefix forbids that path, so the two cannot both hold.

## Decision

Every cookie of the interactive surface is host-only: `Path=/`, no `Domain`, and a `__Host-` name. Tenants that share an
origin are separated by the cookie's name instead of its path.

- `__Host-NCode.Identity.Interactive`: the interactive session. `Secure` always, `HttpOnly`, `SameSite=Lax`.
- `__Host-NCode.Identity.Antiforgery`: the antiforgery cookie token. `HttpOnly`, `SecurePolicy=SameAsRequest`.
- `__Host-XSRF-TOKEN`: the antiforgery request token that the pages echo back. Script-readable by design,
  `SameSite=Strict`.
- `__Host-opbs`: the Session Management browser state. Script-readable, `Secure`, `SameSite=None`.

- **The name separates tenants.** When a tenant is served under a path, `InteractiveSessionCookieManager` appends
  `.` and `Base64Url(SHA-256(tenant path))` to the session cookie's name. A tenant served at the root keeps the base
  name. Every read, write and delete maps the name the same way, so each tenant reads and clears only its own cookie.
- **The readable request-token cookie is script-readable on purpose.** The single-page forms echo it back as a hidden
  field, and antiforgery pairs it with its own `HttpOnly` cookie.
- **The `check_session_iframe` reads `__Host-opbs`.** It is framed by the relying party, so the cookie is readable by
  script and sent in a third-party context (`SameSite=None; Secure`). The script takes the name from the same constant
  the server writes it with.
- **Antiforgery keeps `SameAsRequest`.** With `Always` the antiforgery services refuse every request that is not
  HTTPS, and the `__Host-` prefix already makes a browser refuse the cookie without `Secure`.

## Options considered

- Keep path scoping without a prefix. Rejected: a sibling subdomain can fix the session.
- A `__Secure-` prefix with a path. Rejected: it allows a `Domain` attribute, and it keeps the cookie readable by a
  sibling that sets it first for the parent domain.
- A readable per-tenant suffix, such as the tenant's path. Rejected: a path may hold characters that a cookie name
  cannot, so the name carries a hash of it.
- `__Host-` names with a path-scoped cookie. Rejected: the prefix requires `Path=/`.

## Consequences

- On a shared origin under `DynamicByPath`, the browser sends every tenant's session cookie to every tenant, and each
  tenant reads only its own. `HttpOnly` keeps the cookies from script, and a path was never a security boundary
  ([ADR-1193](1193-the-interactive-surface-is-served-under-the-tenant-route.md)).
- A session under the old cookie names is ignored, so a user signs in again once.
- Chunked session cookies keep the tenant's name, with the chunking suffix after it.

## References

- [`InteractiveSessionCookieManager.cs`](../../src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveSessionCookieManager.cs),
  [`DefaultRegistration.cs`](../../src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs) and
  [`DefaultSessionStateService.cs`](../../src/NCode.Identity.OpenId.Authentication/Sessions/DefaultSessionStateService.cs).
- [RFC 6265bis](https://datatracker.ietf.org/doc/draft-ietf-httpbis-rfc6265bis/) §4.1.3: cookie name prefixes.
- [NCodeGroup/Identity#442](https://github.com/NCodeGroup/Identity/issues/442).
