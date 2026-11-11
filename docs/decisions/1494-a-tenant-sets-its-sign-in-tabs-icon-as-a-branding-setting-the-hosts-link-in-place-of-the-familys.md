# 1494. A tenant sets its sign-in tab's icon as a branding setting the hosts link in place of the family's

- **Status:** Accepted (amends [ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md) decision 6)
- **Refines:** [ADR-1194](1194-a-tenant-brands-its-interactive-surface-through-settings.md)
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

[ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md) gives every web app the
family's icon and leaves a tenant's own tab icon to a branding setting of its own.

- **`logoUrl` is not a tab icon.** A tenant's `logoUrl`, and the console's branding `LogoUrl`, is a header picture of any
  shape, which a browser would squash into a 16-pixel square if it were the tab icon.
- **The hosts write the page's head.** The OP host and the console host serve the bundle's `index.html`. Its head holds
  the family's icon links, which Vite writes as `<link rel="icon" href="./assets/favicon-HASH.ico" sizes="32x32">` and
  `<link rel="apple-touch-icon" href="./assets/apple-touch-icon-HASH.png">`. A tenant's page that shows another
  organization's brand carries the NCode Identity icon in its tab unless the host leaves the family's links out.
- **The policy admits few image origins.** Every page the server writes carries a content security policy whose `img-src`
  admits `'self'` and the origin of the logo
  ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
  A tab icon on another origin needs its origin there.
- **The bundle never reads it.** The branding block of the page has a shape that
  [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md) fixes and an integration
  test asserts.
- **Other identity providers offer it.** Auth0's branding has a `favicon_url`.

## Decision

**A tenant sets its tab icon with the branding setting `branding_favicon_uri` and the console with
`ManagementConsole:Branding:FaviconUrl`; each host links that icon in place of the family's.**

1. **The names and the rule.** The tenant setting is `branding_favicon_uri` and the console option is `FaviconUrl`. They
   sit beside `branding_logo_uri` and `LogoUrl` and copy their rule: an absolute `https` URI. For the tenant setting an
   empty value means none; the console option is unset when it is `null`, and an empty string fails startup, as it does
   for `LogoUrl`. The host writes the link only when the value parses as an absolute `https` URI, the same condition
   under which the policy admits its origin.
2. **One tab icon is the page's whole icon set.** When a favicon is configured the host removes every `<link>` of the
   bundle's `index.html` whose `rel` tokens include `icon` or `apple-touch-icon`, and writes one
   `<link rel="icon" href="<url>" />`. A tab icon is never mixed with the family's. `HtmlIconLinks` of `NCode.Identity.AspNetCore`
   holds `Remove(string html)` and `Create(string href)` for both hosts. The OP host removes the links once, when it
   splits a bundle's index into its shell, and the console host removes them once, when it parses its index.
3. **The favicon is a server-side token.** `InteractiveBranding.FaviconUrl` is `[JsonIgnore]` and
   `ManagementConsoleBrandingDocument` has no such member, because the bundle never reads the icon. The `#branding`
   block keeps the shape that [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md)
   fixes.
4. **The policy admits the origin once.** `InteractivePageSecurity.Create` and `ManagementConsolePageSecurity.Create`
   add the favicon's origin to `img-src` beside the logo's, and once when both share an origin.
5. **Standalone pages carry it too.** A page with no bundle carries the tenant's icon in its head after `<base>`, and
   carries no family icon either way.
6. **No second apple-touch-icon and no manifest.** A tenant that sets a favicon gets the one `rel="icon"` link, and a
   browser that wants an iOS icon uses it. The tab icon is not a web manifest, for the reasons of
   [ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md) decision 4, and the
   server never fetches the icon.
7. **The checks.** `HtmlIconLinksTests` holds the removal and the link. `InteractiveSettingDescriptorDataSourceTests`
   hold the rule of the setting. `InteractiveBrandingTests` hold the reading, the `FaviconUri` condition and the
   omission from the serialized branding. `InteractivePageSecurityTests` hold the `img-src` origins and their single
   admission. `InteractiveBundleShellTests` hold the head without icons.
   `InteractiveAuthenticationPageRendererTests` hold the tenant link in place of the family's, with a bundle and
   without one. `InteractiveTenantRouteTests` request the login pages of a tenant that sets a favicon and one that does
   not, and read the link and the policy. `ManagementConsoleOptionsValidatorTests` hold the option's rule,
   `ManagementConsolePageTests` and `ManagementConsolePageSecurityTests` hold the console page's link and `img-src`, and
   `ManagementConsolePageHandlerTests` serve the page with the configured icon.

## Options considered

- **Swap the hrefs in the bundle's script.** Rejected: a page without JavaScript and the first paint keep the family's
  icon.
- **Put the favicon in the `#branding` block.** Rejected: the bundle never reads it, and the block's shape is fixed by
  [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md).
- **Reuse `logo_uri`.** Rejected: it is a header picture of any shape that a browser would squash into a 16-pixel
  square.

## Consequences

- A white-labeled sign-in page and console page carry the tenant's icon and no NCode Identity icon.
- A tenant or operator that sets no favicon keeps the family's icons of
  [ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md).
- The page's `img-src` admits one more origin when the favicon's differs from the logo's, and the favicon's host is
  fetched by the end-user's browser, not by the server.
- The developer surfaces `conformance` and `playground` keep the family's icon.
- A tenant has no iOS home-screen icon of its own.

## References

- Issue [#745](https://github.com/NCodeGroup/identity/issues/745)
- [ADR-1194](1194-a-tenant-brands-its-interactive-surface-through-settings.md)
- [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md)
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
- [ADR-1492](1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md)
