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

A review (backlog `R11`) found three weaknesses in the comparison:

- The query of the requested URI was not compared, so a request could add or change query parameters on a registered
  URI.
- Any loopback URI was accepted when the client allowed loopback redirects, even one the client never registered, and
  the setting `allow_loopback_redirect` defaulted to `true`.
- The response writer kept the request-supplied query parameters, so those parameters travelled to the client's
  redirect endpoint inside an accepted response.

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
   so a child cannot allow it again. `fapi2` sets no ceiling, because FAPI 2.0 permits native loopback redirects.
4. **The registered query is kept.** The redirect URI of an accepted request equals a registered URI as written, so its
   query is the registered query, and the response writer keeps it and adds the response parameters to it (RFC 6749
   §3.1.2). A response parameter replaces a registered query parameter of the same name.
5. **One check, in the delivery resolver.** `IAuthorizationResponseDeliveryResolver` runs the check when it derives the
   `AuthorizationResponseDelivery` for the authorization endpoint and for the PAR endpoint
   ([ADR-0201](0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md)). A parked
   continuation does not check its stored redirect URI again, because it was checked when the request arrived. When a
   request has both an outer and a request-object `redirect_uri`, the two are compared as written
   ([ADR-0202](0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

## Consequences

- A client that relied on extra query parameters on its `redirect_uri`, or on an unregistered loopback URI, must
  register its exact URIs. A client that registers a loopback IP redirect URI keeps the any-port behavior unless its
  profile turns it off.
- A client under `fapi1-advanced` registers the exact loopback port, or does not use a loopback redirect.
- The comparison lives in one place, `RedirectUriMatcher`, which works on strings and has no dependency on the
  request, so every path that checks a redirect URI can share it.
- The delivery still holds a parsed `Uri`, so the URI the response is sent to, and the one the token endpoint compares
  against, can differ from the registered string where parsing changes the serialization. Keeping an accepted URI as
  written after the check is pending work (backlog `R52`).
- A redirect URI is not yet required to use `https` under the FAPI profiles. That is pending work (backlog `R53`).
