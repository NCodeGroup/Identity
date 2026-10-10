# 56. `NCode.Identity.Abstractions` is split into framework-free, correctly-tiered packages

- **Status:** Accepted
- **Date:** 2026-10-09
- **Deciders:** NCode Group

## Context

`NCode.Identity.Abstractions` had grown to mix three dependency tiers in one assembly, surfaced by the
[ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) deep-dive:

- **Pure BCL primitives** — `Claims`, `Logic/ICryptoService`, the encoding/hash enums, `Models/TimePeriod`,
  `IsActiveResult*`, `ISupportClone`, `IdentityLibrary`. These need nothing beyond the base class library (plus the
  `NCode.Registration` marker).
- **A self-contained Settings subsystem** — the 16 `NCode.Identity.Settings.*` types, which depend on
  `System.Text.Json` and `NCode.Collections.Providers`.
- **An ASP.NET Core HTTP surface** — `Models/UriDescriptor`, `Exceptions/HttpResultException`, and the `Results/*`
  contracts. These are the **sole** reason the package carried the `Microsoft.AspNetCore.App` `FrameworkReference`.

Because the `FrameworkReference` sat on the lowest common abstractions package, it dragged ASP.NET Core into everything
beneath `NCode.Identity.*` and blocked the standalone crypto branch (`NCode.Identity.Jose(.Abstractions)`,
`NCode.Identity.Secrets(.Abstractions)`) from ever referencing the core without inverting the layering. That same
layering wall forced the JOSE branch to keep its own `JsonElement` helper (`JsonElementExtensions`) separate from the
Identity branch's helper, leaving two homes for the same utility ([`BACKLOG`](../../BACKLOG.md) items R2/R4).

## Decision

Factor the package along its three tiers. Namespaces are unchanged — only assembly/package boundaries move — so the
cutover is `.csproj` + `PublicAPI` mechanics with no `using` churn in consumers:

- **`NCode.Identity.AspNetCore.Abstractions`** (new) owns the ASP.NET Core HTTP surface (`UriDescriptor`,
  `HttpResultException`, `Results/*`) and carries the `Microsoft.AspNetCore.App` `FrameworkReference`.
- **`NCode.Identity.Settings.Abstractions`** (new) owns the Settings subsystem and its
  `NCode.Collections.Providers.Abstractions` dependency.
- **`NCode.Identity.Abstractions`** (slimmed) keeps only the framework-free BCL primitives. It drops the
  `FrameworkReference`, `NCode.Json`, and `NCode.Collections.Providers.Abstractions`, and is now referenceable by
  anything — including the crypto branch.
- The JOSE `JsonElement` helper is folded into the dependency-free **`NCode.Json`** package: `TryGetPropertyValue<T>`
  now lives on `NCode.Json.JsonElements` alongside `IsNullOrUndefined` / `OrEmptyObject` / `EmptyObject`, giving one
  universal home (resolves R2). Detached parsing standardizes on `JsonElement.Parse(string)` at the convertible call
  sites (resolves R3).

The `NCode.Registration.AspNetCore` project reference, previously carried by the core and relied on **transitively** by
`NCode.Identity.OpenId.Abstractions` (whose `IOpenIdExceptionHandler` returns a `ReadOnlyEndpointDisposition`), moves to
that hub where it is genuinely consumed — it was not actually unused, only mis-located.

## Consequences

- The crypto branch and any future low-level package can reference `NCode.Identity.Abstractions` without inheriting
  ASP.NET Core; the `FrameworkReference` is now scoped to the packages that genuinely need HTTP types.
- Consumers that use Settings or the HTTP surface pick up the new packages transitively through
  `NCode.Identity.OpenId.Abstractions`; the few direct consumers outside that tree (`NCode.Identity`, the abstractions
  test project) reference them directly.
- One JSON helper home (`NCode.Json`) instead of two; the bespoke `NCode.Identity.Jose.Extensions.JsonElementExtensions`
  is removed.
- Expected pre-release churn: new packages, moved `PublicAPI.Unshipped.txt` entries, and a clean cutover with no shims,
  consistent with the [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) posture.
