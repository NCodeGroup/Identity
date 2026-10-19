# 141. Response types are whole values, enforced exactly and consistent with grant types

- **Status:** Accepted (amends [ADR-0140](0140-setting-values-are-validated-against-their-domain.md),
  [ADR-0083](0083-secure-by-default-discovery-metadata.md), and
  [ADR-0063](0063-dynamic-client-registration.md))
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

An OAuth 2.0 `response_type` value is a space-delimited list of names whose order does not matter, and the meaning of a
value with several names is defined by the specification that registers it (RFC 6749 §3.1.1). OAuth 2.0 Multiple
Response Type Encoding Practices registers `code id_token`, `code token`, `id_token token`, and `code id_token token`
beside `code`, `id_token`, and `token`; each is a distinct flow with its own security properties — `code id_token`
returns an ID token from the authorization endpoint, `code token` also returns an access token there. Every metadata
field that carries response types carries these **whole values**: `response_types_supported` (OpenID Connect
Discovery §3, RFC 8414 §2) and the dynamic client registration field `response_types`, whose values "MUST be the same as
the value of the `response_type` parameter" (RFC 7591 §2).

A response type is also tied to the grant types it uses. OpenID Connect Dynamic Client Registration 1.0 §2 lists the
grant types a client MUST register for each response type: `authorization_code` for `code`, `implicit` for `id_token`
and `id_token token`, and both for every hybrid value. RFC 7591 §2.1 asks the server to keep a client from registering
an inconsistent pair.

A model that stores single names and accepts any combination of them cannot express these values:

- **An operator cannot enable one flow.** Listing `code` and `id_token` to enable `code id_token` also enables
  `id_token` alone, and listing `token` enables every combination with it. Discovery has to expand the names into all
  `2^n - 1` combinations, advertising flows nobody chose.
- **Dynamic client registration cannot honor a registration.** A client that registers `code id_token` is refused,
  because the value is not a single name; mapping it onto its names would instead let the client use `id_token` alone,
  which it did not register.
- **A hybrid flow is not a grant type.** Modeling the hybrid flow as a `hybrid` grant type — a name OAuth does not
  define — means a client registered the standard way (`authorization_code` and `implicit`) never passes the
  authorization endpoint's grant check, and the token endpoint accepts `grant_type=hybrid` for redeeming a code.
- **The response mode must follow the response type.** A response that returns a token or an ID token is returned in
  the fragment by default, and the query encoding MUST NOT be used for it; error responses use the same mode (OAuth 2.0
  Multiple Response Type Encoding Practices §2.1, §2.2, §3, §5). A default chosen without the response type puts tokens
  in a query string, where server logs and `Referer` headers record them.

## Decision

**A response type is a whole value everywhere — stored, advertised, registered, and enforced as one value in one
canonical form — and it is always considered together with the grant types it uses.**

1. **Canonical form.** A response type is written with its names in ordinal order, separated by a single space
   (`code id_token`, never `id_token code`). That is the form the server stores and compares, and it is the order the
   specifications use. `OpenIdConstants.ResponseTypes` names all seven values.
2. **`response_types_supported` lists whole values.** Its domain is the seven response types in canonical form
   (ADR-0140); a value written in another order is outside the domain, because a value compared verbatim is configured
   in the form it is compared in ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)).
   Discovery advertises the list as it is. The merge stays `Intersect`, which is exact on canonical values. The
   secure-by-default baseline is `code` alone; an operator enables a front-channel flow only by listing that flow.
3. **The authorization endpoint accepts exactly a listed value.** The requested `response_type` is put in canonical
   form — protocol input may order its names freely — and must equal a listed value; its grant types (point 5) must be
   in `grant_types_supported`. An unset list is unrestricted
   ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)). A response type the tenant does not support is
   rejected with `unsupported_response_type`, and one the tenant supports but the client may not use with
   `unauthorized_client` (RFC 6749 §4.1.2.1).
4. **Dynamic client registration registers what the client asked for.** Each `response_types` value is stored in
   canonical form, duplicates removed, and reported back in that form. Omitted values default consistently: omitted
   `grant_types` are the grant types the registered response types use (`authorization_code` when neither is
   registered), and omitted `response_types` are `code`, except for a client whose grant types do not use the
   authorization endpoint (neither `authorization_code` nor `implicit`), which registers none. A registration whose
   response type uses a grant type it does not register is refused, and so is a grant type or response type the tenant
   does not support — the endpoints would refuse it at every request, so reporting it as registered would be false. All
   are `invalid_client_metadata` (RFC 7591 §2.1, §3.2.2). The response reports an empty list as empty rather than
   omitting it, since an omitted `response_types` reads as `code`. Redirection URIs are required exactly when the client
   registers a response type.
5. **Grant types follow OpenID Connect Registration §2; there is no hybrid grant type.** A response type uses
   `authorization_code` when it includes `code`, and `implicit` when it includes `id_token` or `token` — a token returned
   by the authorization endpoint is issued through the implicit grant, in a hybrid flow too. `hybrid` is not a grant
   type the server names, and the authorization code grant handler answers only `grant_type=authorization_code`.
6. **One rule decides whether a response type returns tokens.** A response type that includes `id_token` or `token`
   returns a token from the authorization endpoint. Such a response type defaults to the `fragment` response mode — for
   the success response and for the error response alike, on the first pass and on a resumed one — may not use `query`,
   and requires a `nonce` for an `openid` request. Every other response type defaults to `query`.

## Options considered

- **Map a registered combination onto its names.** Rejected: it admits each name alone, so a client registered for
  `code id_token` could use `id_token`, and the registration response would misreport what the client may do.
- **Keep single names in the setting and store whole values only for registered clients.** Rejected: one setting would
  hold two shapes merged into one effective value, and an operator still could not enable a single flow.
- **Accept any order in configuration and normalize when a value is read.** Rejected: the settings framework compares
  and merges values verbatim, and a lenient reader turns a typo into an accepted value; protocol input is canonicalized
  at the protocol boundary instead.
- **Keep the `hybrid` grant type and add `authorization_code` + `implicit` as an alternative.** Rejected: two spellings
  of one capability, one of them outside the OAuth grant type registry, with the token endpoint accepting a
  non-standard `grant_type`.
- **Enforce the reverse consistency direction too** (a registered `implicit` grant needs a response type that uses it).
  Rejected: an unused grant type grants nothing at the authorization endpoint, OpenID Connect Registration requires only
  the forward direction, and refusing it would break clients that register a broad grant list.

## Consequences

- An operator enables a flow by listing its whole value — for example `code id_token` with the `implicit` grant — and
  enables nothing else; discovery advertises exactly the list.
- Dynamic client registration accepts any registered response type the tenant supports, so the OpenID Foundation
  hybrid and implicit certification profiles can register their clients the standard way.
- A client registered only for `code id_token` is refused `code` with `unauthorized_client`.
- A response that returns a token is never redirected in a query string, whether it carries tokens or an error.
- A configured or stored `response_types_supported` value written in a non-canonical order, or a `grant_types_supported`
  value of `hybrid`, is outside its domain and fails as ADR-0140 describes; such a document is corrected by rewriting the
  value in canonical form, or as `authorization_code` plus `implicit`.

## References

- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) — the response-type domain this replaces.
- [ADR-0083](0083-secure-by-default-discovery-metadata.md) — secure-by-default discovery; the baseline stays `code`.
- [ADR-0063](0063-dynamic-client-registration.md) — dynamic client registration.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — an unset ceiling is unrestricted.
- RFC 6749 §3.1.1 and §4.1.2.1, RFC 7591 §2, §2.1 and §3.2.2, RFC 8414 §2, OpenID Connect Discovery 1.0 §3, OpenID
  Connect Dynamic Client Registration 1.0 §2, OAuth 2.0 Multiple Response Type Encoding Practices.
- Code: `NCode.Identity.OpenId.OpenIdConstants.ResponseTypes`, `NCode.Identity.OpenId.Settings.OpenIdSettingRules`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.ResponseTypeValues`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers.DefaultValidateAuthorizationRequestHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.DefaultAuthorizationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Registration.DefaultClientRegistrationEndpointHandler`.
