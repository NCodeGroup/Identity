# 1402. The interactive surface renders in the locale the request prefers among the registered UI locales

- **Status:** Accepted (amends
  [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md): the page the host
  renders carries its language, which the bundle reads before it renders)
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

OpenID Connect Core 1.0 §3.1.2.1 defines `ui_locales` as the end-user's preferred languages for the user interface,
as BCP 47 language tags ordered by preference, and says an unsupported locale SHOULD NOT cause an error; RP-Initiated
Logout 1.0 §2 defines the same parameter for the end-session request. Discovery advertises the locales the provider's
interface supports as `ui_locales_supported`. The authorization endpoint parses the parameter into
`IAuthorizationRequest.UiLocales`, and the `ui_locales_supported` tenant setting is a discoverable `*_supported`
ceiling whose unset value is unrestricted ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).

The interface the preference applies to is the interactive surface: the server-rendered no-JavaScript login, consent,
logout confirmation and authorization error pages, and the `apps/auth` bundle that mounts over them and draws the
account pages ([ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md)). Every page
reaches the surface through a redirect, the login challenge's and the consent and logout confirmation challenges', and
a page the surface redirects back to, such as the login page after a refused sign-in, starts from a new request. The
surface is one capability a host may replace with an interface of its own, so the locales an interface renders are
not a fact the core can hard-code, and the setting that advertises them must name only locales that something renders
([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## Decision

**The locales the end-user interface renders are a registered vocabulary; the authorization and end-session
requests carry their `ui_locales` to the surface; every page selects its locale from the request's preferences, the
user agent's, and the tenant's settings; and the page states the locale it rendered in, which the bundle and the
forms carry on.**

1. **A UI locale is registered.** `IUiLocale` (`NCode.Identity.OpenId.Locales`) names a locale the end-user
   interface renders by its BCP 47 language tag. The package that renders an interface registers one per locale it
   renders, as a strategy collection; `IUiLocaleCatalog` (`DefaultUiLocaleCatalog`) indexes them without regard to
   case and refuses a tag that is not well formed or is registered twice. The catalog reads only the registered data,
   so the setting descriptors consult it inside options validation
   ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)).
2. **The settings name registered locales only.** `ui_locales_supported` accepts only registered language tags, so
   discovery advertises only locales the interface renders, and stays unset when the tenant restricts nothing, which
   lets a request select any registered locale. `default_ui_locale` names the registered locale the surface renders
   in when no preference selects another; empty, its default, renders in the first registered locale. Both are
   tenant settings, and only `ui_locales_supported` is discoverable.
3. **The catalog selects the locale.** `IUiLocaleCatalog.Select` takes the end-user's preferences, most preferred
   first, and the bounds: each preference selects a registered locale by lookup (RFC 4647 §3.4: the tag, then the tag
   with its last subtag removed, so `fr-CA` selects a registered `fr`), and the first selection the tenant's
   `ui_locales_supported` lists wins; otherwise the tenant's `default_ui_locale`, when `ui_locales_supported` lists
   it or is unset; otherwise the first registered locale `ui_locales_supported` lists; otherwise the first registered
   locale. The ceiling bounds the fallback as it bounds a preference, so a page renders in a locale discovery
   advertises whenever the tenant advertises a registered one, and a `default_ui_locale` outside the ceiling is never
   rendered. A preference that selects nothing is a preference the end-user does not get, never an error: the
   authorization request's validation does not check `ui_locales` against `ui_locales_supported`.
4. **The request's preferences reach the surface.** The authorization endpoint's login challenge sets the request's
   `ui_locales` as an authentication-properties parameter of that name, which the interactive cookie scheme's login
   redirect appends to the login URL as `ui_locales`; the consent challenge appends the authorization request's, and
   the end-session confirmation challenge the end-session request's, to the page they redirect to.
   `IEndSessionRequest.UiLocales` parses the end-session parameter.
5. **Every page selects its locale the same way.** `InteractiveLocaleSelector` reads the request's `ui_locales`
   query, in order, then the user agent's `Accept-Language` by descending quality (without the wildcard and a
   quality of zero), and selects through the catalog with the tenant's effective settings; the authorization error
   page a request without a tenant renders selects with no bounds. The surface registers its locales as
   `InteractiveLocale` records, each carrying the `InteractiveMessages` of its server-rendered pages;
   `InteractiveAuthenticationOptionsValidator` stops the host as it starts when the catalog holds any other kind of locale, and
   the selector refuses such a catalog too, so no page renders text in one language under the tag of another and
   the error is found at deploy time, never by an end-user. `InteractiveLocales` ships English, which it registers
   first, German, Spanish and French; a host adds a locale, or changes a page's wording with a locale built from a
   shipped one and its own messages, through `AddInteractiveLocale`, which registers the `InteractiveLocale` in place
   of any registered locale of its tag, before or after `AddInteractiveAuthentication`, which never registers a
   shipped locale over a tag the host registered. The registered order is canonical, a function of the set of
   registered locales alone and never of the order of the calls: the shipped locales in their shipped order, each
   replaced by the host's locale of its tag when the host registered one, then the locales with other tags in the
   order the host registered them. Both registrations end by ordering the registered instances that way, so the
   order is the same whichever call comes first and however the calls interleave; an order that depended on the
   calls would make the default language a property of the host's composition order, which nothing documents and
   a reordering of `Program.cs` would change without a test failing. So English is the first registered locale,
   and the surface's default language, and a host that wants another default language names it in the tenant's
   `default_ui_locale`. A UI locale is registered data: both registrations register each locale as a singleton
   instance and find the registered locale of a tag among the instances, which `TryAddEnumerable` cannot, since it
   dedupes by implementation type and every shipped locale is the one record type. A locale a host registers
   through a type or a factory is outside the replace-by-tag rule and keeps its own place, and the catalog refuses
   its tag when it is registered twice.
6. **The page states its locale, and the forms carry it.** The server writes the selected locale's tag into the
   `lang` attribute of the `html` element: as the attribute of its standalone document, and in place of the
   `__NCODE_LANG__` placeholder the bundle's `index.html` carries (`InteractiveBundleShell.LanguagePlaceholder`,
   beside the nonce placeholder of
   [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
   Every server-rendered form carries the tag as a hidden `ui_locales` field, and a page the surface redirects to
   carries it as the `ui_locales` query, when it names a registered locale: the login page after a refused sign-in,
   the account page after a declined sign-out, and the login page an anonymous request for an account page is sent
   to, together with the page it returns to. So the next page renders in the same locale without a stored
   preference. The account pages the bundle draws (the account, sessions, applications and change-password pages)
   belong to no authorization or end-session request, so `ui_locales` is not theirs to carry: their forms and links
   carry none, and a page the server redirects to after a password change or a session revocation renders from the
   user agent's `Accept-Language` and the tenant's settings, as the page before it did. A language an end-user
   chooses for their account is a stored preference of theirs, outside this decision.
7. **The bundle renders the page's language.** `mountSurface` reads the document's `lang` before it renders
   (`readPageLanguage`), and `LocaleProvider` resolves it to a catalog the bundle holds (`apps/auth/src/i18n`:
   `Messages` is the shape every catalog satisfies, `en` is the default, and `resolveLocale` applies the same lookup
   as the server). Every surface reads its text through `useMessages`, formats dates with the resolved locale, and
   the forms carry the page's language back as `ui_locales`. A language the host selected but the bundle has no
   catalog for renders in the bundle's default language while the forms still carry the host's tag.

## Options considered

- **Resolve the parked authorization request on every page to read its `ui_locales`.** Rejected: the login page is
  anonymous, and a store read keyed by a query value on every anonymous request is a cost any caller can impose;
  carrying the preference on the redirect and the forms costs nothing and reaches the end-session and account pages
  the parked request does not cover.
- **Remember the selected locale in a cookie.** Rejected: a cookie is state the tenant route must scope
  ([ADR-1310](1310-the-interactive-surfaces-cookies-are-host-only-and-a-tenants-session-is-named-for-its-path.md)) and a second source of truth beside the
  request's preference; the hidden field and the query carry the choice exactly as far as it applies.
- **Let the bundle negotiate from `navigator.languages`.** Rejected: the no-JavaScript pages would stay in one
  language, the first frame would flash another, and the document's `lang` would not match what the user reads. The
  host knows the request's preference and the tenant's bounds; the bundle follows the page.
- **Keep `ui_locales_supported` a free-form list.** Rejected: discovery would advertise locales nothing renders, and a
  request for one would silently get another. A registered vocabulary keeps the advertisement true
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Localize the server-rendered pages through `.resx` resources.** Rejected: satellite assemblies are closed to a
  host that adds a language, and the messages of four pages are a small record a host can register or adapt.
- **Add an i18n library to the bundle.** Rejected: the bundle's text is a few dozen strings with simple
  interpolation, which typed catalogs and the platform's `Intl` cover; a message-format library would be the
  bundle's largest dependency for a feature it does not need.

## Consequences

- A request's `ui_locales` is honored on every page of the interaction, in its order, and an unsupported preference
  costs nothing. The end-session confirmation page honors the end-session request's `ui_locales` too.
- A tenant advertises only locales the interface renders, chooses which of them a request may select, and chooses
  its default language among them: the advertised set and the rendered default never disagree. A stored
  `ui_locales_supported` or `default_ui_locale` that names an unregistered locale is outside its domain and fails
  closed like any other setting ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)).
- A host that renders its own interface registers its locales and reads the request's preference the same way; a
  host that keeps the surface adds a language with one `InteractiveLocale` and one bundle catalog, and rewords a
  shipped one with `AddInteractiveLocale`. A locale registered without the surface's messages stops the host as it
  starts.
- The `lang` attribute, the `ui_locales` hidden field and the `ui_locales` query are part of the contract between
  the host's pages and the bundle, as the stable ids are
  ([ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md)).
- A bundle built from another `index.html` without the placeholder keeps that document's `lang`; the bundle then
  renders in its default language.

## References

- [OpenID Connect Core 1.0 §3.1.2.1](https://openid.net/specs/openid-connect-core-1_0.html#AuthRequest): `ui_locales`.
- [RP-Initiated Logout 1.0 §2](https://openid.net/specs/openid-connect-rpinitiated-1_0.html#RPLogout): `ui_locales`
  at the end-session endpoint.
- [RFC 4647 §3.4](https://www.rfc-editor.org/rfc/rfc4647#section-3.4): lookup.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md), [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1253](1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md).
- Issue [#540](https://github.com/NCodeGroup/identity/issues/540).
- Code: `src/NCode.Identity.OpenId.Abstractions/Locales/`, `src/NCode.Identity.OpenId.Core/Locales/`,
  `src/NCode.Identity.OpenId.Authentication.Interactive.Abstractions/InteractiveLocale.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveLocales.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveLocaleSelector.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationOptionsValidator.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/DefaultRegistration.cs`,
  `src/NCode.Identity.OpenId.Authentication.Interactive/InteractiveAuthenticationPageRenderer.cs`,
  `frontend/apps/auth/src/i18n/`, `frontend/apps/auth/src/pageLanguage.ts`.
