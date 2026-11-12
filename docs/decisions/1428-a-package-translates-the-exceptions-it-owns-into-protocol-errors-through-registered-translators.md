# 1428. A package translates the exceptions it owns into protocol errors through registered translators

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A tenant that wraps its master keys under a root key it controls
([ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)) can lose access to that key for a
while: the provider is unreachable, or the tenant has disabled the key. The key ring reports the outage as
`OwnerRootKeyUnavailableException`, which the secrets packages own and the protocol endpoints do not know. A
package-owned exception that escapes a protocol endpoint would answer `server_error`, so a client could not tell a
temporary outage that a retry resolves from a fault. An owner root-key outage, for example, surfaces before any request
context exists, because a tenant's secrets are unwrapped as the context is created. The server already answers a
temporary condition with `temporarily_unavailable` and a `Retry-After` header on every protocol endpoint, as the rate
limits and the client secret verifier do
([ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

## Decision

1. **`IOpenIdExceptionTranslator` is an open collection.** It lives in `NCode.Identity.OpenId.Abstractions` and maps an
   exception that a package owns to an `IOpenIdError`. A package registers its translator with `TryAddEnumerable`. The
   first translator that answers wins, and an exception that no translator answers is a `server_error`
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). A package that owns an exception adds a translator
   for it without a change to Core.
2. **Three places translate.**
   - `OpenIdEnvironmentEndpointFilter`, shared by the protocol and the interactive endpoint groups, translates an
     exception thrown while it creates the request context. It uses the platform's `OpenIdEnvironment` from
     `IOpenIdEnvironmentProvider`, because no tenant context exists yet, and it answers the error without an audit
     record.
   - `OpenIdExceptionEndpointFilter` translates an exception that an endpoint throws before it audits the exception and
     picks the exception handler, so an endpoint-specific handler also sees an `OpenIdException`.
   - `DefaultAuthorizationRequestProcessor` translates in its catch-all and delivers the error to the client's redirect
     URI with `state` ([RFC 6749 §4.1.2.1](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.2.1)).
3. **An unavailable owner root key is `temporarily_unavailable`.** `OwnerRootKeyUnavailableExceptionTranslator` lives in
   `NCode.Identity.OpenId.Core`, is registered with `TryAddEnumerable`, and answers `OwnerRootKeyUnavailableException`
   with `temporarily_unavailable`, status `503`, `Retry-After` set to `SecretProtection:OwnerRootKeys:RetryInterval`
   rounded up to whole seconds (at least 1) and `Cache-Control: no-store`. Every protocol endpoint answers this way, as
   the server's other temporary conditions (the rate limits and the client secret verifier) do. The management API keeps
   its own `503` problem response for the same outage (`OwnerRootKeyUnavailableEndpointFilter`,
   [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)).
4. **It is logged at Warning.** `DefaultOpenIdExceptionHandler` logs a `temporarily_unavailable` error at Warning (event
   `TemporarilyUnavailable`), because the tenant resolves the condition and it is not a defect of the server. The key
   ring still logs and audits the outage once per validation.

## Options considered

- **Mapping only at the authorization endpoint.** Rejected: a client of the token, revocation and other endpoints could
  not tell an outage from a fault.
- **A mediator command.** Rejected: the exception-handler command means "handled", not "mapped", so a translator would
  also have to decide whether to stop the pipeline.
- **A hard-coded `switch` in Core.** Rejected: it is closed to the exceptions that other packages own
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Registered translators, the first answer wins.** Chosen: every endpoint translates the same way and a package adds
  its exceptions by registering.

## Consequences

- A client of any protocol endpoint reads the outage of a tenant's root key as `temporarily_unavailable` with the
  interval to wait.
- A client of the authorization endpoint receives the error at its redirect URI with `state`.
- A package that owns an exception adds a translator for it without a change to Core.
- The outage is logged at Warning once per translation, and the key ring audits it once per validation.

## References

- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md),
  [ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)
- Issue [#573](https://github.com/NCodeGroup/identity/issues/573)
