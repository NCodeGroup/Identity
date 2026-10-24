# 253. The interactive bundle mounts at once over the page the host rendered

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The host renders working no-JavaScript login, consent and authorization error pages
([ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md),
[ADR-0114](0114-non-redirectable-authorization-errors-are-content-negotiated.md)). When the `apps/auth` bundle is
present, the host writes the page into the bundle's `#root` element and the bundle replaces it (progressive
enhancement). Two forces decide how that replacement happens:

- A bundle that fetches before it mounts leaves the server page in place while it waits on the network. An end-user
  who types, or whose browser autofills, a username and a password in that time loses both when the bundle replaces the
  page, and loses the focus and any consent scope they cleared. The fetch also needs a time-out that has to hold on
  every network, and a reply that arrives after it themes the page a second time.
- The route table selects a surface by path. The authorization error page is served at the protocol endpoint, which is
  outside the surface's paths, so the bundle's login fallback replaces an error page with a login form (backlog `R20`).

## Decision

**The bundle mounts when its module runs, over the page the host rendered, and starts from that page: the host embeds
the tenant's branding in the page, and the bundle reads the branding and the page's state before it renders.**

1. **The host embeds the tenant's branding in every page it serves with the bundle.** The page carries
   `<script type="application/json" id="branding">…</script>` right after `<base>`. The JSON is the
   `InteractiveBranding` of the tenant the page is served for, in the shape of the bootstrap document's `branding`
   member with unset tokens omitted, serialized with `JsonSerializerOptions.Web`. Its default encoder escapes `<`, `>`
   and `&`, so no value, such as a display name that holds `</script>`, can close the block. The block is written only
   when the bundle shell is present. The host resolved the tenant for the page already, so the bundle makes no request
   for it: `GET {tenant}/account/branding` and `InteractiveAuthenticationDefaults.BrandingPath` do not exist.
2. **`mountSurface` mounts at once and makes no request.** `mountSurface` in `apps/auth/src/mount.tsx` runs from
   `main.tsx` when the bundle's module runs. It first reads:
   - the branding: `readBranding` reads the `#branding` block and `parseBrandingConfig` of `@ncode/config` keeps only
     well-formed tokens, so a missing or malformed block themes the surface with the default presentation;
   - the state of the page: `readServerPage` reads the non-empty values of the text, email and password inputs by id,
     the `scope` checkboxes the end-user cleared, the id of the input that has focus inside the page, and the
     authorization error (`#authorization-error`, its `data-error-code` attribute, and
     `#authorization-error-description`).

   Then it calls `createRoot(container).render(...)`, so the first frame is themed and carries the state.
3. **The forms carry what the end-user typed and where the focus was.** `LoginForm` writes a carried value into its
   input through a stable ref callback, `writeCarriedValue`. The callback assigns the DOM `value` property, and only
   while the input is empty. It never uses React's `defaultValue`, which writes the `value` attribute, so a password
   never reaches the markup. The field that had focus gets `autoFocus`. `ConsentForm` starts the carried cleared scopes
   unchecked.
4. **The error page is redrawn, not replaced.** `resolveRoute` renders `AuthorizationErrorPage` whenever the server
   page reports an authorization error, before it matches any path. The component keeps the page's stable ids
   (`authorization-error`, `data-error-code` and `authorization-error-description`), so automation reads the same page
   with or without the bundle.

## Options considered

- **Fetch the branding with a time-out, then mount.** Rejected: the server page stays in place for as long as the
  request takes, up to the time-out, and the replacement then discards what the end-user typed. A time-out is a guess
  about the network that a slow one always exceeds. The host knows the branding already, so the request has no
  purpose.
- **Mount at once and theme when a fetch returns.** Rejected: the first frame is unthemed, the reply paints a second
  theme over it, and the request still asks the host for what the host already knew.
- **Hydrate the server page.** Rejected: hydration requires the server markup to be React's output, and the host's
  markup is plain HTML with its own structure.
- **Leave the server form in place and enhance it.** Rejected: the pages the bundle draws entirely, such as the account
  pages, have no server markup to keep, and a form owned by both the host's markup and React splits the ownership of
  its elements.
- **`flushSync` the first render.** Rejected: `@eslint-react/dom-no-flush-sync` forbids it, and it is not needed. A
  root's first render is not time-sliced, so it commits in the task after `render` is called, and the state is read
  before that.

## Consequences

- No request precedes the mount, and the first frame is themed. There is no time-out to tune.
- A branding change shows on the next page load. The pages that issue antiforgery tokens are sent `no-cache, no-store`,
  so a browser does not serve a stale branding block from its cache.
- A browser that withholds an autofilled password from script until the end-user interacts with the page gives the
  bundle nothing to carry. The browser then fills the bundle's form itself, because the form keeps the ids, names and
  `autocomplete` values of the server's.
- The element ids and field names of the host's pages are a contract with the bundle, as the stable automation ids
  already are: a page that drops one has nothing to carry.
- `GET {tenant}/account/branding` and `InteractiveAuthenticationDefaults.BrandingPath` do not exist. A tenant's
  branding reaches the bundle only inside the pages the host serves.

## References

- [ADR-0065](0065-interactive-login-consent-op-hosted-auth-surface.md): interactive login/consent as the OP-hosted
  `apps/auth` surface.
- [ADR-0114](0114-non-redirectable-authorization-errors-are-content-negotiated.md): non-redirectable authorization
  errors are content-negotiated.
- [ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md): the interactive surface is served under
  the tenant route.
- [ADR-0194](0194-a-tenant-brands-its-interactive-surface-through-settings.md): a tenant brands its interactive surface
  through settings.
- Code: `src/frontend/apps/auth/src/mount.tsx`, `src/frontend/apps/auth/src/serverPage.ts`,
  `src/frontend/apps/auth/src/branding.ts`, `src/frontend/apps/auth/src/routes.tsx`,
  `src/frontend/apps/auth/src/LoginForm.tsx`, `src/frontend/apps/auth/src/AuthorizationErrorPage.tsx`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs` and
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveBranding.cs`.
