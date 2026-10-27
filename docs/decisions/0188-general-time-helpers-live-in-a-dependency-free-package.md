# 188. General time helpers live in a dependency-free package

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

`NCode.Identity.Jose.Abstractions` held three extension classes with real method bodies, in namespace
`NCode.Identity.Jose.Extensions`:

- `DateTimeExtensions.WithPrecisionInSeconds()`
- `DateTimeOffsetExtensions.WithPrecisionInSeconds()`
- `TimeProviderExtensions.GetUtcNowWithPrecisionInSeconds()` and `GetTimestampWithPrecisionInSeconds()`

They truncate an instant to whole seconds, so a time the server stores equals the RFC 7519 `NumericDate` it
round-trips through a JWT. An `*.Abstractions` package holds contracts only and no logic, so they did not belong there.
[#29](https://github.com/NCodeGroup/Identity/issues/29) moved the JOSE dictionary accessor to `NCode.Json` for the same reason, but these helpers are not about JSON.

Their consumers are two implementation packages: `NCode.Identity.Jwt` (the lifetime and certificate-lifetime handlers
and the encoder) and `NCode.Identity.OpenId.Authentication` (about 25 grant, session, and endpoint handlers). Neither
references the other, so the helpers need a home both can reference.

## Decision

- **A new dependency-free `NCode.Time` package holds general time helpers**, in namespace `NCode.Time`. It is the time
  counterpart of `NCode.Json` ([ADR-0055](./0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md)):
  it references nothing but the base class library, so any package in the family can use it.
- **The three helpers move unchanged.** Their names and behavior stay the same; only the namespace and the package
  change. `NCode.Identity.Jwt` and `NCode.Identity.OpenId.Authentication` reference `NCode.Time`.
- **The tests move to `NCode.Time.Tests`.** They use fixed instants rather than the wall clock, so they never depend on
  the millisecond a test runs at.

## Options considered

- **Move the helpers to `NCode.Json`.** Rejected. `NCode.Json` is the family's JSON helper home, and a whole-second
  `TimeProvider` reading is not JSON.
- **Move the helpers to `NCode.Identity.Jose`.** Rejected. `NCode.Identity.OpenId.Authentication` would have to
  reference another implementation package, which [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md)
  forbids.
- **Inline the truncation at each call site.** Rejected. About thirty call sites would repeat the tick arithmetic, and
  one that drifted would store a time that no longer matches its token.

## Consequences

- `NCode.Identity.Jose.Abstractions` holds no logic for times and no `Extensions` folder.
- A consumer that used `NCode.Identity.Jose.Extensions` switches to `using NCode.Time;` and references `NCode.Time`.
  The family is pre-release, so the move is a clean cutover with no forwarding types.
- The family publishes one more package. Any later general time helper goes to `NCode.Time` too.

## References

- `src/NCode.Time/`
- `tests/NCode.Time.Tests/`
- [ADR-0016](./0016-implementation-packages-depend-only-on-abstractions.md)
- [ADR-0055](./0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md)
- Issues [#29](https://github.com/NCodeGroup/Identity/issues/29) and [#31](https://github.com/NCodeGroup/Identity/issues/31)
