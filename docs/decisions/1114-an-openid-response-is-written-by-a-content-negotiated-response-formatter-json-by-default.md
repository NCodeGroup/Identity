# 1114. An OpenID response is written by a content-negotiated response formatter, JSON by default

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

When the authorization endpoint cannot safely redirect an error back to the client, because the `redirect_uri` is
unregistered or invalid or the `client_id` cannot be resolved,
[OpenID Connect Core §3.1.2.4](https://openid.net/specs/openid-connect-core-1_0.html#AuthError) requires the OP to
**not** redirect to the untrusted URI and **SHOULD inform the resource owner**. A human driving a browser needs a
readable HTML page for that case, while programmatic callers need the JSON body that is the OAuth and OpenID Connect
wire default.

An inline `if (response is IOpenIdError && prefersHtml)` branch in `OpenIdResult<T>` would privilege one response type
(errors) and one alternate representation (HTML), and it would put the negotiation plumbing (Accept parsing, formatter
lookup, JSON fallback) inside a `[PublicAPI]` result type of the lean `NCode.Identity.OpenId.Abstractions` package. The
codebase already has the right shape for writing a particular kind of response to the `HttpResponse`: the
`IResultExecutor<TResult>` seam, where the `IResult` lives in Abstractions and delegates to a DI-registered executor
in Core (`OpenIdRedirectResult` delegating to `DefaultOpenIdRedirectResultExecutor`), and a buffer and stream stack
(`NCode.Buffers` `SecureEncoding.UTF8`, `Sequence<char>`) that the HTML writer `DefaultAuthorizationResultExecutor`
already uses.

## Decision

**`OpenIdResult<T>` is a thin `IResult` that delegates to `IResultExecutor<IOpenIdResult>`, and the representation is
chosen by server-driven content negotiation over an open `IOpenIdResponseFormatter` strategy collection**
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)).

- **Abstractions stays lean.** `OpenIdResult<T>.ExecuteAsync` resolves `IResultExecutor<IOpenIdResult>` and delegates,
  the same pattern as `OpenIdRedirectResult`. No JSON serialization, Accept parsing or formatter lookup lives in
  Abstractions. The only contracts it holds for this are the extension seam `IOpenIdResponseFormatter` and its
  forward-compatible `OpenIdResponseFormatterContext`.
- **Core owns the plumbing.** `DefaultOpenIdResultExecutor`, registered as `IResultExecutor<IOpenIdResult>`, applies
  the status code and any `ISupportResponseHeaders`, negotiates over the registered `IOpenIdResponseFormatter`
  collection, and lets the chosen formatter write the body.
- **The default JSON representation is itself a formatter.** `DefaultJsonOpenIdResponseFormatter` handles every
  response as `application/json` and is the final candidate, so the JSON body flows through the same pipeline as every
  alternate rather than through a special-cased branch.
- **The seam is open on both axes.** A formatter declares the media types it emits and a `CanFormat(IOpenIdResponse)`
  predicate, so a new representation for any response type (an HTML error page, a JARM `form_post`, an XML body) is a
  `TryAddEnumerable` registration, not a change to `OpenIdResult<T>`
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Wildcards never select a formatter.** Only an explicit `type/subtype` in `Accept`, such as a browser listing
  `text/html`, selects a formatter. A wildcard-only `Accept` (`*/*`, curl's default) and programmatic callers get
  JSON.
- **The HTML error page reuses the interactive surface.** The `NCode.Identity.OpenId.Authentication.Interactive`
  package registers `InteractiveOpenIdErrorResponseFormatter`, which renders a non-redirectable authorization error as
  an HTML page through `InteractiveAuthenticationPageRenderer`, the markup and bundle seam of the sign-in and consent
  pages, into a pooled `Sequence<char>`, and writes it through `InteractiveHtmlResult.WriteAsync`, which encodes it with
  `SecureEncoding.UTF8` into the response body under the page's security headers, as the sign-in and consent pages are
  written. The page carries an `authorization-error` element with `data-error-code` that
  the bundle redraws
  ([ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)). Without the
  interactive package no such formatter is registered and every error is JSON.
- **Hosts register the executor.** Hosts that compose the OpenID Core library get `DefaultOpenIdResultExecutor`
  through `AddResultServices`.

## Options considered

- **An inline `if (error && prefersHtml)` in `OpenIdResult<T>` (rejected).** It couples the result type to one
  response type and one media type, and puts negotiation plumbing in the lean Abstractions package.
- **An error-specific `IOpenIdErrorHtmlRenderer` seam (rejected).** It is still error-coupled and HTML-coupled; a
  second alternate representation or a non-error HTML page would need yet another seam.
- **A new bespoke executor interface (rejected).** `IResultExecutor<IOpenIdResult>` already fits because
  `IOpenIdResult : IResult`, so the existing seam carries the delegation with no new abstraction.
- **A response-formatter strategy collection behind `IResultExecutor<IOpenIdResult>` (chosen).** It keeps Abstractions
  lean, reuses the established executor pattern and the `NCode.Buffers` HTML-writing idiom, and is extensible on both
  the response-type and media-type axes.

## Consequences

- `OpenIdResult<T>` requires `IResultExecutor<IOpenIdResult>` to be registered by Core's `AddResultServices`, the same
  coupling `OpenIdRedirectResult` has.
- A browser that is refused a redirect at the authorization endpoint sees a styled, readable error page; programmatic
  clients and `*/*` callers get JSON. The conformance harness's `oidcc-ensure-registered-redirect-uri` review matches
  the rendered error body.
- Further representations (JARM `form_post`, problem+json, localized pages) are additive formatter registrations.

## References

- [OpenID Connect Core §3.1.2.4](https://openid.net/specs/openid-connect-core-1_0.html#AuthError)
- `OpenIdResult<T>`, `IOpenIdResponseFormatter` and `OpenIdResponseFormatterContext` in
  `NCode.Identity.OpenId.Abstractions`
- `DefaultOpenIdResultExecutor`, `DefaultJsonOpenIdResponseFormatter` and `AddResultServices` in
  `NCode.Identity.OpenId.Core`
- `InteractiveOpenIdErrorResponseFormatter` and `InteractiveAuthenticationPageRenderer` in
  `NCode.Identity.OpenId.Authentication.Interactive`
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1253](1253-a-tenant-brands-its-sign-in-pages-through-settings-and-the-bundle-mounts-over-the-hosts-page.md)
