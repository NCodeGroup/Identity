# 1122. JWT claim values map to and from JSON by their declared type alone

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Context

A `Claim` carries its value as a string plus a value type: an XML Schema type URI from `ClaimValueTypes`
(`#integer32`, `#integer64`, `#uinteger64`, `#integer`, `#double`, `#boolean`, `#string`, `#dateTime`, …) or a
`JsonClaimValueTypes` marker (`JSON`, `JSON_ARRAY`, `JSON_NULL`). A JWT payload carries JSON values. The JWT library
converts in both directions: encoding a subject's claims into a payload, and decoding a validated payload into a
`ClaimsIdentity`. Decoded identities are routinely re-encoded (token exchange, re-issuance, refresh), so the two
directions form a round trip whose fidelity matters.

Two conversions that live apart drift apart, and each drift is a silent data defect:

- A decoder that types every number as `#double` makes re-encoding go through `double`, which loses integers above
  2^53 (`9007199254740993` becomes `9007199254740992`): identifiers, counters and large timestamps.
- An encoder that knows only some declared types emits the rest as strings, so an unsigned or unbounded integer claim
  becomes a JSON string, and `JSON_NULL` becomes `""`.
- Values JSON cannot represent, or that are malformed for their type, make an encoder throw instead of degrade: a
  `#double` of `NaN` or `Infinity` fails serialization, and a malformed `JSON` value fails parsing.
- Lenient numeric parsing (`NumberStyles.Any`) accepts thousands separators, currency symbols and parenthesized
  negatives, none of which are in the XML Schema lexical space the value type declares.

Dates add two hazards. `NCode.Identity.Json.DateCoercion` is the family's date and time coercion policy, and the
loosely typed JSON mappers (`ObjectJsonConverter`, `JsonElements`, `ObjectGraphExtensions`) parse through it so they
accept the same formats and produce identical values; a claim mapping that hand-rolls `DateTime(Offset).TryParse`
duplicates that policy and drifts from it. Parsing also behaves differently by type: for text whose instant lies
outside the representable `DateTimeOffset` range (`0001-01-01T00:00:00+05:00`), `DateTimeOffset` parsing fails, while
`DateTime` parsing succeeds by clamping to a machine-local value, a wrong instant rather than a failure. And lenient
date parsing accepts far more than ISO 8601, so a decoder that re-types any date-looking string as a date alters claims
after signature verification: `"1.2"` becomes January 2 of the current year, `"10:30"` becomes today at 10:30, and an
OpenID Connect `birthdate` of `"1990-03-15"` becomes `"1990-03-15T00:00:00.0000000Z"`, breaking the `YYYY-MM-DD` format
the specification requires.

JSON has no date type. RFC 7519 represents time claims as `NumericDate` numbers, and OpenID Connect string-valued dates
(`birthdate`) are defined by their exact textual format. A string's meaning as a date is a property the producer
declares, not something a consumer can infer from its shape.

## Decision

**One internal `ClaimValueMapper` owns both directions of the claim-value mapping, and each direction is driven by the
value's declared type alone.**

- **Both directions live together.** `ClaimValueMapper.ToClaim` (JSON to `Claim`) and
  `ClaimValueMapper.ToJsonValue` (`Claim` to JSON) sit side by side so their symmetry is visible and tested as a pair.
  A round-trip test over every JSON scalar kind guards them. The encoder and the claims-identity factory only call
  them.
- **One factory maps every claim set.** `IClaimsIdentityFactory` (`NCode.Identity.Jwt.Abstractions`) maps any JSON
  claim set to a `ClaimsIdentity`, with an issuer for a set that carries no `iss`. `DefaultJwtValidator` and the
  protocol client's UserInfo both map through it, so one JSON value gives one `Claim`.
- **Decode keeps the exact JSON text and never sniffs.** A string is always `#string`: it is never re-typed by its
  shape, never probed for a date or any other type, so `"1.2"`, `"10:30"` and a `birthdate` stay verbatim. A number
  takes the first match among `#integer32`, `#integer64`, `#uinteger64` and `#integer` (the unbounded type, for any
  larger integer literal), or `#double` when it has a fraction or an exponent. `null` is `JSON_NULL`, an object is
  `JSON`, an array is `JSON_ARRAY` and a boolean is `#boolean`.
- **Encode parses the value in its declared type's lexical space.** Parsing uses the invariant culture with
  `NumberStyles.Integer` for integers and `NumberStyles.Float` for reals. `#integer32`, `#integer64`, `#uinteger32` and
  `#uinteger64` map to `int`, `long`, `uint` and `ulong`. `#integer` maps to the narrowest of `long` or `ulong`, or an
  exact JSON number beyond 64 bits. `#double` must be finite. `JSON` and `JSON_ARRAY` must be well formed and of the
  declared kind (object or array). `JSON_NULL` encodes as `null`.
- **Dates parse through `DateCoercion`.** A `#dateTime` claim encodes through `DateCoercion.TryParseDateTimeOffset`: a
  zone-less value is UTC, an explicit offset is kept, and an out-of-range instant fails rather than clamping.
- **A value outside its declared type degrades to its verbatim string.** A malformed, out-of-range or unrepresentable
  value, or an unknown value type, is emitted as the claim's string value. Encoding never throws on claim content and
  never guesses a different type.
- **`DateCoercion` is the one way to parse a date string.** `NCode.Identity.Json.DateCoercion` is public, and its
  `TryParseDateTimeOffset` and `TryParseDateTime` are the contract for turning a date or time string into a CLR value.
  The production coding conventions forbid hand-rolled `DateTime(Offset).TryParse`. Its RFC 7519 `NumericDate`
  conversions are public too, under the strict, range-guarded rules of
  [ADR-1123](1123-numeric-date-is-read-strictly-and-range-guarded.md).
- **A lenient coercion stays separate and opt-in.** `ObjectJsonConverterOptions.DateString` is a different, explicitly
  configured coercion for loosely typed `object` deserialization; this mapping does not use it.

## Options considered

- **Keep the two directions in separate files, fixing each defect in place.** Smallest diff, but the drift behind the
  defects is structural: two halves of one bijection in two files, neither tested against the other. Rejected in favor
  of one mapper with a round-trip test.
- **Sniff dates on decode through `DateCoercion`.** Consolidates the code but keeps the defect: the shared policy is
  deliberately lenient, because it serves callers that already know the text is a date, so sniffing through it still
  rewrites `"1.2"`, `"10:30"` and `birthdate`. Rejected.
- **Sniff only strict ISO 8601 on decode** (for example `JsonElement.TryGetDateTimeOffset`). Avoids the worst false
  positives but still matches `birthdate`-style date-only strings, still rewrites the value, and adds a second, stricter
  date grammar beside `DateCoercion`. Rejected.
- **Write a number's literal text on encode.** Exact for reals beyond `double` precision, but the claim value of an
  application-created claim is in XML Schema lexical form (`+042`, `.5`), which is not valid JSON, so a parse step is
  needed anyway; and real-number precision beyond binary64 is not interoperable
  ([RFC 8259 §6](https://datatracker.ietf.org/doc/html/rfc8259#section-6)). Integers, where precision loss is
  observable and common, are kept exact at any size; reals map through `double`. Rejected.
- **Throw on a value invalid for its declared type.** Surfaces producer bugs loudly, but turns one malformed claim into
  a failed token issuance. Rejected; the verbatim string preserves the data.
- **Map by declared type in both directions, in one mapper, never sniffing (chosen).** Deterministic and lossless for
  strings; the decoded identity carries exactly what the signed token said. A consumer that wants a typed date reads the
  claim and parses it with `DateCoercion`, where it has established that the claim is a date.

## Consequences

- Strings, booleans, `null`, objects and integers of any size round-trip losslessly. Reals round-trip through IEEE-754
  `double`.
- A validated `ClaimsIdentity` carries the issuer's strings byte-for-byte, so authorization logic that compares claim
  values sees what the issuer signed, not a re-formatted rendering. A string claim never arrives typed `#dateTime` from
  decoding; code that needs a date from a string claim calls `DateCoercion.TryParseDateTimeOffset` (or
  `TryParseDateTime`) on `Claim.Value`.
- A decoded JSON number is never `#double` unless it has a fraction or an exponent, so code reading integer claims sees
  an integer type.
- Applications write numeric claims in invariant lexical form (`value.ToString(CultureInfo.InvariantCulture)`);
  culture-formatted values such as `"1,000"` are emitted as strings, not coerced.
- A top-level JSON array decodes into one claim per element and re-encodes as an array of the distinct values, so a
  single-element top-level array re-encodes as a scalar, an inherent limit of the flat claims model.
- Re-encoding a decoded identity reproduces its string claims verbatim, so token exchange and re-issuance do not drift
  date-like values.

## References

- `src/NCode.Identity.Jwt/Claims/ClaimValueMapper.cs` (`ToClaim` and `ToJsonValue`)
- `src/NCode.Identity.Jwt/Claims/DefaultClaimsIdentityFactory.cs`, `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs`
- `src/NCode.Identity.Json/DateCoercion.cs`
- [ADR-1123](1123-numeric-date-is-read-strictly-and-range-guarded.md): the strict `NumericDate` rules
- [XML Schema Part 2: Datatypes](https://www.w3.org/TR/xmlschema-2/): the `ClaimValueTypes` lexical spaces
- [RFC 7519 §2: `NumericDate`](https://datatracker.ietf.org/doc/html/rfc7519#section-2)
- [RFC 8259 §6: Numbers](https://datatracker.ietf.org/doc/html/rfc8259#section-6)
- [OpenID Connect Core 1.0 §5.1: Standard Claims (`birthdate`)](https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims)
- Issues [#32](https://github.com/NCodeGroup/identity/issues/32) and [#34](https://github.com/NCodeGroup/identity/issues/34)
