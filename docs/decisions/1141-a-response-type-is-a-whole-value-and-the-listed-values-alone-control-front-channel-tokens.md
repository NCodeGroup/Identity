# 1141. A response type is a whole value, and the listed values alone control front-channel tokens

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

An OAuth 2.0 `response_type` value is a space-delimited list of names whose order does not matter, and the meaning of a
value with several names is defined by the specification that registers it (RFC 6749 §3.1.1). OAuth 2.0 Multiple
Response Type Encoding Practices registers `code id_token`, `code token`, `id_token token` and `code id_token token`
beside `code`, `id_token` and `token`. Each is a distinct flow with its own security properties: `code id_token` returns
an ID token from the authorization endpoint, and `code token` also returns an access token there. Every metadata field
that carries response types carries these whole values: `response_types_supported` (OpenID Connect Discovery §3,
RFC 8414 §2) and the dynamic client registration field `response_types`, whose values "MUST be the same as the value of
the `response_type` parameter" (RFC 7591 §2).

A response type is also tied to the grant types it uses. OpenID Connect Dynamic Client Registration 1.0 §2 lists the
grant types a client MUST register for each response type: `authorization_code` for `code`, `implicit` for `id_token`
and `id_token token`, and both for every hybrid value. RFC 7591 §2.1 asks the server to keep a client from registering
an inconsistent pair.

A model that stores single names and accepts any combination of them cannot express these values:

- **An operator cannot enable one flow.** Listing `code` and `id_token` to enable `code id_token` also enables
  `id_token` alone, and listing `token` enables every combination with it. Discovery would have to expand the names into
  all `2^n - 1` combinations, advertising flows nobody chose.
- **Dynamic client registration cannot honor a registration.** A client that registers `code id_token` is refused,
  because the value is not a single name; mapping it onto its names would let the client use `id_token` alone, which it
  did not register.
- **A hybrid flow is not a grant type.** Modeling the hybrid flow as a `hybrid` grant type, a name OAuth does not
  define, means a client registered the standard way (`authorization_code` and `implicit`) never passes the
  authorization endpoint's grant check, and the token endpoint accepts `grant_type=hybrid` for redeeming a code.
- **The response mode must follow the response type.** A response that returns a token or an ID token is returned in
  the fragment by default, and the query encoding MUST NOT be used for it; error responses use the same mode (OAuth 2.0
  Multiple Response Type Encoding Practices §2.1, §2.2, §3, §5). A default chosen without the response type puts tokens
  in a query string, where server logs and `Referer` headers record them.

The response type also decides whether the authorization endpoint returns an access token: a response type that
includes the `token` name returns one. The list `response_types_supported` merges as a ceiling (intersection) down the
platform, team, tenant and client cascade ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)),
so a second switch for the same capability would disagree with it:

- An operator who lists `code token` but leaves a separate boolean at `false` (or the reverse) has a flow that is
  advertised and registrable but refused at every request, which breaks the guarantee that advertisement and
  enforcement share one source.
- The refusal of a response type the tenant imposes is `unsupported_response_type` (RFC 6749 §4.1.2.1), not
  `unauthorized_client`.

RFC 9700 §2.1.2 says clients SHOULD NOT use response types that issue access tokens in the authorization response. That
is why the default list excludes them; it is not a reason for a second control.

## Decision

**A response type is a whole value everywhere (stored, advertised, registered and enforced as one value in one canonical
form) and it is always considered together with the grant types it uses. The listed response types are the only control
over the tokens the authorization endpoint returns.**

1. **Canonical form.** A response type is written with its names in ordinal order, separated by a single space
   (`code id_token`, never `id_token code`). That is the form the server stores and compares, and it is the order the
   specifications use. `OpenIdProtocol.ResponseTypes` names all seven values.
2. **`response_types_supported` lists whole values.** Its domain is the seven response types in canonical form
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)); a value written in
   another order is outside the domain, because a value compared verbatim is configured in the form it is compared in
   ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
   Discovery advertises the list as it is. The merge is `Intersect`, which is exact on canonical values. The
   secure-by-default baseline is `code` alone; an operator enables a front-channel flow only by listing that flow.
3. **The authorization endpoint accepts exactly a listed value.** The requested `response_type` is put in canonical
   form, because protocol input may order its names freely, and must equal a listed value; its grant types (point 5)
   must be in `grant_types_supported`. An unset list is unrestricted
   ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)).
   A response type the tenant does not support is rejected with `unsupported_response_type`, and one the tenant supports
   but the client's list excludes with `unauthorized_client` (RFC 6749 §4.1.2.1).
4. **Dynamic client registration registers what the client asked for.** Each `response_types` value is stored in
   canonical form, duplicates removed, and reported back in that form. Omitted values default consistently: omitted
   `grant_types` are the grant types the registered response types use (`authorization_code` when neither is
   registered), and omitted `response_types` are `code`, except for a client whose grant types use neither
   `authorization_code` nor `implicit`, which registers none. A registration whose response type uses a grant type it
   does not register is refused, and so is a grant type or response type the tenant does not support, because the
   endpoints would refuse it at every request and reporting it as registered would be false. All are
   `invalid_client_metadata` (RFC 7591 §2.1, §3.2.2). The response reports an empty list as empty rather than omitting
   it, since an omitted `response_types` reads as `code`. Redirect URIs are required exactly when the client registers a
   response type.
5. **Grant types follow OpenID Connect Registration §2; there is no hybrid grant type.** A response type uses
   `authorization_code` when it includes `code`, and `implicit` when it includes `id_token` or `token`; a token returned
   by the authorization endpoint is issued through the implicit grant, in a hybrid flow too. `hybrid` is not a grant type
   the server names, and the authorization code grant handler answers only `grant_type=authorization_code`.
6. **One rule decides whether a response type returns tokens.** A response type that includes `id_token` or `token`
   returns a token from the authorization endpoint. Such a response type defaults to the `fragment` response mode, for
   the success response and for the error response alike, on the first pass and on a resumed one; it may not use
   `query`, and it requires a `nonce` for an `openid` request. Every other response type defaults to `query`.
7. **`response_types_supported` is the only control over access tokens from the authorization endpoint.** A tenant or
   client forbids them by listing no response type that includes `token`. The baseline lists only `code`, so by default
   the authorization endpoint returns no token. No boolean setting duplicates this control.

## Options considered

- **Map a registered combination onto its names.** Rejected: it admits each name alone, so a client registered for
  `code id_token` could use `id_token`, and the registration response would misreport what the client may do.
- **Keep single names in the setting and store whole values only for registered clients.** Rejected: one setting would
  hold two shapes merged into one effective value, and an operator still could not enable a single flow.
- **Accept any order in configuration and normalize when a value is read.** Rejected: the settings framework compares
  and merges values verbatim, and a lenient reader turns a typo into an accepted value; protocol input is canonicalized
  at the protocol boundary instead.
- **Keep the `hybrid` grant type and add `authorization_code` plus `implicit` as an alternative.** Rejected: two
  spellings of one capability, one of them outside the OAuth grant type registry, with the token endpoint accepting a
  non-standard `grant_type`.
- **Enforce the reverse consistency direction too** (a registered `implicit` grant needs a response type that uses it).
  Rejected: an unused grant type grants nothing at the authorization endpoint, OpenID Connect Registration requires only
  the forward direction, and refusing it would break clients that register a broad grant list.
- **A separate boolean switch for token-bearing response types.** Rejected: every reader of the response types would
  have to combine two settings, the refusal would not say which scope imposed it, and it is the second switch that
  [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)
  rules out. As a server-wide kill switch it would add nothing, since the server's own `response_types_supported`
  already caps every tenant by intersection.
- **Whole canonical values, enforced exactly, with one list as the only token control.** Chosen: it expresses every
  registered flow, enables one flow without its parts, and keeps advertisement, registration and enforcement on one
  source.

## Consequences

- An operator enables a flow by listing its whole value, for example `code id_token` with the `implicit` grant, and
  enables nothing else; discovery advertises exactly the list.
- Discovery, dynamic client registration and the authorization endpoint agree on whether the authorization endpoint can
  return an access token, because they read one list.
- Dynamic client registration accepts any registered response type the tenant supports, so the OpenID Foundation hybrid
  and implicit certification profiles can register their clients the standard way.
- A client registered only for `code id_token` is refused `code` with `unauthorized_client`.
- A request that the tenant's list forbids is refused with `unsupported_response_type`; one that only the client's list
  forbids is refused with `unauthorized_client`.
- A response that returns a token is never redirected in a query string, whether it carries tokens or an error.
- A configured or stored `response_types_supported` value written in a non-canonical order, or a
  `grant_types_supported` value of `hybrid`, is outside its domain and fails as
  [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) describes.

## References

- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  the merge lattice, the secure-by-default baseline and the unset list that is unrestricted.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md): the response-type domain.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md):
  values compared verbatim are configured in the form they are compared in.
- RFC 6749 §3.1.1 and §4.1.2.1, RFC 7591 §2, §2.1 and §3.2.2, RFC 8414 §2, RFC 9700 §2.1.2, OpenID Connect Discovery
  1.0 §3, OpenID Connect Dynamic Client Registration 1.0 §2, OAuth 2.0 Multiple Response Type Encoding Practices.
- Code: `NCode.Identity.OpenId.OpenIdConstants.ResponseTypes`, `NCode.Identity.OpenId.Settings.OpenIdSettingRules`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.ResponseTypeValues`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers.DefaultValidateAuthorizationRequestHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.DefaultAuthorizationEndpointHandler` and
  `NCode.Identity.OpenId.Authentication.Endpoints.Registration.DefaultClientRegistrationEndpointHandler`.
