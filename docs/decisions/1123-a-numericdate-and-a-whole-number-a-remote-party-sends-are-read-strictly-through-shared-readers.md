# 1123. A NumericDate and a whole number a remote party sends are read strictly through shared readers

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

RFC 7519 §2 defines a `NumericDate` as a JSON number of seconds since `1970-01-01T00:00:00Z` (UTC, ignoring leap
seconds) and allows non-integer values. JWT time claims (`exp`, `nbf`, `iat`), OpenID Connect `auth_time`, the DPoP
proof `iat` (RFC 9449) and Unix-seconds request parameters are all `NumericDate`s, and most arrive from outside the
trust boundary. Reading them ad hoc creates three classes of defect:

- **Faulting on range.** `DateTimeOffset.FromUnixTimeSeconds` throws `ArgumentOutOfRangeException` outside
  `[-62135596800, 253402300799]`. A DPoP proof is signed by a key the client chooses, so any caller can send
  `iat: 99999999999999` and turn an `invalid_dpop_proof` rejection into an unhandled server error; a client assertion's
  `exp` at the top of the range overflows when a replay-retention margin is added to it.
- **Rejecting valid values.** Reading a `NumericDate` as a `long` rejects a fractional value that RFC 7519 permits.
- **Failing open.** A validator that treats a present but unreadable `nbf` or `exp` as absent ignores it: an unparseable
  `nbf` lets a token be used early, and an unparseable `exp` never expires when expiry is not required. A validator that
  reads time claims through a lenient date accessor also accepts date strings, which are not `NumericDate`s, and
  compares an offset-bearing string's local ticks against a UTC clock.

JSON has one number type ([RFC 8259 §6](https://datatracker.ietf.org/doc/html/rfc8259#section-6)): the texts `60`,
`60.0` and `6e1` are one value, and how a sender writes it is the sender's choice. A pushed authorization response
([RFC 9126 §2.2](https://datatracker.ietf.org/doc/html/rfc9126#section-2.2)) and a token response
([RFC 6749 §5.1](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1)) both send `expires_in`, a lifetime in
seconds, as a JSON number, so a provider may write a lifetime of 60 seconds as `60.0`. `JsonElement.TryGetInt32` and the
serializer's `int` handling accept only the integer form, so a provider that writes `60.0` or `6e1` would fail a
successful push, which the protocol client reports as an `OpenIdProviderException`, and the whole token request when
`TokenResponse.ExpiresIn` is deserialized, although the value is a whole number of seconds. The client must still not
invent a lifetime the provider never stated: a fraction such as `60.5`, a string and a number outside the `int` range
are not a count of seconds it can use.

## Decision

**Every `NumericDate` and every whole number a remote party sends is read through one strict, range-guarded reader in
`NCode.Identity.Json`, and a present but invalid value fails closed.**

NumericDate:

- **Public, non-throwing conversions on `DateCoercion`.** `TryFromNumericDate(long)` and `TryFromNumericDate(double)`
  convert integer or fractional seconds to a UTC `DateTimeOffset` at tick precision. `TryParseNumericDate(string?)`
  parses the JSON-number text a claim value or request parameter carries: sign, fraction and exponent, no whitespace,
  separators or dates. Each returns `false`, never throws, for a non-finite or out-of-range value.
- **Strict JSON accessors.** `JsonElement.TryGetNumericDate()` and `TryGetPropertyNumericDate(name)` accept only a JSON
  number, never a string. The loosely typed accessors share the conversion for numbers.
- **Fail closed.** A time claim that is present must be valid: `TokenLifetimeRequirementHandler` fails a token whose
  `nbf` or `exp` is present but invalid, and a required claim such as the DPoP `iat` or a client assertion's `exp` fails
  with the endpoint's own error.
- **Saturating arithmetic.** Adding a margin near the top of the range saturates at `DateTimeOffset.MaxValue`.
- **Tool-enforced.** The repository's `BannedSymbols.txt` bans `DateTimeOffset.FromUnixTimeSeconds` and
  `FromUnixTimeMilliseconds` (RS0030), so a new site cannot reintroduce the throwing conversion.

Whole numbers:

- **Non-throwing readers.** `JsonElement.TryGetWholeInt32(out int)` and `TryGetPropertyWholeInt32(name, out int)` mirror
  the NumericDate readers. The reader checks `ValueKind`, accepts the integer form, and otherwise accepts a value that
  `TryGetDecimal` reads, that `decimal.Truncate` leaves unchanged and that lies in `int` range. It never throws.
- **`decimal`, not `double`.** `60.0000000000000001` is refused, whereas a `double` would round it to 60.
- **What is refused.** A non-zero fraction, a value outside `int` range, a JSON string such as `"60"` and any
  non-number. A value below `decimal` precision (`1e-400`) reads as 0, which the pushed authorization response refuses
  as not positive. `TokenResponse.ExpiresIn` is not checked for a positive value, so a zero or negative lifetime makes
  the caller treat the token as expired.
- **A public converter.** `WholeInt32JsonConverter : JsonConverter<int>` in `NCode.Identity.Json` applies the same rule
  to a serialized member, writes a JSON integer and fails with `JsonException`. It is public there because an
  `*.Abstractions` package holds no helper logic ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md))
  and production assemblies never use cross-assembly `InternalsVisibleTo`
  ([project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md)).
- **Call sites.** The pushed authorization response's `expires_in` is read with `TryGetPropertyWholeInt32` and must be
  positive ([ADR-1275](1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md)).
  `TokenResponse.ExpiresIn` is an `int?` read through `WholeInt32JsonConverter`, whose failure `DefaultTokenClient`
  wraps in `OpenIdProviderException`. `TokenResponse.RefreshTokenExpiresIn` ignores a value that is not a positive whole
  number.
- **No other reader.** Any whole number a remote party sends is never read through `JsonElement.TryGetInt32` or the
  serializer's default `int` handling, as the production conventions say.

## Options considered

- **Catch `ArgumentOutOfRangeException` at each site.** Leaves the policy scattered, depends on every site remembering
  the guard, and does nothing for fractional values or the fail-open validator. Rejected.
- **Read time claims through `TryGetPropertyValue<DateTimeOffset>`.** Range-guarded, but it parses date strings
  leniently, accepting non-`NumericDate` input and comparing local ticks for offset strings. Rejected for time claims.
- **Integer seconds only.** Simpler, but rejects values RFC 7519 allows and real clients send. Rejected.
- **Round or truncate a fraction.** Invents a lifetime the provider never stated. Rejected.
- **`TryGetDouble` for the whole-value test.** Binary rounding accepts `60.0000000000000001` as 60. Rejected in favor of
  `decimal`.
- **Accept numeric strings such as `"60"`.** The RFCs specify a JSON number, and a string is not a `NumericDate` either.
  Rejected.
- **Widen `TryGetValue<int>`.** Silently changes the result for every existing caller. Rejected.
- **A private reader per client package.** Two copies of one rule. Rejected.

The chosen option is one shared set of readers in `NCode.Identity.Json` that every package references.

## Consequences

- Hostile time values produce the protocol's own rejection (`invalid_dpop_proof`, `invalid_client`, a failed lifetime
  validation, `invalid_request`), never a server error.
- A token with an unreadable `nbf` or `exp` is rejected.
- Fractional NumericDates are accepted everywhere, with sub-second precision preserved.
- A provider that writes `expires_in` as `60.0` or `6e1` works, for both a pushed authorization response and a token
  response.
- A value that is not a whole number in `int` range is rejected: the pushed authorization response with an
  `OpenIdProviderException`, the token response at deserialization.
- The client-assertion lifetime cap is in
  [ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md).

## References

- `src/NCode.Identity.Json/DateCoercion.cs`, `src/NCode.Identity.Json/JsonElements.cs`,
  `src/NCode.Identity.Json/ObjectGraphExtensions.cs`, `src/NCode.Identity.Json/WholeInt32JsonConverter.cs`
- `src/NCode.Identity.Jwt/Validation/Handlers/TokenLifetimeRequirementHandler.cs`
- `src/NCode.Identity.OpenId.Client/Authorization/DefaultAuthorizationRequestClient.cs`
- `src/NCode.Identity.OpenId.Client.Abstractions/TokenResponse.cs`
- [RFC 7519 §2 — `NumericDate`](https://datatracker.ietf.org/doc/html/rfc7519#section-2)
- [RFC 9449 §4.3 — DPoP proof `iat`](https://datatracker.ietf.org/doc/html/rfc9449#section-4.3)
- [RFC 7523 §3 — JWT assertion `exp`](https://datatracker.ietf.org/doc/html/rfc7523#section-3)
- [RFC 8259 §6 — Numbers](https://datatracker.ietf.org/doc/html/rfc8259#section-6)
- [RFC 9126 §2.2 — Successful Response](https://datatracker.ietf.org/doc/html/rfc9126#section-2.2)
- [RFC 6749 §5.1 — Successful Response](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1)
- [project-template ADR-0104](project-template/0104-no-cross-assembly-internalsvisibleto.md)
- [ADR-1122](1122-jwt-claim-values-map-to-and-from-json-by-their-declared-type-alone.md)
- [ADR-1187](1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md)
- [ADR-1275](1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md)
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)
- Issues [#33](https://github.com/NCodeGroup/identity/issues/33), [#352](https://github.com/NCodeGroup/identity/issues/352)
