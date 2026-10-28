# 277. A whole-number JSON value is accepted with a zero fraction

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

JSON has one number type ([RFC 8259 §6](https://datatracker.ietf.org/doc/html/rfc8259#section-6)): the texts `60`,
`60.0` and `6e1` are one value, and how a sender writes it is the sender's choice. A pushed authorization response
([RFC 9126 §2.2](https://datatracker.ietf.org/doc/html/rfc9126#section-2.2)) and a token response
([RFC 6749 §5.1](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1)) both send `expires_in`, a lifetime in
seconds, as a JSON number, so a provider may write a lifetime of 60 seconds as `60.0`.

`JsonElement.TryGetInt32` and the serializer's `int` handling accept only the integer form. A provider that writes
`60.0` or `6e1` therefore fails a successful push, which the protocol client reports as an `OpenIdProviderException`,
and fails the whole token request when `TokenResponse.ExpiresIn` (an `int?`) is deserialized, although the value is a
whole number of seconds. The client must still not invent a lifetime the provider never stated: a fraction such as
`60.5`, a string and a number outside the `int` range are not a count of seconds it can use.

## Decision

**A whole number a remote party sends is read through one shared reader in `NCode.Json`: any JSON number that holds a
whole value in `int` range is accepted, and everything else is refused.**

- **Non-throwing readers.** `JsonElement.TryGetWholeInt32(out int)` and `TryGetPropertyWholeInt32(name, out int)`
  mirror `TryGetNumericDate` and `TryGetPropertyNumericDate`
  ([ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md)). The reader checks `ValueKind` first, reads the
  integer form with `TryGetInt32`, and otherwise accepts a value that `TryGetDecimal` reads, that
  `decimal.Truncate` leaves unchanged, and that lies within `int.MinValue` and `int.MaxValue`. It never throws.
- **`decimal`, not `double`.** `60.0000000000000001` parses exactly as a `decimal` and is refused, whereas a `double`
  rounds it to 60.
- **What is refused.** A non-zero fraction, a value outside the `int` range, a JSON string (`"60"`) and any non-number
  kind. The `JsonElement` readers return `false`. A value below `decimal` precision (`1e-400`) reads as 0: the pushed
  authorization response still rejects 0, and a zero token lifetime only makes the caller treat the token as expired.
- **A public converter.** `WholeInt32JsonConverter : JsonConverter<int>` applies the same rule to a serialized member
  and writes a JSON integer; a value the reader refuses fails with a `JsonException`, which `DefaultTokenClient` wraps
  in an `OpenIdProviderException`. It is public in `NCode.Json` rather than internal in
  `NCode.Identity.OpenId.Client.Abstractions`: an `*.Abstractions` package holds no helper logic
  ([ADR-0188](0188-general-time-helpers-live-in-a-dependency-free-package.md)), and production assemblies never use
  cross-assembly `InternalsVisibleTo` ([ADR-0004](0004-no-cross-assembly-internalsvisibleto.md)).
- **Call sites.** The pushed authorization response's `expires_in` is read with `TryGetPropertyWholeInt32` and must
  still be positive
  ([ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md)).
  `TokenResponse.ExpiresIn` stays an `int?` read through `WholeInt32JsonConverter`; its positivity is not validated.
  `NCode.Identity.OpenId.Client` and `NCode.Identity.OpenId.Client.Abstractions` both reference `NCode.Json`, and
  neither holds a copy of the rule.

## Options considered

- **Round or truncate a fraction.** Invents a lifetime the provider never stated. Rejected.
- **`TryGetDouble` for the whole-value test.** Binary rounding accepts `60.0000000000000001` as 60. Rejected in favor
  of `decimal`.
- **Accept numeric strings such as `"60"`.** The RFCs specify a JSON number, and
  [ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md) refuses a string for a `NumericDate` too.
  Rejected.
- **Widen `TryGetValue<int>`.** Silently changes the result for every existing caller. Rejected.
- **A private reader in each client package, or an internal converter in `NCode.Identity.OpenId.Client.Abstractions`.**
  Two copies of one rule, and an `*.Abstractions` package holds no helper logic
  ([ADR-0188](0188-general-time-helpers-live-in-a-dependency-free-package.md)). Rejected.

## Consequences

- A provider that writes `expires_in` as `60.0` or `6e1` works, for both a pushed authorization response and a token
  response.
- A value that is not a whole number in `int` range fails as it did before: the pushed authorization response is
  rejected with an `OpenIdProviderException`, and the token response fails deserialization.
- Any whole number a remote party sends is read through these readers, never through `JsonElement.TryGetInt32` or the
  serializer's default `int` handling; the production coding conventions say so.
- `NCode.Identity.OpenId.Client.Abstractions` gains a project reference to `NCode.Json` and a converter attribute, and
  no logic.

## References

- `src/NCode.Json/JsonElements.cs`, `src/NCode.Json/WholeInt32JsonConverter.cs`
- `src/NCode.Identity.OpenId.Client/Authorization/DefaultAuthorizationRequestClient.cs`
- `src/NCode.Identity.OpenId.Client.Abstractions/TokenResponse.cs`
- [RFC 8259 §6 — Numbers](https://datatracker.ietf.org/doc/html/rfc8259#section-6)
- [RFC 9126 §2.2 — Successful Response](https://datatracker.ietf.org/doc/html/rfc9126#section-2.2)
- [RFC 6749 §5.1 — Successful Response](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1)
- [ADR-0004](0004-no-cross-assembly-internalsvisibleto.md) — no cross-assembly `InternalsVisibleTo`
- [ADR-0123](0123-numeric-date-is-read-strictly-and-range-guarded.md) — the `NumericDate` readers this mirrors
- [ADR-0188](0188-general-time-helpers-live-in-a-dependency-free-package.md) — an `*.Abstractions` package holds no
  logic
- [ADR-0275](0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) — the pushed
  authorization request this amends
- Issue [#352](https://github.com/NCodeGroup/Identity/issues/352)
