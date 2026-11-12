# 1256. Every page the server writes has a strict Content-Security-Policy and is framed only by design

- **Status:** Accepted (amended by
  [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md): the server's pipeline sends
  Strict-Transport-Security; decisions 1 and 3 amended by
  [ADR-1388](1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md):
  the model and its writer live in `NCode.Identity.AspNetCore`, namespace `NCode.Identity.AspNetCore`, and `SetHtmlPageSecurity` writes
  `Cache-Control` and `Pragma` through `SetNoStore()`)
- **Date:** 2026-10-24
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0121](project-template/0121-html-pages-have-a-strict-content-security-policy.md)

## Context

A page that any origin can frame is a clickjacking target: an attacker's page overlays the login or consent page and
steers the end-user's clicks and keystrokes (RFC 9700 §4.16). The server writes seven kinds of HTML page: the login and
consent pages, the account pages (applications, sessions and password), the authorization error page, the `form_post`
response, the front-channel logout page and the `check_session_iframe`. Five forces bear on how they are secured:

- Antiforgery is the only component that sets a framing policy on the interactive pages. ASP.NET Core antiforgery adds
  `X-Frame-Options: SAMEORIGIN` to a response only when it stores an antiforgery cookie. A browser that sends its
  cookie back is served the login, consent and account pages and the error page with no framing policy, so a page's
  protection depends on whether the browser has visited before (issue [#303](https://github.com/NCodeGroup/identity/issues/303)).
- A page that writes its own headers carries what its author remembered. The `form_post` response's policy lists
  `default-src` and `script-src` but no `frame-ancestors`, and `frame-ancestors` does not fall back to `default-src`,
  so the page can be framed.
- The pages need different policies. The `check_session_iframe` is framed cross-site by relying parties by design
  (Session Management 1.0). The front-channel logout page frames the clients' logout URIs. The login, consent and
  `form_post` pages post forms whose redirects lead to a client.
- The `apps/auth` bundle is made of module scripts, and emotion, which MUI styles through, inserts `<style>` elements
  at run time. A policy can admit both without `'unsafe-inline'`, which would admit every injected script and style as
  well, only by marking them with a nonce.
- The packages that write pages share only Abstractions
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)), and CSP directive names are an open
  vocabulary ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## Decision

**Every HTML page the server writes states its policy as an `HtmlPageSecurityPolicy`, and one writer,
`HttpResponse.SetHtmlPageSecurity`, sets its security headers. A page starts from a strict default that allows nothing
and forbids framing, and lists only the sources it needs. Only the `check_session_iframe` may be framed by any origin,
and the `form_post` response only by its client's origin.**

1. **One model and one writer live in Abstractions.** In `NCode.Identity.OpenId.Authentication.Endpoints`:
   `ContentSecurityPolicyDirectives` names the known directives; `ContentSecurityPolicySources` provides `None`, `Self`,
   `StrictDynamic`, `Nonce()`, `Sha256()`, `Origin()` and `CreateNonce()`; `ContentSecurityPolicyDirective` is a
   validated name plus its sources; `HtmlPageSecurityPolicy` holds `Directives`, `AllowsAnyFormAction`, `AllowsFraming`,
   `FrameAncestors` and `ToContentSecurityPolicy()`; and `HttpResponse.SetHtmlPageSecurity(HtmlPageSecurityPolicy)` is
   the only code that writes a page's security headers. A policy is a list of directives, not a property per directive,
   because directive names are an open vocabulary.
2. **The default is strict.** `ToContentSecurityPolicy()` writes `default-src 'none'`, then the page's directives in
   order (a directive with no sources is written `'none'`), then `base-uri 'none'` and `form-action 'none'` unless the
   page lists them (`form-action` is also omitted when `AllowsAnyFormAction`), then `frame-ancestors` with the page's
   `FrameAncestors` sources, or `'none'` when it lists none, unless `AllowsFraming`, joined with `"; "`. Listing
   `default-src` or `frame-ancestors`, or a name twice, throws `InvalidOperationException`, and so does setting both
   `AllowsFraming` and `FrameAncestors`. A directive name is `[a-z0-9-]+`, and a source is one or more characters from
   `!` to `~` except `;` and `,` (the CSP Level 3 grammar), so no value can start another directive: the constructor of
   `ContentSecurityPolicyDirective` throws `ArgumentException` for a name or a source outside the grammar.
3. **The headers are assigned, not appended.** `SetHtmlPageSecurity` sets `Content-Security-Policy`,
   `X-Frame-Options: DENY` (removed when `AllowsFraming`, or when `FrameAncestors` names a source, which
   `X-Frame-Options` cannot express), `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`,
   `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache`, so the
   `SAMEORIGIN` that antiforgery adds is replaced. `AntiforgeryOptions.SuppressXFrameOptionsHeader` is left alone: it is
   application-wide and protects the host's own pages. Both `same-origin` and `no-referrer` send no referrer
   cross-origin, so clients and logo hosts never see the issuer's URLs; only `same-origin` lets a same-origin form or
   fetch POST send its `Origin`, where `no-referrer` makes it send `Origin: null`.
4. **Each page lists what it needs.**

   | Page                   | Directives after `default-src 'none'`                                | Forms | Framed |
   | ---------------------- | -------------------------------------------------------------------- | ----- | ------ |
   | Interactive pages      | nonce `script-src`/`style-src`, `img-src`, `connect-src`, `base-uri` | any   | no     |
   | `form_post` response   | `script-src` by the hash of its auto-submit script                   | any   | client |
   | Front-channel logout   | `frame-src`, and `script-src` by hash when it redirects              | none  | no     |
   | `check_session_iframe` | `script-src` by the hash of its script                               | none  | yes    |

   The interactive pages are the login, consent, account and error pages. Their policy is `script-src 'nonce-N'
   'strict-dynamic'`, `style-src 'nonce-N'`, `img-src 'self'` plus the origin of the tenant's logo, `connect-src 'self'`
   and `base-uri 'self'`, where N is 16 random bytes, Base64Url-encoded, created for each response. The interactive
   pages and the `form_post` response set `AllowsAnyFormAction` (decision 5). The `check_session_iframe` sets
   `AllowsFraming`, so it carries no `X-Frame-Options` and no `frame-ancestors`. The `form_post` response lists as
   `FrameAncestors` the origin of an `http` or `https` redirect URI whose host is a DNS name or an IPv4 address, and
   carries no `X-Frame-Options`. A relying party's `prompt=none` re-authentication in an iframe (Session Management
   1.0) therefore receives it. Any other redirect URI gets `frame-ancestors 'none'`. No other page may be framed. The
   front-channel logout page lists as `frame-src` the distinct origins of the logout URIs it frames and, only when it
   redirects, the hash of its redirect script; its iframes and its redirect anchor use the `hidden` attribute, never
   `style`. JSON endpoints, the `text/plain` `503` answer for a missing bundle and the `text/plain` end-session
   confirmation are not pages and get no policy.
5. **`form-action` is omitted where a submission can reach a client.** Chrome and Safari check `form-action` against
   every redirect that follows a submission (MDN; w3c/webappsec-csp#8). A login, consent or logout post redirects
   through the continue endpoint to the client's `redirect_uri` and then wherever the client redirects, and the
   `form_post` page posts to the client, which redirects anywhere. Those targets cannot be listed. A page with no form
   gets `form-action 'none'`. HTML encoding of every rendered value, nonce-only scripts and `base-uri` keep an injected
   form out.
6. **The bundle carries the page's nonce.** `@ncode/vite-config`'s `defineBackendServedSurfaceConfig` sets Vite's
   `html.cspNonce` to `CSP_NONCE_PLACEHOLDER`, which is `__NCODE_CSP_NONCE__`, so the build writes
   `nonce="__NCODE_CSP_NONCE__"` on its module script and its modulepreload links, and a
   `<meta property="csp-nonce">`. `InteractiveBundleShell.NoncePlaceholder` is the same string, and
   `InteractiveAuthenticationPageRenderer` writes the response's nonce in its place only inside the bundle's own
   segments (prelude, head and tail), never inside a rendered value. The bundle reads the nonce with `readCspNonce`
   from the meta element's `nonce` property and gives it to its emotion cache, which `createCache` builds for the
   `auth` key, the document head and that nonce under `CacheProvider`, so MUI's `<style>` elements carry it. The
   branding `<script type="application/json">` block is a data block that a browser never executes, so CSP does not
   apply to it.
7. **Some headers are not sent.** `Cross-Origin-Opener-Policy`: a value other than `unsafe-none` on the issuer's pages
   severs `window.opener` for a pop-up sign-in (`display=popup`). `Permissions-Policy`: the pages use no powerful
   feature, and a frame gets none without an `allow` attribute. `Cross-Origin-Resource-Policy`: it applies to
   subresources, not pages. `Strict-Transport-Security`: it is a transport header that belongs to the host's pipeline
   (issue [#304](https://github.com/NCodeGroup/identity/issues/304)).
8. **The interactive pages are wired through one type.** `InteractivePageSecurity.Create(branding)` returns the nonce
   and the policy of one response. Login, consent, the bundle pages and the error formatter each create one per response
   and pass its nonce to the renderer, and `InteractiveHtmlResult` writes the page through `SetHtmlPageSecurity`.
9. **Two ADRs are amended.** [ADR-1088](1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md): the
   `check_session_iframe` is the one page relying parties may frame, and the front-channel logout page frames only the
   clients' logout origins. [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md):
   the bundle runs only the scripts and styles that carry the page's nonce.

## Options considered

- **Rely on antiforgery's header.** Rejected: it is written only when a cookie is stored, so it protects a first visit
  and not a return, and it names no policy for scripts, styles or forms.
- **One middleware that stamps every `text/html` response.** Rejected: the pages need different policies, and a
  middleware also clamps the pages that the host serves and the server does not own, such as an API reference page. A
  page that states its own policy is read next to the markup that the policy describes.
- **`script-src 'self'` with `style-src 'unsafe-inline'`.** Rejected: `'unsafe-inline'` admits every injected style,
  and a policy that allows scripts by origin admits any script that the origin serves. A nonce on the bundle's scripts
  and on emotion's styles admits exactly what the response carries.
- **A `form-action` that lists the redirect targets.** Rejected (decision 5): the targets are the clients' redirect
  URIs and wherever those redirect, which the server does not know.
- **`Cross-Origin-Opener-Policy`.** Rejected (decision 7): it breaks the opener relationship of a pop-up sign-in.
- **`Referrer-Policy: no-referrer`.** Rejected (decision 3): both policies send no referrer cross-origin, and
  `no-referrer` makes a same-origin form or fetch POST send `Origin: null`.
- **The `form_post` response refuses every frame.** Rejected: a relying party that re-authenticates with `prompt=none`
  and `response_mode=form_post` in an iframe receives the response in a frame of its own page, and the client is the
  response's recipient, so its frame enables no clickjacking.

## Consequences

- One writer sets the headers of every page, and each page's policy is a value that a test reads. A page added later
  must call `SetHtmlPageSecurity`; a `text/html` write that does not call it is a defect.
- A browser that returns with its antiforgery cookie is served the same headers as a first visit.
- A bundle built without `html.cspNonce` runs none of its scripts, because none of them carries the nonce, while the
  host's server-rendered forms keep working. `CSP_NONCE_PLACEHOLDER` and `InteractiveBundleShell.NoncePlaceholder` are a
  contract; the built-bundle test of the frontend CI fails when they differ.
- A client whose front-channel logout URI redirects to another origin is blocked by `frame-src`, which checks every
  redirect. That is a "you chose wrong" smell: the client registers the origin that serves its logout page.
- The `check_session_iframe` runs only its own script and stays embeddable, and the `form_post` response may be framed
  by its client's origin; every other page refuses framing.
- A page that submits a form to a client keeps `form-action` open, so its protection against an injected form rests on
  HTML encoding, the nonce and `base-uri`.
- The reference host sends no `Strict-Transport-Security` header (issue [#304](https://github.com/NCodeGroup/identity/issues/304)).

## Relation to project-template ADR-0121

This record keeps [project-template ADR-0121](project-template/0121-html-pages-have-a-strict-content-security-policy.md)'s
model, writer, headers, nonce and framing rules. It differs in one default, and it meets the template's cache rule.

- A page that lists no `form-action` gets `'none'`, not `'self'` (decision 2). Every default of the policy allows
  nothing, and no page the server writes has a same-origin form: each either posts none or omits `form-action`
  (decision 5).
- `Cache-Control: no-store` on every page meets the template's per-user rule. Every page carries per-response or
  per-user content: a nonce, an authorization response, or a session's logout targets. It costs nothing for the one
  static page, the `check_session_iframe`.

## References

- RFC 9700 §4.16, clickjacking: <https://www.rfc-editor.org/rfc/rfc9700.html#name-clickjacking>
- Content Security Policy Level 3: <https://www.w3.org/TR/CSP3/>
- OWASP Secure Headers Project: <https://owasp.org/www-project-secure-headers/>
- MDN, the `form-action` directive:
  <https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/form-action>
- Redirects and `form-action`: <https://github.com/w3c/webappsec-csp/issues/8>
- Vite, `html.cspNonce`: <https://vite.dev/config/shared-options#html-cspnonce>
- Front-Channel Logout 1.0: <https://openid.net/specs/openid-connect-frontchannel-1_0.html>
- Session Management 1.0: <https://openid.net/specs/openid-connect-session-1_0.html>
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md): implementation packages depend only on
  abstractions.
- [ADR-1088](1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md): opt-in front-channel logout and session
  management.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): vocabularies are open unless provably closed.
- [ADR-1193](1193-the-interactive-surface-is-served-under-the-tenant-route.md): the interactive surface is served under
  the tenant route.
- [ADR-1201](1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md): authorization
  responses are delivered by registered response-mode handlers.
- [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md): the interactive bundle
  mounts at once over the page the host rendered.
- Code: `src/NCode.Identity.AspNetCore/HtmlPageSecurityPolicy.cs`,
  `src/NCode.Identity.AspNetCore/HtmlPageSecurityResponseExtensions.cs`,
  `src/NCode.Identity.AspNetCore/ContentSecurityPolicyDirective.cs`,
  `src/NCode.Identity.AspNetCore/ContentSecurityPolicyDirectives.cs`,
  `src/NCode.Identity.AspNetCore/ContentSecurityPolicySources.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Results/AuthorizationResponseWriter.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/CheckSession/DefaultCheckSessionEndpointProvider.cs`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/DefaultEndSessionEndpointProvider.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractivePageSecurity.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveHtmlResult.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBundleShell.cs`,
  `frontend/packages/vite-config/src/preset.ts`, `frontend/apps/auth/src/cspNonce.ts`,
  `frontend/apps/auth/src/mount.tsx` and
  `tests/NCode.Identity.OpenId.Integration.Tests/Endpoints/HtmlPageSecurityTests.cs`.
