# 16. Implementation packages depend only on abstractions; composition wires them

- **Status:** Accepted
- **Date:** 2026-09-29
- **Deciders:** NCode Group

## Context

The family is split into many packages that collaborate at runtime, each shipped as a matched pair: an `*.Abstractions`
package of pure contracts and an implementation package that provides the behavior. When one implementation needs a
capability owned by another (a JOSE service consuming secrets, the OIDC runtime consuming tenant resolution, the
management API consuming the same tenant resolution), the tempting shortcut is a direct project reference from one
implementation package to another.

A direct implementation-to-implementation reference welds the two packages together: a consumer that wants one
capability transitively drags in the other's entire dependency closure, the package boundary stops meaning anything,
and two implementations that should each be independently replaceable can no longer be swapped in isolation. It also
invites the exact divergence the split exists to prevent — two callers reaching a shared behavior by two different
concrete paths.

## Decision

**An implementation package references only `*.Abstractions` packages — never another implementation package.**
Cross-implementation collaboration flows through an abstraction (an interface owned by an `*.Abstractions` package)
resolved from dependency injection. Each implementation package registers its own concrete types from a single
[`DefaultRegistration.cs`](../../src/NCode.Identity.OpenId.Authentication/Tenants/DefaultRegistration.cs)
`extension(IServiceBuilder<TMarker>)` block using `TryAdd*`, and the **composition root** (`AddIdentityServer()`,
built on the [`NCode.Registration`](../../src/NCode.Registration) `IServiceBuilder<TMarker>` builder) is the single
place that references the implementation packages and invokes their registrations to satisfy the abstractions.

The rule applied to a shared capability `Foo` used by two implementations:

- `Foo.Abstractions` owns `IFoo` (and its options/value types). Both consuming implementations reference it.
- `Foo` (implementation) provides `DefaultFoo` and references only abstractions (its own `Foo.Abstractions` plus any
  `*.Persistence.Abstractions` it needs).
- Each consumer takes `IFoo` by constructor injection; **no consumer references the `Foo` implementation package.**
- The composition root references `Foo`, `Foo`'s consumers, and everything else, and wires them together.

## Options considered

- **Direct implementation-to-implementation project reference.** Rejected: transitive dependency bloat, a meaningless
  package boundary, loss of independent replaceability, and a standing invitation for two callers to bind a shared
  behavior through divergent concrete paths.
- **A single shared package with no abstractions split** (implementation and contract together). Rejected: any consumer
  of the contract is forced to also take the implementation and its dependencies — the same coupling in a smaller box —
  and it tempts the very implementation-to-implementation reference this ADR forbids.
- **Abstraction seam + DI composition (chosen).** Consumers depend on the contract only; the concrete type is
  registered once at the composition root via `NCode.Registration`. Each package stays independently buildable,
  testable, and replaceable, and a shared capability has exactly one concrete binding.

## Consequences

- A new cross-cutting capability shared by more than one implementation must ship as an abstraction/implementation
  **pair**; a lone implementation package would tempt a forbidden direct reference.
- Wiring intra-family dependencies lives in the `NCode.Registration` composition, not in project files — the reference
  graph of implementation packages stays a flat fan-out from the composition root rather than a web.
- One extra abstraction package per shared capability, in exchange for package boundaries that hold their meaning and
  implementations that swap independently.
- The rule is mechanically checkable: an implementation `.csproj` whose `ProjectReference` list names a non-`Abstractions`
  package is a violation.

## References

- [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2, §4.
- [ADR-0001](./0001-mediator-vs-dependency-injection-logic-classes.md) — the DI shapes an abstraction seam is wired as.
- [ADR-0004](./0004-no-cross-assembly-internalsvisibleto.md) — the sibling rule that packages collaborate only through
  deliberately-designed public contracts.
- [ADR-0006](./0006-single-canonical-impl-is-defaultfoo.md) — the `DefaultFoo` naming for the concrete type behind a seam.
