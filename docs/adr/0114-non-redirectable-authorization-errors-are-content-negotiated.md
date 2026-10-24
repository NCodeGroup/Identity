# 114. Non-redirectable authorization errors are content-negotiated via a response-formatter seam

- **Status:** Accepted (amended by
  [ADR-0253](0253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md):
  the bundle redraws the error page instead of replacing it)
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

When the authorization endpoint cannot safely redirect an error back to the client — an unregistered/invalid
`redirect_uri` or an unresolvable `client_id` —
[OpenID Connect Core §3.1.2.4](https://openid.net/specs/openid-connect-core-1_0.html#AuthError) requires the OP to
**not** redirect to the untrusted URI and **SHOULD inform the resource owner**. The OP previously returned a raw
`application/json` error body for this case, which a human driving a browser sees as unstyled JSON. The correct
behavior is a human-readable HTML page for an interactive (`Accept: text/html`) request, while programmatic callers
keep the JSON body.

The naive fix — a `if (response is IOpenIdError && prefersHtml) { renderHtml() }` branch inside `OpenIdResult<T>` —
bakes in two wrong couplings: it privileges **one** response type (errors) and **one** alternate representation (HTML),
and it puts the negotiation plumbing (Accept parsing, formatter lookup, JSON fallback) inside a `[PublicAPI]` result
type in the lean `NCode.Identity.OpenId.Abstractions` package. The codebase already has the right shape for "write a
particular kind of response to the `HttpResponse`": the `IResultExecutor<TResult>` seam, where the `IResult` lives in
Abstractions and delegates to a DI-registered executor in Core (`OpenIdRedirectResult` →
`DefaultOpenIdRedirectResultExecutor`), and a mature buffer/stream stack (`NCode.Buffers` `SecureEncoding.UTF8`,
`Sequence<char>`) that the existing HTML writer `DefaultAuthorizationResultExecutor` uses.

## Decision

**`OpenIdResult<T>` is a thin `IResult` that delegates to `IResultExecutor<IOpenIdResult>`; representation selection is
server-driven content negotiation over an open `IOpenIdResponseFormatter` strategy collection.** Concretely:

- **Abstractions stays lean.** `OpenIdResult<T>.ExecuteAsync` resolves `IResultExecutor<IOpenIdResult>` and delegates —
  the same pattern as `OpenIdRedirectResult`. No JSON serialization, Accept parsing, or formatter lookup lives in
  Abstractions. The only new contracts there are the extension seam `IOpenIdResponseFormatter` and its forward-
  compatible `OpenIdResponseFormatterContext` DTO.
- **Core owns the plumbing.** `DefaultOpenIdResultExecutor` (registered as `IResultExecutor<IOpenIdResult>`) applies the
  response envelope (status code + any `ISupportResponseHeaders`), then performs content negotiation over the registered
  `IOpenIdResponseFormatter` collection and lets the selected formatter write the body.
- **The default JSON representation is itself a formatter.** `DefaultJsonOpenIdResponseFormatter` (`CanFormat` → every
  response, media type `application/json`) is registered alongside the alternates, so the JSON body flows through the
  **same** pipeline rather than being a special-cased inline branch. Negotiation appends `application/json` as the final
  candidate, so a wildcard-only or non-matching `Accept` resolves to the JSON formatter.
- **The seam is open on both axes.** A formatter declares the media types it emits **and** a `CanFormat(IOpenIdResponse)`
  predicate, so a new representation (an HTML error page today; a JARM `form_post` or an XML body tomorrow) for **any**
  response type is an additive `TryAddEnumerable` registration, not a change to `OpenIdResult<T>`.
- **Wildcards never select a formatter.** Only an explicit `type/subtype` in `Accept` (a real browser listing
  `text/html`) selects a formatter; a wildcard-only `Accept` such as curl's default `*/*`, and programmatic callers,
  keep the JSON default. This is the safe-by-default negotiation rule — JSON remains the OAuth/OIDC wire default unless
  an interactive client asks for something else.
- **The HTML error page reuses the interactive surface.** The `NCode.Identity.OpenId.Authentication.Interactive` package
  registers `InteractiveOpenIdErrorResponseFormatter`, which renders via the same `InteractiveAuthenticationPageRenderer`
  markup/bundle seam as the login/consent pages (so the page is styled and SPA-enhanced) and writes the body with
  `HttpResponse.WriteAsync(html, SecureEncoding.UTF8, …)` — matching `DefaultAuthorizationResultExecutor`. The page
  carries a stable `authorization-error` element (and `data-error-code`) for automation. When the interactive package is
  absent, no formatter is registered and every error degrades gracefully to JSON.

## Options considered

- **Inline `if (error && prefersHtml)` in `OpenIdResult<T>` (rejected).** Couples the result type to one response type
  and one media type, and puts negotiation plumbing in the lean Abstractions package.
- **An error-specific `IOpenIdErrorHtmlRenderer` seam (rejected).** Still error-coupled and HTML-coupled; a second
  alternate representation or a non-error HTML page would need yet another seam.
- **A new bespoke executor interface (rejected).** Unnecessary — `IResultExecutor<IOpenIdResult>` already fits because
  `IOpenIdResult : IResult`, so the existing, understood seam carries the delegation with no new abstraction.
- **Response-formatter strategy collection behind `IResultExecutor<IOpenIdResult>` (chosen).** Keeps Abstractions lean,
  reuses the established executor pattern and the `NCode.Buffers` HTML-writing idiom, and is extensible on both the
  response-type and media-type axes.

## Consequences

- `OpenIdResult<T>` now **requires** `IResultExecutor<IOpenIdResult>` to be registered (Core's `AddResultServices`), the
  same coupling `OpenIdRedirectResult` already has. Hosts that compose the OpenID Core library get it automatically.
- A browser that is refused a redirect at the authorize endpoint sees a styled, human-readable error page; programmatic
  clients and `*/*` callers are unchanged (JSON). The conformance harness's
  `oidcc-ensure-registered-redirect-uri` review matches the rendered error body.
- Future representations (JARM `form_post`, problem+json, localized pages) are additive formatter registrations.
