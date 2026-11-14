# 1511. The hosts send no Server header

- **Status:** Accepted
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

Kestrel adds `Server: Kestrel` to every response by default. Naming the server software helps an anonymous caller
fingerprint the stack, and the
[OWASP HTTP Headers Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Headers_Cheat_Sheet.html)
recommends that a response not name it (issue [#808](https://github.com/NCodeGroup/identity/issues/808)).

Two facts shape where the setting lives:

- **The hosts compose their hardening from `NCode.Identity.AspNetCore`.** Forwarded-header trust and strict transport
  security come from one general package, and each host calls the same registrations
  ([ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package.md)).
- **Each host has one `ConfigureKestrel` call, and `ConfigureHttpsDefaults` keeps only its last action.** That call is
  about TLS
  ([ADR-1499](1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md)), so a second setting placed in it
  shares a block with the TLS configuration.

## Decision

**Both reference hosts send no `Server` header in any environment.** `AddServerHeaderSuppression()` in
`NCode.Identity.AspNetCore` sets `KestrelServerOptions.AddServerHeader` to `false` through an options action. Options
actions compose, so each host calls it beside `AddStrictTransportSecurity`, and its `ConfigureKestrel` call stays about
TLS. Any host composed from the family calls it the same way.

## Options considered

- **Set `AddServerHeader` in each host's `ConfigureKestrel`.** Rejected: every host repeats the setting, and the
  setting is mixed into the TLS block.
- **Strip the header at the ingress or the proxy.** Rejected: a host reached directly, in development or behind another
  proxy, still names itself.

## Consequences

- No response from either host carries a `Server` header. A proxy in front of a host may add its own.
- The setting holds in every environment, so Development responses match Production responses.
- The integration test asserts the option on the built host.

## References

- Issue [#808](https://github.com/NCodeGroup/identity/issues/808)
- [OWASP HTTP Headers Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/HTTP_Headers_Cheat_Sheet.html)
- [ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package.md),
  [ADR-1499](1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md)
- `src/NCode.Identity.AspNetCore/ServerHeaderRegistration.cs`
