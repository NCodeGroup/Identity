# 1253. A tenant brands its sign-in pages through settings, and the bundle mounts over the host's page

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The host renders working no-JavaScript login, consent and authorization error pages
([ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md),
[ADR-1114](1114-an-openid-response-is-written-by-a-content-negotiated-response-formatter-json-by-default.md)). When the
`apps/auth` bundle is present, the host writes the page into the bundle's `#root` element and the bundle replaces it
(progressive enhancement). Several forces shape how a tenant brands those pages and how the replacement happens:

- **Branding is per tenant.** `@ncode/ui` themes from a runtime `BrandingConfig` (display name, logo, primary and
  secondary colors, color mode) and the frontend bootstrap document can carry one
  ([ADR-1064](1064-frontend-presentation-platform.md)). That document is configured per deployment, and the surface a
  tenant's end-users see is served beneath the tenant route
  ([ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md)).
  Auth0 offers per-tenant Universal Login branding; this is the equivalent.
- **A bundle that fetches before it mounts loses input.** The server page stays in place while the bundle waits on the
  network. An end-user who types, or whose browser autofills, a username and a password in that time loses both when the
  bundle replaces the page, and loses the focus and any consent scope they cleared. The fetch also needs a time-out that
  has to hold on every network, and a reply that arrives after it themes the page a second time.
- **The route table selects a surface by path.** The authorization error page is served at the protocol endpoint, which
  is outside the surface's paths, so the bundle's login fallback would replace an error page with a login form (issue
  [#211](https://github.com/NCodeGroup/identity/issues/211)).
- **`logoUrl` is not a tab icon.** A tenant's logo is a header picture of any shape, which a browser would squash into a
  16-pixel square. The OP host and the console host serve the bundle's `index.html`, whose head holds the family's icon
  links, so a tenant's page that shows another organization's brand carries the NCode Identity icon in its tab unless
  the host leaves the family's links out
  ([ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md)).
- **The policy admits few image origins.** Every page the server writes carries a content security policy whose
  `img-src` admits `'self'` and the origin of the logo
  ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)),
  so a tab icon on another origin needs its origin there. Auth0's branding has a `favicon_url` too.

## Decision

**A tenant's branding is a set of tenant settings the interactive surface registers. The host embeds the tenant's
branding in every page it serves with the bundle, and the bundle mounts at once over that page, starting from the
branding and the state the page carries.**

1. **Branding is tenant settings, registered by the surface.** `InteractiveSettingDescriptorDataSource` registers six
   tenant settings: `branding_display_name`, `branding_logo_uri`, `branding_favicon_uri`, `branding_primary_color`,
   `branding_secondary_color` and `branding_color_mode`. They join the catalog only where the surface is registered, as
   vocabularies are open ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). Each applies to tenants,
   defaults to empty (the surface's own theme), and is replaced, not merged, by a child. The management API and console
   set them like any other tenant setting.
2. **Each value is checked against the shape the bundle draws.** The display name is at most 100 characters with no
   control characters. The logo and the favicon are absolute `https` URIs, because the end-user's browser fetches them
   from a page served over TLS. The colors are `#rgb` or `#rrggbb`, the one notation every theme engine reads the same
   way. The color mode is `light` or `dark`. A stored value therefore always renders.
3. **The host embeds the branding in the page.** Every page served with the bundle carries
   `<script type="application/json" id="branding">…</script>` right after `<base>`. The JSON is the
   `InteractiveBranding` of the tenant the page is served for, in the shape of the bootstrap document's `branding`
   member with unset tokens omitted, serialized with `JsonSerializerOptions.Web`. Its encoder escapes `<`, `>` and `&`,
   so no value, such as a display name that holds `</script>`, can close the block. The host resolved the tenant for the
   page already, so the bundle fetches no branding.
4. **The favicon is a server-side token the host links in place of the family's.** `InteractiveBranding.FaviconUrl` is
   `[JsonIgnore]`, so the `#branding` block never carries it and the bundle never reads it. When it parses as an
   absolute `https` URI, the host removes every bundle `<link>` whose `rel` tokens include `icon` or
   `apple-touch-icon` and writes one `<link rel="icon" href="<url>" />`, so a tab icon is never mixed with the family's.
   `HtmlIconLinks.Remove` and `HtmlIconLinks.Create` in `NCode.Identity.AspNetCore` do both; the host removes the links
   once, when it splits the index into its shell. A standalone page, with no bundle, carries the tenant's link after
   `<base>` and no family icon. A tenant that sets a favicon gets the one `rel="icon"` link and no apple-touch-icon or
   manifest, and the server never fetches the icon.
5. **The policy admits the origins once.** `InteractivePageSecurity.Create` adds the logo's and the favicon's origins to
   `img-src`, and adds them once when they are the same origin
   ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
6. **The console mirrors the favicon.** `ManagementConsole:Branding:FaviconUrl` is unset when `null`, and an empty
   string fails startup, as it does for `LogoUrl`. `ManagementConsolePageSecurity` admits its origin as
   `InteractivePageSecurity` does, and `ManagementConsoleBrandingDocument` has no favicon member. The console keeps its
   deployment branding from its bootstrap document.
7. **`mountSurface` mounts at once and makes no request.** `mountSurface` in `apps/auth/src/mount.tsx` runs from
   `main.tsx` when the bundle's module runs. Before `createRoot(container).render(...)` it reads:
   - the branding: `readBranding` reads the `#branding` block and `parseBrandingConfig` of `@ncode/config` keeps only
     well-formed tokens, so a missing or malformed block themes the surface with the default presentation;
   - the state of the page: `readServerPage` reads the non-empty values of the text, email and password inputs by id,
     the `scope` checkboxes the end-user cleared, the id of the input that has focus, and the authorization error
     (`#authorization-error`, its `data-error-code` attribute and `#authorization-error-description`);
   - the page's `lang`
     ([ADR-1402](1402-the-interactive-surface-renders-in-the-locale-the-request-prefers-among-the-registered-ui-locales.md)).

   The first frame is therefore themed and carries the state.
8. **The forms carry what the end-user typed and where the focus was.** `LoginForm` writes a carried value into its
   input through a stable ref callback, `writeCarriedValue`. The callback assigns the DOM `value` property, and only
   while the input is empty. It never uses React's `defaultValue`, which writes the `value` attribute, so a password
   never reaches the markup. The field that had focus gets `autoFocus`. `ConsentForm` starts the carried cleared scopes
   unchecked.
9. **The error page is redrawn, not replaced.** `resolveRoute` renders `AuthorizationErrorPage` whenever the server page
   reports an authorization error, before it matches any path. The component keeps the page's stable ids
   (`authorization-error`, `data-error-code` and `authorization-error-description`), so automation reads the same page
   with or without the bundle.
10. **What the surface draws.** The logo is drawn above the surface, the display name titles the login and account
    pages, and the consent page names the requesting application.

## Options considered

- **Publish the branding in the frontend bootstrap document.** Rejected: the bootstrap document is configured per
  deployment and served at the root for the console, while the interactive bundle never fetches it. Making it
  per-tenant would give it a second tenant-resolution path.
- **Fetch the branding with a time-out, then mount.** Rejected: the server page stays in place for as long as the
  request takes, up to the time-out, and the replacement then discards what the end-user typed. A time-out is a guess
  about the network that a slow one always exceeds. The host knows the branding already, so the request has no purpose.
- **Mount at once and theme when a fetch returns.** Rejected: the first frame is unthemed, the reply paints a second
  theme over it, and the request still asks the host for what the host already knew.
- **Hydrate the server page.** Rejected: hydration requires the server markup to be React's output, and the host's
  markup is plain HTML with its own structure.
- **Leave the server form in place and enhance it.** Rejected: the pages the bundle draws entirely, such as the account
  pages, have no server markup to keep, and a form owned by both the host's markup and React splits the ownership of its
  elements.
- **`flushSync` the first render.** Rejected: `@eslint-react/dom-no-flush-sync` forbids it, and it is not needed. A
  root's first render is not time-sliced, so it commits in the task after `render` is called, and the state is read
  before that.
- **Accept any CSS color.** Rejected: theme engines disagree on named and functional notations, and a value the theme
  cannot read would break the surface at run time instead of being rejected when it is set.
- **Put the favicon in the `#branding` block.** Rejected: the bundle never reads it, and the block's shape is the
  bootstrap document's.
- **Swap the icon hrefs in the bundle's script.** Rejected: a page without JavaScript and the first paint keep the
  family's icon.
- **Reuse `logo_uri` as the tab icon.** Rejected: it is a header picture of any shape that a browser would squash into a
  16-pixel square.
- **The chosen option.** Tenant settings, embedded in the page the host renders and read before the first render, win:
  no request precedes the mount, the first frame is themed, and no time-out has to hold.

## Consequences

- A tenant brands its login, consent and account pages, and its tab icon, by setting tenant settings, with no rebuild or
  host configuration. A white-labeled sign-in page and console page carry the tenant's icon and no NCode Identity icon.
- No request precedes the mount, and there is no time-out to tune. A tenant's branding reaches the bundle only inside
  the pages the host serves.
- A branding change shows on the next page load. The pages that issue antiforgery tokens are sent `no-cache, no-store`,
  so a browser does not serve a stale branding block from its cache.
- A browser that withholds an autofilled password from script until the end-user interacts with the page gives the
  bundle nothing to carry. The browser then fills the bundle's form itself, because the form keeps the ids, names and
  `autocomplete` values of the server's.
- The element ids and field names of the host's pages are a contract with the bundle, as the stable automation ids
  already are: a page that drops one has nothing to carry.
- A tenant or operator that sets no favicon keeps the family's icons. The page's `img-src` admits one more origin when
  the favicon's differs from the logo's, and the favicon's host is fetched by the end-user's browser, not by the server.
  A tenant has no iOS home-screen icon of its own.
- Branding is per tenant only. Per-client branding, such as the RFC 7591 `logo_uri` on the consent page, is separate,
  and the `conformance` and `playground` surfaces keep the family's icon.
- Tests cover it: `InteractiveSettingDescriptorDataSourceTests` hold the rules of the settings,
  `InteractivePageSecurityTests` the `img-src` origins, `HtmlIconLinksTests` the removal and the link, and
  `InteractiveTenantRouteTests` request the login pages of a tenant that sets a favicon and one that does not.

## References

- Issues [#211](https://github.com/NCodeGroup/identity/issues/211) and
  [#745](https://github.com/NCodeGroup/identity/issues/745).
- [ADR-1062](1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md):
  the interactive login and consent surface.
- [ADR-1064](1064-frontend-presentation-platform.md): the frontend presentation platform and its branding seam.
- [ADR-1114](1114-an-openid-response-is-written-by-a-content-negotiated-response-formatter-json-by-default.md):
  non-redirectable authorization errors are content-negotiated.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): vocabularies are open unless provably closed.
- [ADR-1193](1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md):
  the interactive surface is served under the tenant route.
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md):
  page security and `img-src`.
- [ADR-1402](1402-the-interactive-surface-renders-in-the-locale-the-request-prefers-among-the-registered-ui-locales.md):
  the page's language.
- [ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md): the family's icon.
- Code: `frontend/apps/auth/src/mount.tsx`, `frontend/apps/auth/src/serverPage.ts`,
  `frontend/apps/auth/src/branding.ts`, `frontend/apps/auth/src/routes.tsx`, `frontend/apps/auth/src/LoginForm.tsx`,
  `frontend/apps/auth/src/AuthorizationErrorPage.tsx`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs` and
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBranding.cs`.
