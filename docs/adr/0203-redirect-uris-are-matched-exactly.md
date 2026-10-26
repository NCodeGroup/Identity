# 203. Redirect URIs are matched exactly

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

The authorization response is sent to the `redirect_uri` the request names, so the check that the URI belongs to the
client is what stops a code or a token from being sent to an attacker. RFC 9700 §4.1.3 and OAuth 2.1 require the
authorization server to compare a redirect URI to the registered ones by simple string comparison. RFC 8252 §7.3 makes
one exception: a native app's loopback IP redirect URI may use any port, because the app can only know its port when
it starts. FAPI 1.0 Part 1 §5.2.2-20 requires the redirect URI to be pre-registered and matched, and FAPI 2.0 §5.3.2.2
requires the same exact match yet permits the native-app loopback form.

A review (issue [#202](https://github.com/NCodeGroup/Identity/issues/202)) found three weaknesses in the comparison:

- The query of the requested URI was not compared, so a request could add or change query parameters on a registered
  URI.
- Any loopback URI was accepted when the client allowed loopback redirects, even one the client never registered, and
  the setting `allow_loopback_redirect` defaulted to `true`.
- The response writer kept the request-supplied query parameters, so those parameters travelled to the client's
  redirect endpoint inside an accepted response.

A later review (issue [#243](https://github.com/NCodeGroup/Identity/issues/243)) found that an accepted redirect URI did not stay as written. The message model parsed
`redirect_uri` into a `Uri`, so a parked continuation and a stored authorization code carried its normalized form
(`myapp://callback` became `myapp://callback/`, and the host was lower-cased), the response went to the form that a
`UriBuilder` produced, and the token endpoint compared with `Uri` equality, which ignores the case of the host and the
user information, although RFC 6749 §4.1.3 requires the values to be identical.

The same review (issue [#244](https://github.com/NCodeGroup/Identity/issues/244)) found that nothing enforced the redirect URI schemes of the FAPI profiles. FAPI 1.0
Part 1 §5.2.2-20 requires `https` and §7.5 refuses private-use and loopback redirection, and FAPI 1.0 Advanced inherits
both. FAPI 2.0 Security Profile §5.3.2.2-8 refuses `http` except for the loopback redirect of a native client
(RFC 8252 §7.3).

## Decision

1. **A requested redirect URI is matched exactly, as written.** The raw `redirect_uri` string is compared ordinally to
   each registered string. It is never parsed or normalized first, so a value that differs in any character (the case
   of the host, a trailing slash, a query) does not match, and a parsed `Uri` is not compared because its equality
   ignores the case of the host. More than one `redirect_uri` value is refused, because the values would otherwise be
   joined into one string. An unregistered URI is still refused with `unauthorized_client` and a `400`, and is not
   redirected.
2. **Only the port of a registered loopback IP redirect URI may differ.** When the requested and the registered URI
   both begin with `http://127.0.0.1` or `http://[::1]`, and are ordinally equal after their optional port, including
   the path and the query, the two match. The client setting `allow_loopback_redirect` controls the exception. It keeps
   its persisted name, but it admits no unregistered loopback URI and only lets a registered one vary its port. It stays
   `true` by default, because RFC 8252 §7.3 and OAuth 2.1 require the authorization server to allow any port, and the
   URI itself must still be registered. `localhost` gets no port flexibility, because RFC 8252 §8.3 recommends the IP
   literal.
3. **`fapi1-advanced` turns the exception off.** The profile sets a ceiling of `false` for `allow_loopback_redirect`
   ([ADR-0202](0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)), and the setting merges as an `And`,
   so a child cannot allow it again. `fapi2` sets no ceiling for this setting, because FAPI 2.0 permits native loopback
   redirects (decision 8 caps its redirect URI kinds).
4. **The registered query is kept.** The redirect URI of an accepted request equals a registered URI as written, so its
   query is the registered query, and the response writer keeps it and adds the response parameters to it (RFC 6749
   §3.1.2). A response parameter replaces a registered query parameter of the same name.
5. **One check, in the delivery resolver.** `IAuthorizationResponseDeliveryResolver` runs the check when it derives the
   `AuthorizationResponseDelivery` for the authorization endpoint and for the PAR endpoint
   ([ADR-0201](0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)). A parked
   continuation checks neither its stored redirect URI nor its kind again, because both were checked when the request
   arrived. When a request has both an outer and a request-object `redirect_uri`, the two are compared as written
   ([ADR-0202](0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).
6. **An accepted redirect URI is kept as written.** `redirect_uri` is a string wherever the server carries it
   (`OpenIdCommonParameters.RedirectUri`, `IBaseAuthorizationRequestValues`, `IAuthorizationRequest`, `ITokenRequest`,
   `AuthorizationResponseDelivery`). A `Uri` is parsed only to inspect a part of it (its absoluteness, its kind, the
   origin that salts `session_state`) and is never stored, compared, or serialized. The response writer keeps each
   registered query pair as written, drops one that a response parameter replaces, keeps the `#_=_` fragment, and
   percent-encodes only a character that a header value cannot carry as written, a non-ASCII character (RFC 3987 §3.1)
   or an ASCII control character or space, in the `Location` header; a form post uses the string as its action. The
   token endpoint compares the value ordinally and treats an empty value as missing. `UriParser` writes a parsed `Uri`
   as its original string.
7. **The kind of a redirect URI is an open vocabulary that the settings narrow.** The vocabulary is open
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): a registered `IRedirectUriKind` names itself and
   decides whether a parsed URI is of its kind, and `IRedirectUriKindCatalog` resolves the kinds by name and judges a
   URI against a client's settings. The built-in kinds sort a URI into at most one kind: `https`; `loopback`, an `http`
   URI whose host is `127.0.0.1` or `[::1]` (RFC 8252 §7.3, with `localhost` excluded per §8.3); `http`, any other
   `http` URI; and `private_use`, a URI whose scheme is neither `http` nor `https` and is a domain name in reverse
   order: it contains a period and no empty label, such as `com.example.app` (RFC 8252 §7.1; OAuth 2.1 §2.3.1 rejects a
   private-use scheme without a period). The setting `redirect_uri_kinds_supported` lists the permitted kinds. It
   applies to tenants and clients, merges with `Intersect`, is not advertised, accepts registered kind names only, and
   permits every registered kind while it is unset
   ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)). Kinds compare by name, and a URI is permitted when
   any listed kind matches it. A value that is not an absolute URI, or that no registered kind matches (`javascript:`,
   `data:`, `file:`, `ftp:`, `ws:`, `myapp:`), is never permitted, whether or not the setting is set.
8. **The FAPI profiles cap the kinds.** `fapi1-advanced` caps `redirect_uri_kinds_supported` at `https` and keeps
   `allow_loopback_redirect` off; `fapi2` caps it at `https`, `loopback`, and `private_use`.
9. **The kind is checked where a client is registered and where the delivery is derived.** Dynamic client registration,
   the management API, and the conformance seed check the kinds of a client's `redirect_uris` and
   `post_logout_redirect_uris` through the client coherence rules
   ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)), dynamic client registration
   refusing with `invalid_redirect_uri`. A tenant or server settings change that would leave a registered URI
   unpermitted is refused. The delivery resolver checks the kind after the registration match, at the authorization
   endpoint and at the PAR endpoint, and refuses a URI with `invalid_request` and a `400` without redirecting.
10. **A post-logout redirect URI follows the same kinds and is delivered as written.** The end-session handler compares
    `post_logout_redirect_uri` ordinally with the registered values and then checks its kind; a URI that fails either
    check is not redirected to. The redirect is a `302 Found` whose `Location` header uses the escaping of decision 6.

## Consequences

- A client that relied on extra query parameters on its `redirect_uri`, or on an unregistered loopback URI, must
  register its exact URIs. A client that registers a loopback IP redirect URI keeps the any-port behavior unless its
  profile turns it off.
- The comparison lives in one place, `RedirectUriMatcher`, which works on strings and has no dependency on the
  request, so every path that checks a redirect URI can share it.
- A client receives the response at the redirect URI exactly as it sent it, and must send the same string to the token
  endpoint. A value that differs in the case of its host, a trailing slash, or its user information is refused with
  `invalid_grant`.
- A client under `fapi1-advanced` registers only `https` redirect URIs, so it has no loopback redirect URI whose port
  could vary, and a client under `fapi2` registers no `http` redirect URI except one whose host is a loopback IP
  literal. A native client that needs a loopback or a private-use redirect selects `fapi2` or no FAPI profile.
- A scope that lists `http` does not keep `loopback` under a ceiling that lists only `loopback`, because the two kinds
  are distinct and the settings merge with `Intersect`; list the narrower kind by name.
- A tenant or server settings change that would leave a registered redirect URI unpermitted is refused, so no stored
  client is left with a URI that its effective `redirect_uri_kinds_supported` does not permit.
- A native app registers a reverse-domain scheme (`com.example.app:/callback`). A stored `myapp://callback` is of no
  kind, so it is refused at authorization and at end session.
- A `javascript:` or `data:` URI can never be a redirect target, so the front-channel logout page's scripted redirect
  cannot run script in the server's origin.
