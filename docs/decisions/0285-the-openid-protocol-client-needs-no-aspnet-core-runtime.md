# 285. The OpenID protocol client needs no ASP.NET Core runtime

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) D1 gives the
implementation package, `NCode.Identity.OpenId.Client`, the `Microsoft.AspNetCore.App` framework reference. The
reference supplies `StatusCodes` and the `Microsoft.Extensions.*` abstractions (dependency injection, `HttpClient`
factory, logging, options and primitives).

The client does no ASP.NET Core work. The reference buys a constants class and a query-string helper, yet it makes every
console app or worker that uses the client install the ASP.NET Core runtime. The
[backend-for-frontend design](../backend-for-frontend.md) (§4.1) calls the client useful to any server-side client, and
[ADR-0056](0056-abstractions-split-into-framework-free-tiered-packages.md) scopes the framework
reference to the packages that genuinely need HTTP types (issue
[#321](https://github.com/NCodeGroup/Identity/issues/321)).

## Decision

The protocol client is a plain .NET library: no package of it references an ASP.NET Core shared framework.

- `NCode.Identity.OpenId.Client` drops the `Microsoft.AspNetCore.App` framework reference. Its transitive closure is
  already framework-free (`.Client.Abstractions`, `Jwt.Abstractions`, `SharedState.Abstractions`, `NCode.Json`,
  `Jose.Abstractions`, `Protocol.Abstractions`, `Secrets.Abstractions`, `NCode.Registration`, `Jwk`).
- It references five `Microsoft.Extensions.*` packages directly, exactly the ones its `using`s name:
  `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Http`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` and `Microsoft.Extensions.Primitives`.
- Status codes are the internal named `int` constants `ProviderStatusCodes.Status200OK` and `.Status201Created` in
  `NCode.Identity.OpenId.Client.Http`, named as ASP.NET Core names them.
- Authorization-request query parameters are percent-encoded with `Uri.EscapeDataString` (RFC 3986: everything but
  `A-Z a-z 0-9 - . _ ~`), appended with `?` or, when the endpoint already has a query, `&`. Against
  `QueryHelpers` the only difference is that `! * ( ) @` are percent-encoded too; every decoded value is identical.
- `ProtocolPackageDependencyTests` guards the rule: the client assembly references no `Microsoft.AspNetCore.*`
  assembly, and the test host's runtimeconfig lists no `Microsoft.AspNetCore.App`.

The rule: a package of the protocol client references no ASP.NET Core shared framework. Later phases (DPoP, mTLS) keep to
the BCL and `Microsoft.Extensions.*`.

## Options considered

- **Keep the framework reference.** Rejected: it forces the ASP.NET Core runtime onto every non-web consumer for a
  constants class and a query helper.
- **Cast `HttpStatusCode` members to `int` (`(int)HttpStatusCode.OK`).** Rejected: the review of the client asked for
  named constants, not enum casts.
- **Type `ProviderHttpResponse.StatusCode` as `HttpStatusCode`.** Rejected: the public `ProtocolError.StatusCode` is an
  `int`, so the value would cast straight back.

## Consequences

- A console app or worker that uses the client needs only `Microsoft.NETCore.App`. An ASP.NET Core host such as the
  backend-for-frontend resolves the same `Microsoft.Extensions.*` assemblies from its shared framework.
- The framework cannot return unnoticed. Restoring with the reference back fails with NU1510, because the five direct
  `Microsoft.Extensions.*` references would no longer be pruned, so the build itself refuses it; the guard test fails
  too.
- The only behavior change is that `! * ( ) @` in authorization-request query values are percent-encoded; a provider
  decodes them to the same values.
- The guard test reads the runtimeconfig with `File.ReadAllText(path, SecureEncoding.UTF8)`, because the banned-API
  analyzer ([ADR-0266](0266-text-is-encoded-and-decoded-only-through-strict-encodings.md)) forbids the encoding-less overload.
- The "no `ConfigureAwait(false)`" rule still holds: the client's callers are server-side hosts without a
  `SynchronizationContext`.

## References

- [ADR-0056](0056-abstractions-split-into-framework-free-tiered-packages.md)
- [ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)
- [`docs/backend-for-frontend.md`](../backend-for-frontend.md)
- [`src/NCode.Identity.OpenId.Client/`](../../src/NCode.Identity.OpenId.Client/)
