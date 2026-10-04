# 6. The single canonical implementation of an interface is named DefaultFoo

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

Most contracts in the family have exactly one built-in implementation that a consumer resolves through DI and never
names directly. A consistent naming rule for that implementation removes a recurring bikeshed and signals intent.

## Decision

**The single canonical implementation of an `IFoo` that a consumer resolves through DI and never names directly is
named `DefaultFoo`** — strip the `I`, prepend `Default` (`IJoseSerializer` → `DefaultJoseSerializer`,
`IJsonWebTokenService` → `DefaultJsonWebTokenService`, `ITokenService` → `DefaultTokenService`). This is the ASP.NET
Core idiom (`DefaultHttpContextFactory`, `DefaultProblemDetailsWriter`): it keeps the bare concept name free for a
consumer's own type, visually separates interface from implementation, and signals a **replaceable default**. The rule
governs the **implementation** — which therefore stays `internal` (production §2) and is reached only through the
interface — so it applies regardless of whether the `IFoo` itself is `public` or `internal`; it does **not** rename a
public type a consumer constructs by name (carve-out 3).

**Three carve-outs, each when the class is _not_ "the one canonical DI impl":**

1. An implementation that is **one of several variants**, or is defined by _how_ it works, is named for what makes it
   specific — the grant handlers (`DefaultAuthorizationCodeGrantHandler`, `DefaultClientCredentialsGrantHandler`,
   `DefaultPasswordGrantHandler`, `DefaultRefreshTokenGrantHandler`) are each a distinct `ITokenGrantHandler` selected
   at runtime, not a single default.
2. An implementation of a **framework** interface follows that framework's own pattern name (a
   `IValidateOptions<TOptions>` validator is `TOptionsValidator`, not `Default*`).
3. A **public, developer-facing data / message / model / result type that a consumer constructs and names directly**
   keeps the bare concept name — `AuthorizationTicket`, `TokenRequest`, `ResourceNode` — even when it implements an
   `IFoo` seam. The `Default*` prefix signals "an internal swap-in you reach through the interface"; a type a consumer
   names by hand is the opposite of that, and a `Default*` prefix would both mislead and steal the clean concept name.

## Options considered

- **`FooImpl` / `FooService` / `ConcreteFoo`.** Rejected: noisier, and `Service` collides with genuine service nouns.
- **Same name as the interface without the `I`.** Rejected: it steals the clean concept name a consumer might want for
  their own replacement, and reads ambiguously at a call site.
- **`DefaultFoo` (chosen).** Matches the platform, signals replaceability, and keeps the concept name free.

## Consequences

- A reader can pair any `IFoo` with its `DefaultFoo` at a glance, and knows a `Default*` type is an internal swap-in
  default reached through the interface.
- The carve-outs keep the rule honest — a `Default*` name is a claim of "the one canonical DI impl", so variants,
  framework adapters, and public types a consumer names by hand deliberately do not use it.

## References

- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §3.
