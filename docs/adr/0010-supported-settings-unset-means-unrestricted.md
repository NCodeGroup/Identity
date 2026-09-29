# 0010. `*_supported` settings: unset means unrestricted; a replaceable baseline supplies defaults

- **Status:** Accepted
- **Date:** 2026-09-28
- **Deciders:** NCode.Identity maintainers

## Context

Server, tenant, and client settings are merged into a single effective collection that endpoint logic
reads. Collection-valued `*_supported` settings (`grant_types_supported`, `scopes_supported`,
`response_types_supported`, `response_modes_supported`, `prompt_values_supported`, the various
`*_alg_values_supported`, …) merge with `OnMerge = Intersect`, so the cascade is
`root ∩ server-store ∩ tenant ∩ client` — each scope can only *narrow*. This is the intended
operator-control model: the server operator defines a ceiling and downstream scopes stay within it.

The root ceiling for the capability lists was materialized from **hardcoded descriptor `Default`
values** (e.g. `grant_types_supported = [authorization_code, implicit]`). Because intersect only ever
narrows, a hardcoded root default becomes an immovable ceiling: a tenant or client can never use a
value the library did not bake in, and an extension that adds a new capability (a new grant handler,
a custom scope) is dead on arrival. The frozen literal conflated three separate concerns — what the
server is *capable* of, what the operator *policy* allows, and what a child scope narrows to.

## Decision

For the `*_supported` collection settings, **absence is the sentinel for "no restriction" (⊤)**:

- A `*_supported` collection that is **unset** at the effective scope imposes **no restriction** —
  enforcement reads use "restrict only if present":
  `if (settings.TryGetValue(key, out var allowed) && !allowed.Contains(item)) reject;`
- A `*_supported` collection that **is set** is an explicit ceiling and narrows via `Intersect` as
  before. Operator control is preserved: set a ceiling at server or tenant scope and it clamps every
  scope below it.
- **True capability is enforced by the registries**, not by the literal: an unknown `grant_type` is
  rejected by grant-handler lookup, an unknown algorithm by the algorithm providers. The
  `*_supported` lists are therefore *policy*, decoupled from capability.

The hardcoded policy literals are removed from `SettingDescriptor.Default` and moved into a
**replaceable baseline contributor**, `IDefaultSettingsProvider`, applied in the root settings
`Set` (replace/upsert) pipeline *before* configuration. A host that configures nothing still gets the
standard spec-compliant baseline; a host may extend it (root layer is replace/union, so
`baseline ∪ additions`), override it via `OpenId:Server:Settings:*`, or clear a key to get ⊤.

Scalar settings (lifetimes, booleans such as `require_pkce`) keep their descriptor `Default` — an
unset scalar has no sensible ⊤.

## Options considered

- **Change the merge direction to Union for some collections.** Rejected: a client cannot meaningfully
  "add" a grant type the server has no handler for; union breaks the discovery/security invariant that
  the server advertises the maximum. The fix belongs at the root ceiling, not the merge direction.
- **Derive the root ceiling from registrations (like the algorithm lists already do).** Kept for the
  *discovery advertising* path, but insufficient for *operator policy*: scopes and other arbitrary
  strings have no DI capability to derive from, and enforcement capability is already covered by the
  registries. Absence-as-⊤ subsumes it uniformly.
- **Keep a hardcoded conservative default.** Rejected as the primary mechanism: it is exactly the
  frozen ceiling that blocks extensions. Retained only as an overridable, removable baseline layer.

## Consequences

- Extensions and custom scopes/grants are no longer blocked by library literals; a host opts into a
  capability by registering it and (optionally) listing it, and narrows by setting an explicit ceiling.
- The operator-control model is unchanged and arguably clearer: restriction happens only where a
  ceiling is *explicitly set*.
- Posture shift: when neither the baseline nor any scope sets a `*_supported` list, that dimension is
  **permissive**. The default-on baseline keeps the out-of-the-box server safe and spec-compliant.
- Enforcement reads must use `TryGetValue` (restrict-only-if-present); `GetValue` throws on an unset,
  default-less collection. `DefaultValidateAuthorizationRequestHandler` already followed this pattern;
  the remaining scope/grant/response-mode gates were converted to match.
- Discovery omits unset `*_supported` fields automatically (it iterates only present settings).

## References

- `src/NCode.Identity.OpenId.Authentication.Abstractions/Settings/IDefaultSettingsProvider.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/DefaultSettingsProvider.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/RootSettingsCollectionDataSource.cs`
- `src/NCode.Identity.OpenId.Authentication/Settings/DefaultSettingDescriptorDataSource.cs`
- Enforcement gates: `DefaultSelectTokenGrantHandlerHandler`, `DefaultValidateTokenRequestHandler`,
  `DefaultValidateAuthorizationCodeGrantHandler`, `DefaultValidateRefreshTokenGrantHandler`,
  `DefaultValidateAuthorizationRequestHandler`, `DefaultAuthorizationEndpointHandler`
- ADR-0001 (service vs strategy vs mediator), ADR-0005 (public API surface)
