# 122. JWT claim values map to and from JSON by declared type, in one symmetric mapper

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode.Identity maintainers

## Context

A `Claim` carries its value as a string plus a value type — an XML Schema type URI from `ClaimValueTypes`
(`#integer32`, `#integer64`, `#uinteger64`, `#integer`, `#double`, `#boolean`, `#string`, `#dateTime`, …) or a
`JsonClaimValueTypes` marker (`JSON`, `JSON_ARRAY`, `JSON_NULL`). A JWT payload carries JSON values. The JWT library
converts in both directions: encoding a subject's claims into a payload, and decoding a validated payload into a
`ClaimsIdentity`. Decoded identities are routinely re-encoded (token exchange, re-issuance, refresh), so the two
directions form a round trip whose fidelity matters.

Two conversions that live apart drift apart, and each drift is a silent data defect:

- A decoder that probes number widths narrow → wide, letting each later probe overwrite the earlier one, types every
  JSON number as `#double` (any finite number satisfies the `double` probe). Re-encoding then goes through `double` and
  loses integers above 2^53 (`9007199254740993` → `9007199254740992`) — identifiers, counters, and large timestamps.
- An encoder that knows only some declared types emits the rest as strings: an `#uinteger32` / `#uinteger64` claim
  becomes a JSON string, and an `#integer` (xsd:integer, unbounded) claim that does not fit 32 bits becomes a string.
- `JSON_NULL` encoded as the empty string turns `null` into `""` on every round trip.
- Values JSON cannot represent or that are malformed for their type make the encoder throw instead of degrade: a
  `#double` of `NaN`/`Infinity` fails serialization, and a malformed `JSON` value fails parsing or serializes an
  undefined element.
- Lenient numeric parsing (`NumberStyles.Any`) accepts thousands separators, currency symbols, and parenthesized
  negatives — none of which are in the XML Schema lexical space the value type declares — so `"1,000"` typed
  `#integer64` silently becomes `1000`.

## Decision

**One internal `ClaimValueMapper` owns both directions of the claim-value mapping, and each direction is driven by the
value's declared type.**

- **Both directions live together.** `ClaimValueMapper.ToClaim` (JSON → `Claim`) and `ClaimValueMapper.ToJsonValue`
  (`Claim` → JSON) sit side by side so their symmetry is visible and tested as a pair; the encoder and the
  claims-identity factory only call them.
- **Decode keeps the exact JSON text and records the most specific type.** Strings, numbers, objects, and arrays keep
  their exact text as `Claim.Value`. A string is always `#string` — never re-typed by its shape
  ([ADR-0121](0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md)). A number takes the **first**
  matching type, most specific first — `#integer32`, then `#integer64`, then `#uinteger64`, then the unbounded
  `#integer` for any larger integer literal, then `#double` for a fraction or exponent — mirroring
  `ObjectJsonConverter`'s most-specific integer mapping. `null` is `JSON_NULL`, objects `JSON`, arrays `JSON_ARRAY`,
  booleans `#boolean`.
- **Encode parses the value in its declared type's lexical space.** Integers parse with `NumberStyles.Integer` and reals
  with `NumberStyles.Float` (the XML Schema lexical spaces), invariant culture. `#integer32` → `int`, `#integer64` →
  `long`, `#uinteger32` → `uint`, `#uinteger64` → `ulong`; `#integer` → the narrowest of `long` / `ulong`, and beyond
  64 bits an exact JSON number written from the canonical literal. `#double` → a **finite** `double`. `#dateTime` →
  `DateCoercion` (ADR-0121). `JSON` / `JSON_ARRAY` → the parsed JSON value when it is well formed **and** of the
  declared kind (object / array). `JSON_NULL` → JSON `null`.
- **A value outside its declared type degrades to its verbatim string.** Malformed, out-of-range, or unrepresentable
  values — and unknown value types — are emitted as the claim's string value. Encoding never throws on claim content
  and never guesses a different type.

## Options considered

- **Fix each defect in place, keeping the two directions in the encoder and the factory.** Smallest diff, but the
  drift that produced the defects is structural: two halves of one bijection in two files, neither tested against the
  other. Rejected in favor of one mapper with a round-trip test.
- **Preserve every number's literal text on encode (emit the claim value as a raw JSON number).** Exact for reals
  beyond `double` precision, but the claim value of an application-created claim is in XML Schema lexical form
  (`+042`, `.5`), which is not valid JSON, so a parse step is needed anyway; and real-number precision beyond binary64
  is not interoperable ([RFC 8259 §6](https://datatracker.ietf.org/doc/html/rfc8259#section-6)). Integers — where
  precision loss is observable and common — are kept exact at any size; reals map through `double`.
- **Throw on a value invalid for its declared type.** Surfaces producer bugs loudly, but turns one malformed claim into
  a failed token issuance and diverges from the date rule (ADR-0121). Rejected; the verbatim string preserves the data.

## Consequences

- Decode → encode is lossless for strings, booleans, `null`, objects, and integers of any size; reals round-trip
  through IEEE-754 `double` (`1.5` → `1.5`; a literal such as `1e3` re-encodes as `1000`). A round-trip test over every
  JSON scalar kind guards the pair.
- A decoded JSON number is never `#double` unless it has a fraction or exponent, so code reading integer claims sees an
  integer type.
- Application code that creates numeric claims must use the XML Schema lexical form (`value.ToString(CultureInfo.InvariantCulture)`);
  culture-formatted values such as `"1,000"` are emitted as strings, not coerced.
- Top-level JSON arrays still decode into one claim per element and re-encode as an array of the distinct values; a
  single-element array therefore re-encodes as a scalar — an inherent limit of the flat claims model, outside this
  mapping.

## References

- `src/NCode.Identity.Jwt/Claims/ClaimValueMapper.cs`
- `src/NCode.Identity.Jwt/Claims/DefaultClaimsIdentityFactory.cs`, `src/NCode.Identity.Jwt/Encoding/DefaultJwtEncoder.cs`
- [ADR-0121](0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md) — the date rule
- [XML Schema Part 2: Datatypes](https://www.w3.org/TR/xmlschema-2/) — the `ClaimValueTypes` lexical spaces
- [RFC 8259 §6 — Numbers](https://datatracker.ietf.org/doc/html/rfc8259#section-6)
- Issue [#34](https://github.com/NCodeGroup/Identity/issues/34)
