# 123. RFC 7519 NumericDate is read strictly, range-guarded, through `DateCoercion`

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode.Identity maintainers

## Context

RFC 7519 §2 defines a `NumericDate` as a JSON number of seconds since `1970-01-01T00:00:00Z` (UTC, ignoring leap
seconds) and explicitly allows non-integer values. JWT time claims (`exp`, `nbf`, `iat`), OpenID Connect `auth_time`, the
DPoP proof `iat` (RFC 9449), and Unix-seconds request parameters are all `NumericDate`s, and most of them arrive from
outside the trust boundary. Reading them ad hoc creates three classes of defect:

- **Faulting on range.** `DateTimeOffset.FromUnixTimeSeconds` throws `ArgumentOutOfRangeException` outside
  `[-62135596800, 253402300799]`. A DPoP proof is signed by a key the client chooses, so any caller can send
  `iat: 99999999999999` and turn an `invalid_dpop_proof` rejection into an unhandled server error; a client assertion's
  `exp` at the top of the range overflows when a replay-retention margin is added to it.
- **Rejecting valid values.** Reading a `NumericDate` as a `long` rejects a fractional value that RFC 7519 permits —
  some clients emit `iat` with a fractional part.
- **Failing open.** A validator that treats a present-but-unreadable `nbf` or `exp` as absent ignores it: an
  unparseable `nbf` lets a token be used early, and an unparseable `exp` never expires when expiry is not required. A
  validator that reads time claims through a lenient date accessor additionally accepts date **strings**, which are not
  `NumericDate`s, and compares an offset-bearing string's local ticks against a UTC clock.

## Decision

**Every `NumericDate` is read through one strict, range-guarded policy in `NCode.Json`, and a present-but-invalid value
fails closed.**

- **Public, non-throwing conversions on `DateCoercion`.** `TryFromNumericDate(long seconds, …)` and
  `TryFromNumericDate(double seconds, …)` convert integer or fractional seconds to a UTC `DateTimeOffset`, keeping the
  fraction to tick precision; `TryParseNumericDate(string?, …)` parses the JSON-number text a claim value or request
  parameter carries (invariant culture; sign, fraction, and exponent; no whitespace, separators, or date formats). Each
  returns `false` — never throws — for a non-finite value or an instant outside the representable range.
- **Strict JSON accessors.** `JsonElement.TryGetNumericDate()` and `TryGetPropertyNumericDate(name)` accept only a JSON
  **number** (integer or fractional); a string is never a `NumericDate`. The loosely-typed accessors
  (`TryGetValue<DateTimeOffset>`, `ObjectGraphExtensions.TryGetValue<DateTimeOffset>`) use the same conversion for
  numbers, so they too accept fractional values and never fault.
- **Fail closed.** A time claim that is present must be a valid `NumericDate`; the JWT lifetime handler fails a token
  whose `nbf` or `exp` is present but invalid rather than treating it as absent. Required claims (DPoP `iat`,
  client-assertion `exp`) reject an invalid value with the endpoint's own error.
- **Saturating arithmetic.** Adding a margin to a validated instant near the top of the range saturates at
  `DateTimeOffset.MaxValue` instead of overflowing.
- **Tool-enforced.** `DateTimeOffset.FromUnixTimeSeconds` / `FromUnixTimeMilliseconds` are banned in shipping code
  (`BannedSymbols.txt`, `RS0030`), so a new site cannot reintroduce the throwing conversion.

## Options considered

- **Catch `ArgumentOutOfRangeException` at each site.** Leaves the policy scattered, depends on every future site
  remembering the guard, and does nothing for fractional values or the fail-open validator. Rejected.
- **Read time claims through `TryGetPropertyValue<DateTimeOffset>`.** Range-guarded, but it also parses date strings
  leniently — accepting non-`NumericDate` input and reintroducing the local-ticks comparison for offset strings.
  Rejected for time claims; it remains the right accessor for a loosely-typed "date of any shape" read.
- **Integer seconds only.** Simpler, but rejects values RFC 7519 allows and that real clients send. Rejected.

## Consequences

- Malformed or hostile time values produce the protocol's normal rejection (`invalid_dpop_proof`, `invalid_client`,
  a failed lifetime validation, `invalid_request` for a parameter) instead of a server error.
- A token with an unreadable `nbf`/`exp` is rejected, and a date-string `exp` is no longer honored.
- A fractional `NumericDate` is accepted everywhere, with sub-second precision preserved.
- A client assertion whose `exp` is far in the future is still accepted (only the overflow is fixed); capping assertion
  lifetime, which RFC 7523 §3 permits, is a separate hardening item (issue [#78](https://github.com/NCodeGroup/Identity/issues/78)) — since delivered by
  [ADR-0124](0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md).

## References

- `src/NCode.Json/DateCoercion.cs`, `src/NCode.Json/JsonElements.cs`, `src/NCode.Json/ObjectGraphExtensions.cs`
- `src/NCode.Identity.Jwt/Validation/Handlers/TokenLifetimeRequirementHandler.cs`
- [RFC 7519 §2 — `NumericDate`](https://datatracker.ietf.org/doc/html/rfc7519#section-2)
- [RFC 9449 §4.3 — DPoP proof `iat`](https://datatracker.ietf.org/doc/html/rfc9449#section-4.3)
- [RFC 7523 §3 — JWT assertion `exp`](https://datatracker.ietf.org/doc/html/rfc7523#section-3)
- [ADR-0121](0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md) — the string-date half of
  `DateCoercion`
- Issue [#33](https://github.com/NCodeGroup/Identity/issues/33)
