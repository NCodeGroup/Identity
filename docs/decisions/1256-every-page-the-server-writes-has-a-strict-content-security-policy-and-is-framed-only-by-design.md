# 1256. Every page the server writes has a strict Content-Security-Policy and is framed only by design

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Supersedes:** [project-template ADR-0121](project-template/0121-html-pages-have-a-strict-content-security-policy.md)

## Context

A page that any origin can frame is a clickjacking target: an attacker's page overlays the login or consent page and
steers the end-user's clicks and keystrokes (RFC 9700 §4.16). The server writes eight kinds of HTML page: the login and
consent pages, the account pages (applications, sessions and password), the authorization error page, the `form_post`
response, the front-channel logout page, the `check_session_iframe` and the API reference page. Five forces bear on how
they are secured:

- Antiforgery is the only component that sets a framing policy on the interactive pages, and ASP.NET Core antiforgery
  adds `X-Frame-Options: SAMEORIGIN` to a response only when it stores an antiforgery cookie. A browser that sends its
  cookie back is served those pages with no framing policy, so a page's protection would depend on whether the browser
  has visited before (issue [#303](https://github.com/NCodeGroup/identity/issues/303)).
- A page that writes its own headers carries what its author remembered: `frame-ancestors` does not fall back to
  `default-src`, so a policy that omits it can be framed.
- The pages need different policies. The `check_session_iframe` is framed cross-site by relying parties by design
  (Session Management 1.0). The front-channel logout page frames the clients' logout URIs. The login, consent and
  `form_post` pages post forms whose redirects lead to a client.
- The `apps/auth` bundle is made of module scripts, and emotion, which MUI styles through, inserts `<style>` elements at
  run time. A policy can admit both without `'unsafe-inline'`, which would admit every injected script and style as
  well, only by marking them with a nonce.
- Every package that writes a page needs the same model, and CSP directive names are an open vocabulary
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). The model holds logic (validation, serialization,
  hashing, a header writer) and names no identity concept.

## Decision

**Every HTML page the server writes states its policy as an `HtmlPageSecurityPolicy`, and one writer,
`HttpResponse.SetHtmlPageSecurity`, sets its security headers. A page starts from a strict default that allows nothing
and forbids framing, and lists only the sources it needs. Only the `check_session_iframe` may be framed by any origin,
and the `form_post` response only by its client's origin.**

1. **One model and one writer live in `NCode.Identity.AspNetCore`.** The foundation package
   `NCode.Identity.AspNetCore` (namespace `NCode.Identity.AspNetCore`) holds the model and the writer, and every
   package that writes a page references it ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)):
   - `ContentSecurityPolicyDirectives` names the known directives.
   - `ContentSecurityPolicySources` provides `None`, `Self`, `StrictDynamic`, `Nonce()`, `Sha256()`, `Origin()` and
     `CreateNonce()`. `Sha256()` hashes the UTF-8 bytes of a script with `SecureEncoding.UTF8`, so the hash matches the
     script the browser runs ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).
   - `ContentSecurityPolicyDirective` is a validated name plus its sources. A name is `[a-z0-9-]+`, and a source is one
     or more characters from `!` to `~` except `;` and `,` (the CSP Level 3 grammar), so no value can start another
     directive; the constructor throws `ArgumentException` for a name or a source outside the grammar.
   - `HtmlPageSecurityPolicy` holds `Directives`, `AllowsAnyFormAction`, `AllowsFraming`, `FrameAncestors` and
     `ToContentSecurityPolicy()`. A policy is a list of directives, not a property per directive, because directive
     names are an open vocabulary.
   - `HttpResponse.SetHtmlPageSecurity(HtmlPageSecurityPolicy)` is the only code that writes a page's security headers.
2. **The default is strict.** `ToContentSecurityPolicy()` writes `default-src 'none'`, then the page's directives in
   order (a directive with no sources is written `'none'`), then `base-uri 'none'` and `form-action 'none'` unless the
   page lists them (`form-action` is also omitted when `AllowsAnyFormAction`), then `frame-ancestors` with the page's
   `FrameAncestors` sources, or `'none'` when it lists none, unless `AllowsFraming`, joined with `"; "`. Listing
   `default-src` or `frame-ancestors`, or a name twice, throws `InvalidOperationException`, and so does setting both
   `AllowsFraming` and `FrameAncestors`.
3. **The headers are assigned, not appended.** `SetHtmlPageSecurity` sets `Content-Security-Policy`,
   `X-Frame-Options: DENY` (removed when `AllowsFraming`, or when `FrameAncestors` names a source, which
   `X-Frame-Options` cannot express), `X-Content-Type-Options: nosniff` and `Referrer-Policy: same-origin`. It writes
   `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache` through `SetNoStore()`, so one writer owns the
   uncacheable headers ([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)) and a
   page and a secret-revealing response cannot drift apart. The `SAMEORIGIN` that antiforgery adds is replaced.
   `AntiforgeryOptions.SuppressXFrameOptionsHeader` is left alone: it is application-wide and protects the host's own
   pages. `same-origin` sends no referrer cross-origin and lets a same-origin POST send its `Origin`.
4. **Each page lists what it needs.**

   | Page                   | Directives after `default-src 'none'`                                | Forms | Framed |
   | ---------------------- | -------------------------------------------------------------------- | ----- | ------ |
   | Interactive pages      | nonce `script-src`/`style-src`, `img-src`, `connect-src`, `base-uri` | any   | no     |
   | API reference page     | nonce `script-src`/`style-src`, `img-src`, `connect-src`             | none  | no     |
   | `form_post` response   | `script-src` by the hash of its auto-submit script                   | any   | client |
   | Front-channel logout   | `frame-src`, and `script-src` by hash when it redirects              | none  | no     |
   | `check_session_iframe` | `script-src` by the hash of its script                               | none  | yes    |

   The interactive pages are the login, consent, account and error pages. Their policy is `script-src 'nonce-N'
   'strict-dynamic'`, `style-src 'nonce-N'`, `img-src 'self'` plus the origins of the tenant's logo and favicon,
   `connect-src 'self'` and `base-uri 'self'`, where N is 16 random bytes, Base64Url-encoded, created for each response.
   The API reference page (`/scalar/v1`, [ADR-1481](1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md))
   lists `script-src 'nonce-N' 'strict-dynamic'`, `style-src 'nonce-N'`, `img-src 'self' data:` and
   `connect-src 'self'`; Scalar writes the nonce on its scripts, and its injected stylesheet reads it from a
   `<meta property="csp-nonce">` element in the page's head. The `check_session_iframe` sets `AllowsFraming`, so it carries no `X-Frame-Options` and no `frame-ancestors`; relying
   parties frame only it ([ADR-1088](1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md)).
   The `form_post` response lists as `FrameAncestors` the origin of an `http` or `https` redirect URI whose host is a DNS
   name or an IPv4 address, and carries no `X-Frame-Options`, so a relying party's `prompt=none` re-authentication in an
   iframe (Session Management 1.0) receives it. Any other redirect URI gets `frame-ancestors 'none'`. The front-channel
   logout page lists as `frame-src` the distinct origins of the logout URIs it frames and, only when it redirects, the
   hash of its redirect script; its iframes and its redirect anchor use the `hidden` attribute, never `style`. JSON
   endpoints and `text/plain` answers (the `503` for a missing bundle, the end-session confirmation) are not pages and
   get no policy. A `text/html` write that does not call `SetHtmlPageSecurity` is a defect.
5. **`form-action` is omitted where a submission can reach a client.** Only the interactive pages and the `form_post`
   response set `AllowsAnyFormAction`. Chrome and Safari check `form-action` against every redirect that follows a
   submission (MDN; w3c/webappsec-csp#8). A login, consent or logout post redirects through the continue endpoint to
   the client's `redirect_uri` and then wherever the client redirects, and the `form_post` page posts to the client,
   which redirects anywhere. Those targets cannot be listed. A page with no form gets `form-action 'none'`. HTML
   encoding of every rendered value, nonce-only scripts and `base-uri` keep an injected form out.
6. **The bundle carries the page's nonce.** `@ncode/vite-config`'s `defineBackendServedSurfaceConfig` sets Vite's
   `html.cspNonce` to `CSP_NONCE_PLACEHOLDER`, which is `__NCODE_CSP_NONCE__`, so the build writes
   `nonce="__NCODE_CSP_NONCE__"` on its module script and its modulepreload links, and a
   `<meta property="csp-nonce">`. `InteractiveBundleShell.NoncePlaceholder` and
   `ManagementConsoleDefaults.NoncePlaceholder` are the same string, and the host writes the response's nonce in its
   place only inside the bundle's own segments (prelude, head and tail), never inside a rendered value. The bundle reads
   the nonce with `readCspNonce` from the meta element's `nonce` property and gives it to its emotion cache, which
   `createCache` builds for the `auth` key, the document head and that nonce under `CacheProvider`, so MUI's `<style>`
   elements carry it. The branding `<script type="application/json">` block is a data block that a browser never
   executes, so CSP does not apply to it.
7. **Some headers are not sent.** `Cross-Origin-Opener-Policy`: a value other than `unsafe-none` on the issuer's pages
   severs `window.opener` for a pop-up sign-in (`display=popup`). `Permissions-Policy`: the pages use no powerful
   feature, and a frame gets none without an `allow` attribute. `Cross-Origin-Resource-Policy`: it applies to
   subresources, not pages. `Strict-Transport-Security`: it is a transport header that belongs to the host's pipeline
   ([ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package-outside-development.md)).
8. **The interactive pages are wired through one type.** `InteractivePageSecurity.Create(branding)` returns the nonce
   and the policy of one response. Login, consent, the bundle pages and the error formatter each create one per response
   and pass its nonce to the renderer, and `InteractiveHtmlResult` writes the page through `SetHtmlPageSecurity`. The
   bundle runs only the scripts and styles that carry the page's nonce (decision 6).
9. **No CORS policy applies to a front-channel endpoint.** An endpoint the user agent navigates to and a script never
   calls carries `IFrontChannelEndpointMetadata`, which `WithFrontChannel()` adds and `Endpoint.IsFrontChannel` reads:
   the authorization, continue and end-session endpoints and the interactive surface with its bundle assets (FAPI 2.0
   Security Profile §5.3.2.1). The marker is not CORS metadata such as `DisableCorsAttribute`, which fails every request
   when no CORS middleware ran, so a host without CORS middleware serves the endpoint unchanged. A host's CORS
   middleware runs only for an endpoint without the marker, as `UseDeveloperCors()` branches it, and answers no
   preflight that selects no endpoint. `WithFrontChannel()` also makes the endpoint accept its CORS preflight, which
   routing would otherwise send to its own 405 endpoint without the marker, and refuse it with `405` and an `Allow`
   header naming the endpoint's methods.

## Options considered

- **Rely on antiforgery's header.** Rejected: it is written only when a cookie is stored, so it protects a first visit
  and not a return, and it names no policy for scripts, styles or forms.
- **One middleware that stamps every `text/html` response.** Rejected: the pages need different policies, and one
  middleware would write the same policy on every HTML response. A page that states its own policy is read next to the
  markup that the policy describes.
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
- **The model in an Abstractions package.** Rejected: an `*.Abstractions` package holds contracts and data only (issue
  [#513](https://github.com/NCodeGroup/identity/issues/513)), and these types hold logic.
- **The model in an implementation package.** Rejected: `NCode.Identity.OpenId.Authentication.Interactive` also writes
  pages, and [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) forbids an
  implementation package to reference another.
- **`Encoding.UTF8` for the script hash.** Rejected: it breaks the strict-encoding rule
  ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md))
  for a hash that must match the script the browser runs.
- **`OpenId`-prefixed names.** Rejected: a general helper that carries an OpenID prefix tells a reader it is part of the
  protocol, and the package holds no OpenID concept.
- **Chosen:** one model and one writer in `NCode.Identity.AspNetCore`, a strict default, and each page listing what it
  needs.

## Consequences

- One writer sets the headers of every page, and each page's policy is a value that a test reads. A page added later
  must call `SetHtmlPageSecurity`.
- A browser that returns with its antiforgery cookie is served the same headers as a first visit.
- A bundle built without `html.cspNonce` runs none of its scripts, because none of them carries the nonce, while the
  host's server-rendered forms keep working. `CSP_NONCE_PLACEHOLDER` and the two `NoncePlaceholder` constants are a
  contract; a test of the frontend CI fails when the placeholders differ.
- A front-channel logout URI that redirects to another origin is blocked by `frame-src`, which checks every redirect.
- A page that submits a form to a client keeps `form-action` open, so its protection against an injected form rests on
  HTML encoding, the nonce and `base-uri`.
- The BFF builds its browser security headers from one configured string, not from `HtmlPageSecurityPolicy`
  ([ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md)).
- A page that lists no `form-action` gets `'none'`, not `'self'`, which differs from project-template ADR-0121: every
  default of the policy allows nothing, and no page the server writes has a same-origin form. `Cache-Control: no-store`
  on every page meets the template's per-user rule, because every page carries per-response or per-user content.

## References

- RFC 9700 §4.16, clickjacking; Content Security Policy Level 3; Session Management 1.0; Front-Channel Logout 1.0;
  FAPI 2.0 Security Profile §5.3.2.1.
- MDN, the `form-action` directive; <https://github.com/w3c/webappsec-csp/issues/8>; Vite `html.cspNonce`.
- [project-template ADR-0121](project-template/0121-html-pages-have-a-strict-content-security-policy.md)
- Code: `src/NCode.Identity.AspNetCore/` (`HtmlPageSecurityPolicy.cs`, `HtmlPageSecurityResponseExtensions.cs`,
  `ContentSecurityPolicy*.cs`), `src/NCode.Identity.OpenId.Authentication.Interactive/` (`InteractivePageSecurity.cs`,
  `InteractiveHtmlResult.cs`, `InteractiveBundleShell.cs`), `frontend/packages/vite-config/src/preset.ts`,
  `frontend/apps/auth/src/cspNonce.ts`, `tests/NCode.Identity.AspNetCore.Tests/`,
  `src/NCode.Identity.AspNetCore/FrontChannelEndpointConventionBuilderExtensions.cs`,
  `src/NCode.Identity.Server/DevelopmentEnvironment/DeveloperCorsApplicationBuilderExtensions.cs` and
  `src/NCode.Identity.Server/IdentityServerApiReferenceEndpointRouteBuilderExtensions.cs`.
