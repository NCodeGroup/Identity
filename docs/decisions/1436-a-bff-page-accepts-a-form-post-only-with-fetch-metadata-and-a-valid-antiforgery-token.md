# 1436. A BFF page accepts a form post only with Fetch Metadata and a valid antiforgery token

- **Status:** Accepted (amends
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) decision 4)
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)

## Context

A server-rendered page posts forms: a Razor Page, an MVC view and a Blazor component each submit one, and the logout
form of [ADR-1311](1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md) is one of them.
[ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) decision 4 lets a page
accept only safe methods, so a page cannot receive its own form.

A browser submits a form without a custom header, so the static anti-forgery header cannot guard it. ASP.NET Core's
antiforgery token is what Razor Pages, the MVC tag helpers and Blazor's `<AntiforgeryToken>` already emit, so a page
that renders a form carries it without further work.

[Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md) requires a
browser-facing request that changes state to be safe by construction. An MVC controller or a minimal API validates the
antiforgery token only when the endpoint opts in, so the guarantee would depend on every page's own opt-in.

## Decision

**A page accepts every method. A method that is not safe passes only when Fetch Metadata shows no cross-site request and
the ASP.NET Core antiforgery token is valid under the session's principal, and `UseBff()` checks both.**

1. **Safe methods pass.** `GET`, `HEAD`, `OPTIONS` and `TRACE` reach a page as a browser's top-level navigation, which
   a cross-site page cannot make change anything.
2. **Any other method needs two checks.** The request carries Fetch Metadata that is not cross-site, by the rule of
   [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) decision 3: `same-site`
   passes only from a trusted origin. `IAntiforgery.IsRequestValidAsync` then passes under the session's principal.
   `UseBff()` makes both checks, so the guarantee does not rest on the endpoint's own opt-in. A failure is `403`
   `antiforgery_token_invalid` for the token or `403` `cross_site_request` for Fetch Metadata, and is audited as
   `bff.antiforgery.rejected`.
3. **The antiforgery cookie is `__Host-Http-bff-antiforgery`.** `AddBffLibrary` registers antiforgery with that cookie:
   `Secure` always, `Path=/`, `SameSite=Strict` and `HttpOnly`. Startup refuses an antiforgery cookie that is not
   `__Host-`, is not `Secure`, has another path or has a domain.
4. **The host's Data Protection key ring protects the tokens**, as it does for any server-rendered form. The BFF's own
   state still needs no key ring (design D2).

## Options considered

- **Pages accept only safe methods.** Rejected: a page cannot receive a form, so a server-rendered host cannot post
  one to its own page.
- **The logout guard as the form token.** Rejected: it is a second token beside the framework's on every form, and a
  host's own forms already carry the framework's.
- **The framework's own validation only.** Rejected: MVC controllers and minimal APIs validate only when the endpoint
  opts in, so one page that omits the opt-in accepts a cross-site post.

## Consequences

- A page receives a form post, and a form that carries the framework's token needs no further code.
- A cross-site post to a page is refused before the endpoint runs, whatever the endpoint opts into.
- A tag helper, `<AntiforgeryToken>` or `IAntiforgery.GetAndStoreTokens` supplies the token that a page's form posts.
- A host that sets its own antiforgery cookie name, path or domain, or drops `Secure`, fails at startup.
- A token that the host's Data Protection key ring cannot unprotect is refused as `antiforgery_token_invalid`, so a
  host that scales out shares one key ring, as for every server-rendered form.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.6
- [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1311](1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md),
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)
- [Project-template ADR-0129](project-template/0129-browser-facing-requests-are-safe-by-construction.md)
- Issue [#524](https://github.com/NCodeGroup/Identity/issues/524)
