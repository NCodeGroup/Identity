# 292. The BFF accepts its cookie only with a static header and Fetch Metadata

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

A backend-for-frontend authenticates API calls with a cookie the browser attaches on its own
([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so every
endpoint that accepts the cookie is a cross-site request forgery target. The design (§5.6, §5.9 and D3) chose a static
custom header with deny-by-default CORS, Fetch Metadata and `SameSite=Strict`, after RFC 10017 §6.1.3.3.2, over a
double-submit token. It also asked for a startup check that the pipeline runs the checks before authentication. Phase 2
(issue #394) builds the acceptance rules, the problem responses and the pipeline checks; this ADR records them.

## Decision

**The session cookie authenticates only requests to endpoints the BFF guards: an API or `/bff/user` only when the request
carries the static anti-forgery header exactly once and Fetch Metadata does not show a cross-site request, and a page
only for the safe methods `GET` and `HEAD`; a misordered pipeline fails every such request instead of serving it
unguarded.**

1. **The guarded endpoints** are the APIs, which carry `BffApiAttribute` metadata that `[BffApi]` or `.AsBffApi()`
   adds; the BFF's own `/bff/user`; and the pages, which carry `BffPageAttribute` metadata that `[BffPage]` or
   `.AsBffPage()` adds. `BffApiAttribute` and `BffPageAttribute` derive from `AuthorizeAttribute` with the `Bff` scheme,
   so a guarded endpoint is also an authorized one. The cookie authenticates no other endpoint, because the
   authentication handler returns no result there, and `IBffTokenAccessor` hands out tokens on no other endpoint,
   because it throws. The navigation endpoints (`/bff/login`, `/bff/callback`, `/bff/logout`) are not guarded by the
   header, because a browser reaches them by top-level navigation; `/bff/backchannel-logout` carries no cookie at all.
2. **The header rule.** Every request to a guarded endpoint, whatever its method, carries `Bff:Antiforgery:HeaderName`
   (`X-CSRF`) exactly once, with the value `Bff:Antiforgery:HeaderValue` (`1`), compared ordinally. A custom header
   makes a cross-origin `fetch` send a CORS preflight, which the application's deny-by-default CORS refuses, and a form
   or a navigation cannot set it. Startup validation refuses a header name that skips the preflight (`Accept`,
   `Accept-Language`, `Content-Language`, `Content-Type`, `Range`) or that carries credentials (`Cookie`,
   `Authorization`, `DPoP`).
3. **The Fetch Metadata rule.** `Sec-Fetch-Site` absent (an older browser or a non-browser client), `same-origin` or
   `none` passes. `same-site` passes only when `Origin` equals an entry of `Bff:TrustedOrigins`, compared ordinally,
   because a sibling subdomain is same-site and the BFF cannot read the application's CORS decision. Every other value,
   or a repeated header, is refused. Startup validation requires each trusted origin to be a bare origin, `https`
   outside Development.
4. **Pages accept only safe methods.** A page carries no anti-forgery header, because a browser reaches it by top-level
   navigation, so it accepts only `GET` and `HEAD`, which a cross-site page cannot make change anything. Any other
   method is `405` `unsafe_method` with `Allow: GET, HEAD`, and is audited as `bff.antiforgery.rejected`. An endpoint
   marked both as an API and as a page is an API.
5. **Problems.** Every refusal is `application/problem+json` with `type` from `BffProblemTypes`
   (`urn:ncode:identity:bff:<code>`), `title` and `status`, and `Cache-Control: no-store`: a missing or wrong header is
   `403` `antiforgery_header_missing`, a cross-site request `403` `cross_site_request`, and both are audited as
   `bff.antiforgery.rejected`. A challenge on a `[BffApi]` endpoint is `401` `session_required`, and a forbid there is
   `403` `forbidden`. A challenge on a page redirects to `/bff/login?returnUrl=<path and query>`, with the return URL
   replaced by `/` unless it is allow-listed. A challenge on an unguarded endpoint throws `InvalidOperationException`
   that names the markers, because a redirect would loop: the endpoint never sees the session, so the user would come
   back from login to the same challenge. `SignOutAsync` on the `Bff` scheme requires an API endpoint, whose header and
   Fetch Metadata checks keep another site from ending the session. The `Bff` scheme becomes the default scheme only
   when none is set.
6. **Store and token failures.** `UseBff()` answers a `BffStoreUnavailableException` from anywhere later in the pipeline
   with `503` `store_unavailable`, never `401`, so an outage never logs a user out or sends a frontend into a login
   loop. It answers a `BffTokenUnavailableException` with its status, as `session_required` for `401` and
   `token_unavailable` otherwise.
7. **The pipeline fails closed per request.** The host cannot inspect the built pipeline at startup, so the checks run
   on every request instead:
   - `UseBff()` throws `InvalidOperationException` when an `IAuthenticationFeature` is already set, that is when it runs
     after `UseAuthentication()`.
   - The session resolver throws when `UseBff()` did not run, or when the endpoint it guarded is not the one being
     served (routing ran after it).
   - So a missing or misordered `UseBff()` answers `500` and never serves a guarded endpoint without the checks; an
     end-to-end test proves it.
8. **The idle window slides on API and page requests**
   ([ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), never on
   `/bff/user`.

## Options considered

- **A per-session double-submit token (design D3 (b)).** Rejected: it needs a bootstrap read before the first call and
  per-session state, and RFC 10017 does not recommend it over the custom header.
- **Evaluating the application's CORS policy for `same-site` requests.** Rejected: the BFF cannot see the policy that
  `UseCors()` applied, and a second copy of it would drift; `TrustedOrigins` is an explicit, separate allow-list.
- **A startup check of the pipeline order.** Rejected: an ASP.NET Core host cannot see the built pipeline, so a check
  would be a guess; the per-request checks are exact and cannot be bypassed.
- **`SameSite` alone (D3 (c)).** Rejected: it does not stop same-site subdomains.

## Consequences

- A frontend sends `X-CSRF: 1` on every API call; a plain `<form>` post or a cross-origin `fetch` to a guarded
  endpoint fails.
- A page answers `GET` and `HEAD` only, so a form that posts to a page fails with `405`; an endpoint that must accept
  another method is an API.
- A deployment with a frontend on a sibling subdomain lists that origin in `Bff:TrustedOrigins` and allows it in CORS.
- A pipeline error surfaces on the first request as a `500`, in Development through the developer exception page.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.6, §5.7, §5.9 and D3
- [ADR-0290](0290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — the session record
- RFC 10017 §6.1.3 (CSRF in browser-based applications), RFC 9457 (problem details)
- W3C Fetch Metadata Request Headers (`Sec-Fetch-Site`)
- Issues #123 and #394
