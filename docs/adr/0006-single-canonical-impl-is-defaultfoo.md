# 6. The single canonical implementation of an interface is named DefaultFoo

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

Most contracts in the family have exactly one built-in implementation that a consumer resolves through DI and never
names directly. A consistent naming rule for that implementation removes a recurring bikeshed and signals intent.

## Decision

**The single canonical implementation of an `IFoo` is named `DefaultFoo`** — strip the `I`, prepend `Default`
(`IJoseSerializer` → `DefaultJoseSerializer`, `IJsonWebTokenService` → `DefaultJsonWebTokenService`, `ITokenService`
→ `DefaultTokenService`). This is the ASP.NET Core idiom (`DefaultHttpContextFactory`, `DefaultProblemDetailsWriter`):
it keeps the bare concept name free for a consumer's own type, visually separates interface from implementation, and
signals a **replaceable default**. It applies regardless of the interface's visibility.

**Two carve-outs, both when the class is _not_ "the one canonical impl":**

1. An implementation that is **one of several variants**, or is defined by _how_ it works, is named for what makes it
   specific — the grant handlers (`DefaultAuthorizationCodeGrantHandler`, `DefaultClientCredentialsGrantHandler`,
   `DefaultPasswordGrantHandler`, `DefaultRefreshTokenGrantHandler`) are each a distinct `ITokenGrantHandler` selected
   at runtime, not a single default.
2. An implementation of a **framework** interface follows that framework's own pattern name (a
   `IValidateOptions<TOptions>` validator is `TOptionsValidator`, not `Default*`).

## Options considered

- **`FooImpl` / `FooService` / `ConcreteFoo`.** Rejected: noisier, and `Service` collides with genuine service nouns.
- **Same name as the interface without the `I`.** Rejected: it steals the clean concept name a consumer might want for
  their own replacement, and reads ambiguously at a call site.
- **`DefaultFoo` (chosen).** Matches the platform, signals replaceability, and keeps the concept name free.

## Consequences

- A reader can pair any `IFoo` with its `DefaultFoo` at a glance, and knows a `Default*` type is a swap-in default.
- The carve-outs keep the rule honest — a `Default*` name is a claim of "the one canonical impl", so variants and
  framework adapters deliberately do not use it.

## References

- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §3.
