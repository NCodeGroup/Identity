# 0049. OpenID protocol errors are audited once from a single endpoint-filter funnel

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** NCode.Identity maintainers

## Context

An OpenID/OAuth endpoint surfaces a protocol error through one of two paths:

- **Thrown** — a handler throws an `OpenIdException` carrying an `IOpenIdError`, caught by
  `OpenIdExceptionEndpointFilter` and rendered via the registered `IOpenIdExceptionHandler`.
- **Directly returned** — a handler builds an `IOpenIdError` and returns `error.AsHttpResult()`
  (an `OpenIdResult<T>`), or an authorization error returns an `AuthorizationResult` for redirect
  delivery, without throwing.

The events/auditing seam ([ADR-0047](./0047-auditing-and-events-observation-seam.md)) needs to
observe every protocol error exactly once per errored request, with the tenant/correlation/endpoint
envelope, and without sprinkling publish calls across the dozens of error call sites.

Two forces are in tension: call sites have the richest _context_, but a central funnel has the only
reliable _coverage_ and exactly-once guarantee.

## Decision

Publish the error audit event (`openid.error`) from the single `OpenIdExceptionEndpointFilter` that
already wraps the OpenID endpoint group — never from individual error call sites.

- **Both paths flow through the one filter.** The thrown path is audited from the exception
  disposition (plus an _eager_ publish directly from an `OpenIdException`'s error, so the record
  survives even if rendering the response throws). The direct-return path is audited by inspecting
  the result returned from `await next(context)`. The `try`/`catch` branches are mutually exclusive
  per request, so a single `TryPublish` helper fires once.
- **Exactly-once per request** is enforced by a constant `HttpContext.Items` sentinel — the invariant
  is "one audit per errored request" (a terminal HTTP response has exactly one error), not "one per
  error object".
- **Auditing never affects the response.** The publish is wrapped so a recorder failure is logged and
  swallowed.
- **Error-ness of a result is read from the value, not the type.** A non-generic `IOpenIdResult`
  accessor exposes the wrapped `IOpenIdResponse`; the filter treats the result as an error only when
  that response _is_ an `IOpenIdError`. `ISupportOpenIdError` stays a **static type marker** (a type
  that models an optional associated error, such as `AuthorizationResult` and `DefaultOpenIdError`) so
  it remains meaningful in generic `where`-constraints and never falsely tags a successful
  `OpenIdResult<TokenResponse>`.

The rule: **state-changing successes publish at their call site (no funnel exists for a 204); errors
publish at the error funnel (a funnel exists, so exploit it).**

## Options considered

- **Publish at every call site that materializes an error.** Rejected: coverage becomes a manual
  discipline (the first forgotten site is a silent gap), errors that are materialized _and_ thrown
  risk a double publish, and it adds boilerplate to every error path.
- **Make `OpenIdResult<T>` implement `ISupportOpenIdError`.** Rejected: `OpenIdResult<T>` wraps _any_
  response, so a success result would statically advertise itself as error-bearing and pollute any
  `where T : ISupportOpenIdError` filter. Error-ness is a property of the wrapped value, so it is
  exposed through the value (`IOpenIdResult.Response`) instead.
- **Key the dedup sentinel on the error instance (reference/hash).** Rejected: it weakens the
  invariant from "one audit per request" to "one per object", and a hash key could falsely suppress a
  genuinely different error.

## Consequences

- Every protocol error — JSON (`OpenIdResult`) or redirect (`AuthorizationResult`) — is audited from
  one place, verified by `OpenIdExceptionEndpointFilterTests`.
- Error call sites stay boilerplate-free; new error types are covered automatically.
- The audit (`openid.error` event) and the operational exception log (`UnhandledException`) are
  complementary layers for the same incident, not duplicates — one for auditors, one for operators.
- Non-protocol responses that need to carry an error to the funnel must expose it either by being an
  `IOpenIdError` behind `IOpenIdResult`, or by implementing `ISupportOpenIdError`.

## References

- `src/NCode.Identity.OpenId.Authentication/Endpoints/OpenIdExceptionEndpointFilter.cs`
- `src/NCode.Identity.OpenId.Abstractions/Results/IOpenIdResult.cs`,
  `src/NCode.Identity.OpenId.Abstractions/Results/OpenIdResult.cs`
- `src/NCode.Identity.OpenId.Abstractions/Errors/ISupportOpenIdError.cs`
- `src/NCode.Identity.OpenId.Authentication.Abstractions/Auditing/OpenIdErrorEvent.cs`
- [ADR-0047](./0047-auditing-and-events-observation-seam.md) — the events/auditing observation seam
