# 1292. The BFF's cookie authenticates only guarded endpoints, which refuse cross-site requests and reload cookieless navigations

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

[project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) keeps session
cookies host-only and has every cookie-authenticated write prove it is not cross-site. This record decides how the BFF
does that for every request its cookie authenticates.

A backend-for-frontend authenticates calls with a cookie the browser attaches on its own
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so every
endpoint that accepts the cookie is a cross-site request forgery target. The design (§5.6, §5.9 and D3) chose a static
custom header with deny-by-default CORS, Fetch Metadata and `SameSite=Strict`, after RFC 10017 §6.1.3.3.2, over a
double-submit token.

Three kinds of caller reach the guarded endpoints:

- a script calls an API with `fetch`, and can set a custom header;
- a server-rendered page, a Razor Page, an MVC view or a Blazor component, posts forms, which a browser submits without
  a custom header. ASP.NET Core's antiforgery token is what those frameworks already emit. An MVC controller or a
  minimal API validates it only when the endpoint opts in, so the guarantee would depend on every page's own opt-in;
- a browser navigates to a page. A frontend's session cookie is `SameSite=Strict` by default, so the browser withholds
  it on a top-level navigation that a cross-site page or a redirect chain starts: the return from the identity
  provider, whose `302` from `/bff/callback` to the return URL arrives without the cookie the callback has just set, and
  a link that another site offers. A page challenged on such a navigation redirects to login, the provider signs the
  user in again without a prompt, and the return arrives without the cookie once more, so the browser loops. A signed-in
  user who follows a link from another site is signed in again, and the session the browser withheld stays behind until
  it expires.

Fetch Metadata cannot tell these navigations apart from the others: Chromium labels the return from the provider
`Sec-Fetch-Site: cross-site` and Firefox labels it `none`, so a rule that keys on `Sec-Fetch-Site` fails in one of
them.

## Decision

**The session cookie authenticates only requests to endpoints the BFF guards. An API or `/bff/user` request passes only
with the static anti-forgery header exactly once and Fetch Metadata that shows no cross-site request. A page request
with a method that is not safe passes only with the same Fetch Metadata rule and a valid antiforgery token. A page
challenged on a navigation that lacks its Strict session cookie answers once with a same-origin reload page. A
misordered pipeline fails every such request instead of serving it unguarded.**

1. **The guarded endpoints** are the APIs, which carry `BffApiAttribute` metadata that `[BffApi]` or `.AsBffApi()` adds;
   the BFF's own `/bff/user`; the pages, which carry `BffPageAttribute` metadata that `[BffPage]` or `.AsBffPage()`
   adds; and the hubs, which carry the metadata that `[BffHub]` or `.AsBffHub()` adds
   ([ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-opt-in-packages.md)
   holds the hub-specific rules). The attributes derive from `AuthorizeAttribute` with the `Bff` scheme, so a guarded
   endpoint is also an authorized one. An endpoint marked with more than one kind takes the first in the order API, hub,
   page. The cookie authenticates no other endpoint, because the authentication handler returns no result there, and
   `IBffTokenAccessor` hands out tokens on no other endpoint, because it throws. The navigation endpoints (`/bff/login`,
   `/bff/callback`, `/bff/logout`) take no header, because a browser reaches them by top-level navigation;
   `/bff/backchannel-logout` carries no cookie at all.
2. **The header rule for APIs.** Every request to an API or `/bff/user`, whatever its method, carries
   `Bff:Antiforgery:HeaderName` (`X-CSRF`) exactly once, with the value `Bff:Antiforgery:HeaderValue` (`1`), compared
   ordinally. A custom header makes a cross-origin `fetch` send a CORS preflight, which the application's
   deny-by-default CORS refuses, and a form or a navigation cannot set it. Startup validation refuses a header name that
   skips the preflight (`Accept`, `Accept-Language`, `Content-Language`, `Content-Type`, `Range`) or that carries
   credentials (`Cookie`, `Authorization`, `DPoP`).
3. **The Fetch Metadata rule.** `Sec-Fetch-Site` absent (an older browser or a non-browser client), `same-origin` or
   `none` passes. `same-site` passes only when `Origin` equals an entry of the selected frontend's `TrustedOrigins`,
   compared ordinally, because a sibling subdomain is same-site and the BFF cannot read the application's CORS decision
   ([ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md) holds the per-frontend
   settings). Every other value, or a repeated header, is refused. Startup validation requires each trusted origin to be
   a bare origin, `https` unless `LocalDevelopment:Enabled` is set.
4. **Pages accept every method.** `GET`, `HEAD`, `OPTIONS` and `TRACE` pass, because a cross-site page cannot make them
   change anything. Any other method needs two checks: the Fetch Metadata rule of decision 3, then
   `IAntiforgery.IsRequestValidAsync` under the session's principal. `UseBff()` makes both, so the guarantee does not
   rest on the endpoint's own opt-in.
5. **The antiforgery cookie is `__Host-Http-bff-antiforgery`.** `AddBffLibrary` registers antiforgery with that cookie:
   `Secure` always, `Path=/`, `SameSite=Strict` and `HttpOnly`. Startup refuses an antiforgery cookie that is not
   `__Host-`, is not `Secure`, has another path or has a domain. The host's Data Protection key ring protects the
   tokens, as it does for any server-rendered form, so a host that scales out shares one key ring. The BFF's own state
   still needs no key ring (design D2).
6. **Problems.** Every refusal is `application/problem+json` with `type` from `BffProblemTypes`
   (`urn:ncode:identity:bff:<code>`), `title` and `status`, and `Cache-Control: no-store`: a missing or wrong header is
   `403` `antiforgery_header_missing`, a cross-site request `403` `cross_site_request`, and an invalid token
   `403` `antiforgery_token_invalid`. Each is audited as `bff.antiforgery.rejected`. A challenge on an API is `401`
   `session_required`, and a forbid there is `403` `forbidden`. A challenge on a page redirects to
   `/bff/login?returnUrl=<path and query>`, with the return URL replaced by `/` unless it is allow-listed. A challenge on
   an unguarded endpoint throws `InvalidOperationException` that names the markers, because a redirect would loop: the
   endpoint never sees the session, so the user would come back from login to the same challenge. `SignOutAsync` on the
   `Bff` scheme requires an API endpoint, whose header and Fetch Metadata checks keep another site from ending the
   session. The `Bff` scheme becomes the default scheme only when none is set.
7. **The reload page.** A page challenge answers with a same-origin reload page, instead of the login redirect, when all
   of these hold:
   - the method is `GET` or `HEAD`;
   - `Sec-Fetch-Mode` is absent or `navigate`;
   - the frontend's session cookie is `SameSite=Strict`;
   - the request carries no session cookie for the frontend;
   - the request carries no reload marker;
   - the request's own URL (path base, path and query string) is a local URL: it begins with exactly one `/` and holds
     no backslash, whitespace or control character, so the reload page cannot be used to redirect to another origin.

   The condition covers the provider's return and every cross-site link. Every other page challenge keeps the login
   redirect. A request that carries no Fetch Metadata header is treated as a navigation, and the marker bounds the
   reload.
8. **The reload page's response.** It is `200` with `Content-Type: text/html; charset=utf-8` and the shared page-security
   headers that every page the server writes carries
   ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)),
   under a policy that lists no resource directive:
   `Content-Security-Policy: default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'`,
   `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`, and
   `Cache-Control: no-store, no-cache, max-age=0` with `Pragma: no-cache`. Its body is a meta refresh and a link that
   both point at the request's own path base, path and query string, HTML-encoded; a `HEAD` request gets the headers and
   no body. The policy forbids every resource and every script, so the page does nothing but navigate. The browser
   follows it with a same-site request that carries the cookie.
9. **The reload marker.** The reload sets the cookie `__Host-Http-bff-reload.<frontend>` with the value `1`: `Secure`,
   `HttpOnly`, `Path=/`, no `Domain`, `SameSite=Lax` so that every top-level navigation carries it, and a `Max-Age` of
   60 seconds. A challenge that finds the marker does not reload again. The login redirect deletes the marker, so the
   return from the provider can reload once more. The marker is per frontend, so one frontend's reload does not affect
   another's. The callback's `302`, the session cookie's attributes, the `401` that an API or a hub answers, and a
   frontend whose session cookie is `Lax` keep the plain behavior.
10. **Store and token failures.** `UseBff()` answers a `BffStoreUnavailableException` from anywhere later in the pipeline
    with `503` `store_unavailable`, never `401`, so an outage never logs a user out or sends a frontend into a login
    loop. It answers a `BffTokenUnavailableException` with its status, as `session_required` for `401` and
    `token_unavailable` otherwise.
11. **The pipeline fails closed per request.** The host cannot inspect the built pipeline at startup, so the checks run
    on every request instead:
    - `UseBff()` throws `InvalidOperationException` when an `IAuthenticationFeature` is already set, that is when it
      runs after `UseAuthentication()`.
    - The session resolver throws when `UseBff()` did not run, or when the endpoint it guarded is not the one being
      served (routing ran after it).
    - So a missing or misordered `UseBff()` answers `500` and never serves a guarded endpoint without the checks; an
      end-to-end test proves it.

## Options considered

- **A per-session double-submit token (design D3 (b)).** Rejected: it needs a bootstrap read before the first call and
  per-session state, and RFC 10017 does not recommend it over the custom header.
- **Evaluating the application's CORS policy for `same-site` requests.** Rejected: the BFF cannot see the policy that
  `UseCors()` applied, and a second copy of it would drift; `TrustedOrigins` is an explicit, separate allow-list.
- **A startup check of the pipeline order.** Rejected: an ASP.NET Core host cannot see the built pipeline, so a check
  would be a guess; the per-request checks are exact and cannot be bypassed.
- **Pages that accept only safe methods.** Rejected: a page cannot receive a form, so a server-rendered host cannot post
  one to its own page.
- **The logout guard as the form token.** Rejected: it is a second token beside the framework's on every form, and a
  host's own forms already carry the framework's.
- **The framework's own antiforgery validation only.** Rejected: MVC controllers and minimal APIs validate only when the
  endpoint opts in, so one page that omits the opt-in accepts a cross-site post.
- **The callback ends with a same-site page instead of the `302`.** Rejected: it covers only the return from the
  provider. A cross-site link still signs the user in again and orphans the session that the browser withheld.
- **The session cookie defaults to `Lax`.** Rejected: it weakens the cross-site request posture this record builds on.
- **Every host marks its landing pages `[AllowAnonymous]`.** Rejected: it puts a security-critical rule on every
  consumer, and a page that a host forgets to mark loops.
- **The reload page on the page challenge.** Chosen: it covers the return from the provider and every cross-site link,
  starts no new session, and leaves the callback's `302` and the cookie's attributes as they are.

## Consequences

- A frontend sends `X-CSRF: 1` on every API call; a plain `<form>` post or a cross-origin `fetch` to an API fails.
- A page receives a form post, and a form that carries the framework's token (a tag helper, `<AntiforgeryToken>` or
  `IAntiforgery.GetAndStoreTokens`) needs no further code. A cross-site post to a page is refused before the endpoint
  runs, whatever the endpoint opts into.
- A host that sets its own antiforgery cookie name, path or domain, or drops `Secure`, fails at startup. A token that
  the host's Data Protection key ring cannot unprotect is refused as `antiforgery_token_invalid`.
- A deployment with a frontend on a sibling subdomain lists that origin in the frontend's `TrustedOrigins` and allows it
  in CORS.
- A signed-out user who navigates to a page from another site, or returns from the provider, costs one extra same-origin
  round trip before the login redirect or the page. A signed-in user who follows a link from another site reaches the
  page with the session, and the BFF starts no new session.
- A browser that sends no Fetch Metadata gets the reload page the same way, and one that sends `Sec-Fetch-Mode` other
  than `navigate` gets the login redirect. A cross-site `POST` to a page still passes the checks of decision 4 and never
  reloads, because the reload answers only `GET` and `HEAD`.
- A pipeline error surfaces on the first request as a `500`, in Development through the developer exception page.

## References

- [Backend-for-frontend design](../backend-for-frontend.md) §5.3, §5.6, §5.7, §5.9, §5.14 and D3
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) — the session
  record and its cookie
- [ADR-1293](1293-the-bff-ends-sessions-by-a-guarded-logout-form-post-and-by-back-channel-logout-through-indexes.md) — the logout
  form, a page form post
- [ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md) — the frontends
- [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-opt-in-packages.md)
  — pages and hubs
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md) —
  page security headers
- [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
- RFC 10017 §6.1.3 (CSRF in browser-based applications), RFC 9457 (problem details)
- W3C Fetch Metadata Request Headers (`Sec-Fetch-Site`)
- Issues #123, #394, #524 and #603
