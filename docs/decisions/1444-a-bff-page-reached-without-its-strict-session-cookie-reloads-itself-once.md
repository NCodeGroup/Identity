# 1444. A BFF page reached without its Strict session cookie reloads itself once

- **Status:** Accepted (amends
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) decision 8)
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

A frontend's session cookie is `SameSite=Strict` by default
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) decision 8).
A browser withholds a Strict cookie on a top-level navigation that a cross-site page or a redirect chain starts. Two
navigations are of that kind:

- the return from the identity provider. The chain begins at the provider, so the `302` from `/bff/callback` to the
  return URL arrives without the session cookie the callback has just set;
- a link that another site offers to a page of the BFF.

A BFF page that is challenged on such a navigation redirects to the login endpoint, the provider signs the user in
again without a prompt because its own session is alive, and the return arrives without the cookie once more, so the
browser loops. A signed-in user who follows a link from another site is signed in again as well, and the session that
the browser withheld stays behind until it expires.

Fetch Metadata cannot tell these navigations apart from the others. The browsers label the return from the provider
differently: Chromium sends `Sec-Fetch-Site: cross-site` and Firefox sends `none`. A rule that keys on `Sec-Fetch-Site`
fails in one of them.

## Decision

**A page that is challenged on a navigation that lacks its Strict session cookie answers once with a same-origin reload
page. The browser follows it with a same-site request that carries the cookie, and a short-lived marker cookie keeps the
reload from repeating.**

1. **The conditions.** The page challenge answers with the reload page when all of these hold:
   - the method is `GET` or `HEAD`;
   - `Sec-Fetch-Mode` is absent or `navigate`;
   - the frontend's session cookie is `SameSite=Strict`;
   - the request carries no session cookie for the frontend;
   - the request carries no reload marker.

   Every other page challenge keeps its behavior: a `302` to the login endpoint with the return URL.
2. **The reload page.** The response is `200` with `Content-Type: text/html; charset=utf-8`, `Cache-Control: no-store`,
   `Referrer-Policy: no-referrer`, `X-Content-Type-Options: nosniff` and
   `Content-Security-Policy: default-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'`. Its body
   is a meta refresh and a link that both point at the request's own path base, path and query string, HTML-encoded. A
   `HEAD` request gets the headers and no body. The CSP forbids every resource and every script, so the page does
   nothing but navigate.
3. **The marker.** The reload sets the cookie `__Host-Http-bff-reload.<frontend>` with the value `1`: `Secure`,
   `HttpOnly`, `Path=/`, no `Domain`, `SameSite=Lax` so that every top-level navigation carries it, and a `Max-Age` of
   60 seconds. A challenge that finds the marker does not reload again. The login redirect deletes the marker, so the
   return from the provider can reload once more.
4. **What stays as it is.** The callback's `302`, the session cookie's attributes, the `401` that an API or a hub
   answers, and a frontend whose session cookie is `Lax` keep their behavior. A request that carries no Fetch Metadata
   header is treated as a navigation, and the marker bounds the reload.

## Options considered

- **The reload page on the page challenge.** Chosen: it covers the return from the provider and every cross-site link,
  starts no new session, and leaves the callback's `302` and the cookie's attributes as they are.
- **The callback ends with a same-site page instead of the `302`.** Rejected: it covers only the return from the
  provider. A cross-site link still signs the user in again and orphans the session that the browser withheld.
- **The session cookie defaults to `Lax`.** Rejected: it weakens the cross-site request posture that
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) builds on.
- **Every host marks its landing pages `[AllowAnonymous]`.** Rejected: it puts a security-critical rule on every
  consumer, and a page that a host forgets to mark loops.

## Consequences

- A signed-out user who navigates to a page from another site, or returns from the provider, costs one extra
  same-origin round trip before the login redirect or the page.
- A signed-in user who follows a link from another site reaches the page with the session, and the BFF starts no new
  session.
- A browser that sends no Fetch Metadata gets the reload page the same way, and one that sends `Sec-Fetch-Mode` other
  than `navigate` gets the login redirect.
- A page that is reached by a cross-site `POST` still passes the checks of
  [ADR-1436](1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md) and never
  reloads, because the reload answers only `GET` and `HEAD`.
- The marker is per frontend, so one frontend's reload does not affect another's.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.3 and §5.14
- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1436](1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md)
- [Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
- Issue [#603](https://github.com/NCodeGroup/Identity/issues/603)
