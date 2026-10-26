# 121. Claim dates parse only by declared type, through the shared `DateCoercion` policy

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode.Identity maintainers

## Context

The JWT library maps claims in both directions: encoding turns each `Claim` into a native JSON value according to its
`Claim.ValueType`, and decoding turns each JSON payload member back into a `Claim`. Both directions live in one
claim-value mapper ([ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md)), and both touch dates.

`NCode.Json.DateCoercion` is the family's date/time coercion policy: the loosely-typed JSON mappers
(`ObjectJsonConverter`, `JsonElements`, `ObjectGraphExtensions`) parse through it so they accept the same formats and
produce identical values. A claim mapping that parses dates with its own `DateTime(Offset).TryParse` calls duplicates
that policy and drifts from it, and two hazards specific to the claim mapping shape the decision:

- **Out-of-range instants clamp under `DateTime` parsing.** For text whose instant lies outside the representable
  `DateTimeOffset` range (e.g. `0001-01-01T00:00:00+05:00`), `DateTimeOffset` parsing fails, but `DateTime` parsing
  succeeds by clamping to a machine-local value — a wrong instant rather than a failure. A `DateTime` fallback behind a
  `DateTimeOffset` parse is reachable only in exactly that case.
- **Sniffing strings for dates corrupts them.** Lenient date parsing accepts far more than ISO 8601. A decoder that
  re-types any date-looking JSON string as `ClaimValueTypes.DateTime` and rewrites it to a normalized timestamp alters
  claims after signature verification: `"1.2"` becomes January 2 of the current year, `"10:30"` becomes today's date
  at 10:30 (non-deterministic), and an OpenID Connect `birthdate` of `"1990-03-15"` becomes
  `"1990-03-15T00:00:00.0000000Z"`, breaking the `YYYY-MM-DD` format the specification requires.

JSON has no date type. RFC 7519 represents time claims as `NumericDate` numbers, and OpenID Connect string-valued dates
(`birthdate`) are defined by their exact textual format. A string's meaning as a date is therefore a property the
**producer declares**, not something a consumer can infer from its shape.

## Decision

**A date is parsed only where its type is declared, and always through the public `NCode.Json.DateCoercion` policy.**

- **`DateCoercion` is public.** Its string-parsing operations — `TryParseDateTimeOffset` and `TryParseDateTime` — are
  the public contract for turning a date/time string into a CLR value, so any package parses dates under the same
  policy instead of hand-rolling `DateTime(Offset).TryParse`. Its RFC 7519 `NumericDate` conversions are public too,
  under the strict, range-guarded rules of [ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md).
- **Encode honors the declared type.** A claim typed `ClaimValueTypes.DateTime` parses with
  `DateCoercion.TryParseDateTimeOffset` (zone-less → UTC, explicit offset preserved, out-of-range → failure). A value
  that does not parse — including an unrepresentable instant — is emitted as its verbatim string, never as a clamped or
  machine-local guess.
- **Decode never sniffs.** A JSON string decodes to a `ClaimValueTypes.String` claim carrying the exact string. The
  decoder does not infer a date (or any other type) from a string's shape.

## Options considered

- **Route the decode sniffing through `DateCoercion` as well.** Consolidates the code but keeps the defect: the shared
  policy is deliberately lenient (it serves callers that already know the text is a date), so sniffing through it still
  rewrites `"1.2"`, `"10:30"`, and `birthdate`. Rejected — it would cement a data-integrity bug under a single name.
- **Sniff only strict ISO 8601 on decode** (e.g. `JsonElement.TryGetDateTimeOffset`). Avoids the worst false positives
  but still matches `birthdate`-style date-only strings and still rewrites the value, so round-trip fidelity is lost.
  It also adds a second, stricter date grammar alongside `DateCoercion`. Rejected.
- **Never sniff; parse by declared type (chosen).** Deterministic and lossless for strings; the decoded identity
  carries exactly what the signed token said. A consumer that wants a typed date reads the claim and parses it with
  `DateCoercion`, where it has established the claim is a date.

## Consequences

- Claims in a validated `ClaimsIdentity` are byte-for-byte the strings the issuer signed; authorization logic comparing
  claim values sees the issuer's values, not a re-formatted rendering.
- A string claim never arrives typed `ClaimValueTypes.DateTime` from decoding. Code that needs a date from a string
  claim calls `DateCoercion.TryParseDateTimeOffset` (or `TryParseDateTime`) on `Claim.Value`.
- Re-encoding a decoded identity reproduces its string claims verbatim, so token exchange or re-issuance does not drift
  date-like values.
- `DateCoercion` is public surface tracked in `PublicAPI.Unshipped.txt`; the convention to parse date strings through
  it lives in the production coding conventions.
- The lenient string coercion offered by `ObjectJsonConverterOptions` (`DateString`) is a separate, explicitly
  configurable choice for loosely-typed `object` deserialization and is unaffected by this decision.

## References

- `src/NCode.Json/DateCoercion.cs`
- `src/NCode.Identity.Jwt/Claims/ClaimValueMapper.cs` (`ToJsonValue` / `ToClaim`)
- [ADR-0122](0122-jwt-claim-values-map-to-json-by-declared-type.md) — the claim-value mapping this date rule is part of
- [RFC 7519 §2 — `NumericDate`](https://datatracker.ietf.org/doc/html/rfc7519#section-2)
- [OpenID Connect Core 1.0 §5.1 — Standard Claims (`birthdate`)](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)
- Issue [#32](https://github.com/NCodeGroup/Identity/issues/32)
