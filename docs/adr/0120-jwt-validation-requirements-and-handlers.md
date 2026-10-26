# 120. JWT validation is modeled as ASP.NET Core-style requirements and handlers

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode.Identity maintainers

## Context

Issue [#28](https://github.com/NCodeGroup/Identity/issues/28) asked to move the JSON Web Token (JWT) service contracts into their `.Abstractions` package so
Abstractions-only consumers (notably the OpenID authentication handlers) could depend on contracts rather than the
implementation — the same split the Jose and OpenID families already have.

A naive reading — move `IJsonWebTokenService` plus its DTOs (`DecodedJwt`, the validate/encode parameter and result
types) into `.Abstractions` — is **low value**. The reason is the validation configuration surface: built-in validators
were expressed as a **fluent DSL** (`ValidateIssuer` / `ValidateAudience` / `ValidateTokenType` /
`ValidateTokenLifeTime` / `ValidateClaim`) whose methods built imperative closures (frozen sets, media-type
normalization, lifetime/certificate arithmetic, exception throwing). That is _logic_, and logic must not live in an
`*.Abstractions` package. So the DSL would have stayed in the implementation package, and every consumer that
_configures_ validation — which is every OpenID authentication handler — would still have referenced the implementation.
Moving only the DTOs would relocate types without delivering the decoupling the split exists for.

Two further forces shaped the design:

1. **External code must be able to contribute its own validators.** A consumer or sibling package needs to add a
   reusable, data-driven validation rule without forking the library.
2. **The house rule stands: `*.Abstractions` packages hold contracts only — no logic.** Whatever makes validation
   configurable from Abstractions must itself be free of behavior.

## Decision

**Model JWT validation on ASP.NET Core authorization** — pure-data _requirements_ interpreted by _handlers_ that vote
on a shared _context_ — and segregate the service along its two independent responsibilities.

- **Segregate the service.** `IJsonWebTokenService` is split into `IJwtEncoder` (`Encode`) and `IJwtValidator`
  (`ValidateAsync`), with `DefaultJwtEncoder` / `DefaultJwtValidator` implementations. Usage is disjoint — of the
  OpenID consumers, seven validate only and two encode only, none use both — so interface segregation decouples each
  side's consumers without a combined facade.
- **Requirements are pure data.** Built-in validators become `IJwtValidationRequirement` records — `ClaimValueRequirement`
  (with `ValidateIssuer` / `ValidateAudience` as fluent sugar over it), `TokenTypeRequirement`,
  `TokenLifetimeRequirement`, `CertificateLifetimeRequirement`, and `AssertionRequirement` (the inline escape hatch) —
  carried on the data-only `JwtValidationParameters.Requirements`. The fluent builders only _construct and append_ a
  record, so they carry no logic and live in `.Abstractions`.
- **Handlers are logic, keyed by requirement type.** Each requirement is executed by an `IJwtValidationHandler`
  (`JwtValidationHandler<TRequirement>` filters the pending requirements to its type), resolved from DI as a plain
  collection — the family's strategy-collection pattern ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)).
  Built-in handlers ship in the implementation package; **external code registers its own via
  `AddJwtValidationHandler<T>()`**, satisfying force 1 without forking.
- **Succeed/Fail voting replaces throwing.** Handlers vote on a shared `JwtValidationContext` via `Succeed(requirement)`
  / `Fail(reason)`; the token is valid only when every requirement has succeeded and nothing has failed. The validator
  aggregates failures into a single `JwtValidationException` on the result, so consumers reading `result.IsValid` /
  `result.Exception` are unaffected.
- **Contracts move to `.Abstractions`; logic stays in impl.** The interfaces, DTOs, requirement records, fluent
  builders, exceptions, and `JsonClaimValueTypes` move into `NCode.Identity.Jwt.Abstractions` under purpose namespaces
  (`…Jwt.Encoding` / `…Jwt.Validation` / `…Jwt.Claims`); the evaluator, built-in handlers, key resolver, and claims
  factory stay in `NCode.Identity.Jwt`. `NCode.Identity.OpenId.Authentication` now references only the abstractions
  package.
- **Uniform `Jwt*` naming.** The package pair is renamed `NCode.Identity.JsonWebTokens(.Abstractions)` →
  `NCode.Identity.Jwt(.Abstractions)` (pairing with the sibling `NCode.Identity.Jose`), and the family is renamed
  uniformly (`JwtValidationParameters` / `JwtValidationResult` / `JwtValidationContext`, `JwtEncodingParameters`,
  `JwtLibrary`, `AddJwtLibrary`, `Jwt*Exception`).

## Consequences

- An Abstractions-only consumer can inject `IJwtValidator`, configure validation with the fluent builders, and read the
  result without referencing the implementation package. The implementation is named only by the composition root.
- Validation is declarative and inspectable: a `JwtValidationParameters` is a data description of what must hold, not an
  opaque set of closures. New built-in rules are a requirement record plus a handler; third-party rules are the same,
  registered through DI.
- Bespoke one-off validation remains expressible from Abstractions through `AssertionRequirement` (its delegate operates
  only on the Abstractions `JwtValidationContext`), so the escape hatch needs no implementation reference.
- This was a sizable but mostly mechanical breaking change, acceptable under the greenfield, pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)); `PublicAPI.Unshipped.txt` churn and the package
  rename were expected and preferred over compatibility shims.
- The default claims-identity factory and the default key resolver remain implementation details reached through the
  nullable escape-hatch delegates on `JwtValidationParameters`; when a delegate is null, the validator supplies the
  built-in default.
