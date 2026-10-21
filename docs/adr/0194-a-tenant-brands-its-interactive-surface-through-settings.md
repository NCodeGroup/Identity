# 194. A tenant brands its interactive surface through settings

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

[ADR-0064](0064-frontend-presentation-platform.md) left branding as a seam. `@ncode/ui` themes from a runtime
`BrandingConfig` (display name, logo, primary and secondary colors, color mode), and the frontend bootstrap document
can carry one. But that document is configured per deployment, and the surfaces a tenant's end-users see — login,
consent, and the account pages served by `apps/auth` — did not read it at all. Every tenant's end-users saw the same
unbranded pages.

Branding is per tenant, like the rest of a tenant's behavior, and the surface is now served beneath the tenant route
([ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md)). Auth0 offers per-tenant Universal Login
branding; this is the equivalent.

## Decision

**A tenant's branding is a set of tenant settings that the interactive surface registers, serves beneath the tenant
route, and themes itself with before it renders.**

1. **Branding is tenant settings, registered by the surface.** The interactive package registers a setting descriptor
   data source with `branding_display_name`, `branding_logo_uri`, `branding_primary_color`, `branding_secondary_color`,
   and `branding_color_mode`. They join the catalog only where the surface is registered, as the vocabularies are open
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). Each applies to tenants, defaults to empty
   (the surface's own theme), and is replaced, not merged, by a child. The management API and console set them like any
   other tenant setting.
2. **Each value is checked against the shape the bundle draws.** The display name is at most 100 characters with no
   control characters. The logo is an absolute `https` URI, because the end-user's browser fetches it from a page
   served over TLS. The colors are hexadecimal (`#rgb` or `#rrggbb`), the one notation every theme engine reads the same
   way. The color mode is `light` or `dark`. A stored value therefore always renders.
3. **The surface serves the branding beneath the tenant route.** `GET {tenant}/account/branding` returns the tokens of
   the tenant the request is served for, in the shape of the bootstrap document's `branding` member, omitting unset
   ones. It is anonymous, because the login page shows it to every visitor, and is sent `Cache-Control: no-cache` so a
   change appears on the next visit.
4. **The bundle themes itself before it renders.** `apps/auth` fetches `branding` relative to its document base, keeps
   only well-formed tokens (`parseBrandingConfig` in `@ncode/config`, which the bootstrap parser shares), and passes
   them to `AppThemeProvider`. The page the server rendered stays in place until then. A failed fetch falls back to the
   default theme rather than blocking sign-in. The logo is drawn above the surface, and the display name titles the
   login and account pages. The consent page keeps naming the requesting application.

## Options considered

- **Publish the branding in the frontend bootstrap document.** Rejected: the bootstrap document is configured per
  deployment and served at the root for the console, while the interactive bundle never fetches it. Making it
  per-tenant would give it a second tenant-resolution path.
- **Write the branding into the page the host renders.** Rejected for now: every page renderer would have to carry it.
  A fetch beneath the tenant route reuses the surface's existing JSON pattern, and the server-rendered page covers the
  wait.
- **Accept any CSS color.** Rejected: theme engines disagree on named and functional notations, and a value the theme
  cannot read would break the surface at run time instead of being rejected when it is set.

## Consequences

- A tenant brands its login, consent, and account pages by setting tenant settings, with no rebuild or host
  configuration.
- The management console (operator-facing) keeps its deployment branding from the bootstrap document.
- Branding is per tenant only. Per-client branding, such as the RFC 7591 `logo_uri` on the consent page, is separate.

## References

- [ADR-0064](0064-frontend-presentation-platform.md) — the frontend presentation platform and its branding seam.
- [ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md) — the interactive surface is served
  under the tenant route.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed.
