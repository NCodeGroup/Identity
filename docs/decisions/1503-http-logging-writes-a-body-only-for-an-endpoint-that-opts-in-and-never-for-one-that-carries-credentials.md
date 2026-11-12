# 1503. HTTP logging writes a body only for an endpoint that opts in, and never for one that carries credentials

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)

## Context

HTTP logging writes what `HttpLoggingOptions.LoggingFields` selects, at Information under
`Microsoft.AspNetCore.HttpLogging`, and `HttpLoggingFields.All` selects both bodies. An OP's bodies carry client
secrets, `client_assertion` JWTs, authorization codes, access, refresh and logout tokens, passwords (the interactive
sign-in and password pages, the password grant, the management API's local-account endpoints), client secrets the
management API returns once, and the claims of userinfo and the BFF's user endpoint.

- **Headers and the query string carry credentials too.** `Authorization`, `Proxy-Authorization`, `Cookie`, `DPoP`,
  `Set-Cookie`, a `Location` that carries a code, and the `code` and `id_token_hint` parameters. The framework logs
  allow-listed headers only, and its default response allow-list includes `Location`.
- **A log level is an operator's diagnostic dial.** The chart passes it through `extraEnv`, so a protection that holds
  only while the level stays at `Warning` is none.
- **The endpoint's code knows whether a body is safe to log.** The code that maps an endpoint knows what its bodies
  carry. The middleware sees endpoint metadata only after routing.

## Decision

**HTTP logging writes request and response properties, allow-listed headers and the duration, and a body only for an
endpoint that opts in to it in code; an endpoint whose bodies carry credentials can never opt in, and no setting
enables a body.**

1. **Permitted fields.** `AddGuardedHttpLogging()` (`NCode.Identity.AspNetCore`) sets
   `RequestPropertiesAndHeaders | ResponsePropertiesAndHeaders | Duration`, removes `Location` from the response-header
   allow-list and keeps the framework's request-header allow-list.
2. **An endpoint opts in, in code.** `WithRequestBodyLogging()` and `WithResponseBodyLogging()` opt an endpoint or route
   group in. A registered `IHttpLoggingInterceptor` gives each request the permitted fields plus exactly the bodies its
   endpoint opts into, and strips every other field, including what `[HttpLogging]` or `WithHttpLogging(...)` enables.
   A request that selects no endpoint logs no body.
3. **A credential-bearing endpoint can never opt in.** `WithConfidentialBodies()` marks an endpoint or route group. The
   interceptor logs no body for it, and an endpoint with both the mark and an opt-in throws `InvalidOperationException`
   naming it when its endpoints are built. The family marks the OpenID protocol group, the management API's root group,
   the interactive surface group and every BFF frontend's endpoints, so a later endpoint inherits the mark. Discovery
   and JWKS are public and lose nothing.
4. **The options are validated in every environment.** The `HttpLoggingOptions` validator refuses, when the host
   starts, a body, query or trailer field and an allow-listed `Authorization`, `Proxy-Authorization`, `Cookie`, `DPoP`,
   `Set-Cookie` or `Location`. `AddProductionSafety` adds no check, because a Production-only refusal leaves
   Development and Staging logs, which collectors ship too, open, and the opt-in is reviewed code rather than an
   operator's setting.
5. **Routing runs first.** A host calls `UseHttpLogging()` right after `UseRouting()`.

## Options considered

- **Development-only body logging behind a configuration section that `AddProductionSafety` refuses in Production.**
  Rejected: it decides per environment, not per endpoint, a Development host logs passwords, and Production is one
  setting away.
- **The framework's own endpoint metadata as the opt-in.** Rejected: it enables any field, the query string included,
  on any endpoint, a credential-bearing one too.
- **Marking each credential-bearing endpoint.** Rejected: a missed or later endpoint could opt in. Marking route groups
  fails safe.

## Consequences

- Raising any log level writes no body, query string or credential header.
- A host opts its own endpoint in, in code.
- A configured body field or credential header stops the host with a message naming each finding.
- A host's own interceptor registered later could enable a field again. That is host code, which this decision does not
  police.

## References

- Issue [#778](https://github.com/NCodeGroup/identity/issues/778)
- [ADR-1248](1248-the-host-image-writes-one-line-json-logs.md)
- `src/NCode.Identity.AspNetCore/GuardedHttpLoggingRegistration.cs`
- `src/NCode.Identity.AspNetCore/HttpBodyLoggingEndpointConventionBuilderExtensions.cs`
