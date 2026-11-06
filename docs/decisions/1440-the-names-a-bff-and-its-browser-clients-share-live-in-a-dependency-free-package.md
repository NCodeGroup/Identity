# 1440. The names a BFF and its browser clients share live in a dependency-free package

- **Status:** Accepted (amends
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)
  decisions 5 and 6)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

The Backend for Frontend (BFF) and its Blazor WebAssembly client are two halves of one contract. They agree on the
names that cross between them:

- the property names of the `/bff/user` document and the key under which a Blazor server persists that document for
  the client;
- the claim types that carry the logout form's action and guard;
- the paths of the endpoints the BFF maps for each frontend;
- the defaults of the anti-forgery header that same-origin calls carry
  ([ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)).

The WebAssembly client runs in the browser. It cannot reference `NCode.Identity.Bff.Abstractions`, which needs the
ASP.NET Core shared framework, so a name defined there is out of the client's reach. An implementation package keeps
its references to abstractions
([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)), so the server half of Blazor cannot reach
into the client package for the names either.

## Decision

**The names the server and the browser clients share are defined once, in a package that depends on nothing but the
runtime. The server packages and the client package both read them from there.**

1. **The package.** `NCode.Identity.Bff.Protocol.Abstractions` holds its types in the namespace `NCode.Identity.Bff`.
   The package has no project reference and no framework reference, as
   [ADR-1276](1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md) decides for the
   OAuth vocabulary.
2. **Its content.** The package holds `BffDefaults`, `BffClaimTypes`, `BffEndpointPaths` and `BffUserDocumentNames`.
   `BffEndpointPaths` names each endpoint's path relative to the frontend's path prefix, and `BffUserDocumentNames`
   names the `/bff/user` properties and the key a Blazor server persists the document under. The client keeps no copy
   of any of them.
3. **Who references it.** `NCode.Identity.Bff.Abstractions` and `NCode.Identity.Bff.Blazor.Client` reference the
   package. A server call site reads the names through the same namespace as before.
4. **The Blazor packages.** `NCode.Identity.Bff.Blazor` references only `NCode.Identity.Bff.Abstractions` and
   `NCode.Registration`. A host that serves both render modes references both Blazor packages.
5. **Tests enforce it.** `BffPackageDependencyTests` holds the shared package to the runtime and the server packages
   to their abstractions, `BffBlazorPackageDependencyTests` holds the server half of Blazor to the abstraction
   packages, and `BffBlazorClientPackageDependencyTests` holds the client to the shared package and to the
   browser-safe Blazor assemblies.

## Options considered

- **The names in `NCode.Identity.Bff.Abstractions`, with a copy in the client.** Rejected: a wire contract that exists
  in two copies drifts, and a name that differs by one character fails only in a browser.
- **The client references `NCode.Identity.Bff.Abstractions`.** Rejected: the ASP.NET Core shared framework does not run
  in the browser.
- **The server half of Blazor references the client package, as an exception to
  [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md).** Rejected: an implementation package that
  references a sibling implementation package gives up the rule that keeps each package replaceable.
- **A dependency-free package.** Chosen: the one definition is reachable from the server and from the browser, and the
  boundary is checkable by reading an assembly's references.

## Consequences

- Each shared name has one copy. A name both halves read goes to `NCode.Identity.Bff.Protocol.Abstractions`, and a name
  only the server reads stays in `NCode.Identity.Bff.Abstractions`.
- The client compiles the package's constants into its own assembly, so a built client lists no
  `NCode.Identity.` reference beyond the shared package, if it lists one at all. Its dependency test is an allow-list
  and asserts no presence.
- A host that serves interactive server and WebAssembly components references both Blazor packages. Neither package
  needs the other.
- A stray reference in one of these packages fails its dependency test rather than a browser.
- A shared name is part of the wire contract, so its value stays byte-identical across the server and the client.

## References

- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1276](1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md),
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1435](1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md)
- Issue [#605](https://github.com/NCodeGroup/Identity/issues/605)
