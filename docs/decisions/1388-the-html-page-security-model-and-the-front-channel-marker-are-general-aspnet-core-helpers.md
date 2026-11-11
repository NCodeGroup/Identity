# 1388. The HTML page security model and the front-channel marker are general ASP.NET Core helpers

- **Status:** Accepted (amends
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md),
  [ADR-1109](1109-fapi-behavioral-rules-and-discovery-validation-details.md) and
  [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md))
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

[ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
decision 1 places the HTML page security model and its writer in `NCode.Identity.OpenId.Authentication.Abstractions`,
namespace `NCode.Identity.OpenId.Authentication.Endpoints`: `ContentSecurityPolicyDirective`,
`ContentSecurityPolicyDirectives`, `ContentSecurityPolicySources`, `HtmlPageSecurityPolicy` and
`HtmlPageSecurityResponseExtensions.SetHtmlPageSecurity`. Its context gives the reason: the packages that write pages
share only Abstractions ([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)).
[ADR-1109](1109-fapi-behavioral-rules-and-discovery-validation-details.md)'s 2026-10-21 amendment puts the front-channel
marker in the same package: `IOpenIdEndpointFrontChannelMetadata`, `OpenIdEndpointFrontChannelMetadata` and
`WithOpenIdFrontChannel()`, which keep the CORS middleware off an endpoint the user agent navigates to and refuse a
CORS preflight to it.

These types hold method bodies: the policy's validation and serialization, the header writer, the nonce and hash
sources, and the request-delegate wrapper that refuses a preflight. An `*.Abstractions` package holds contracts and
data only (issue [#513](https://github.com/NCodeGroup/Identity/issues/513)). None of them names an identity concept: a
Content-Security-Policy and a page the user agent navigates to are HTTP notions that any ASP.NET Core application has.

[ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md) makes `NCode.Identity.AspNetCore` the home of
general ASP.NET Core helpers that name no identity concept, and says it references only the ASP.NET Core shared
framework. `ContentSecurityPolicySources.Sha256()` hashes the UTF-8 bytes of a script, and the strict-encoding rule
([ADR-1266](1266-text-is-encoded-and-decoded-only-through-strict-encodings.md)) requires `SecureEncoding.UTF8` from
`NCode.Buffers` for that.

## Decision

- **The HTML page security model and its writer live in `NCode.Identity.AspNetCore`, namespace `NCode.Identity.AspNetCore`.** This
  replaces the package and namespace in ADR-1256 decision 1; the model, its grammar, the headers and every page's
  policy are unchanged. Every package that writes a page references `NCode.Identity.AspNetCore`, which references no identity
  package, so the reason ADR-1256 gives for one shared model still holds.
- **`SetHtmlPageSecurity` writes `Cache-Control` and `Pragma` through `SetNoStore()`.** ADR-1256 decision 3's headers
  and values are unchanged; one writer owns the uncacheable headers
  ([ADR-1345](1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)), so a page and a
  secret-revealing response cannot drift apart.
- **The front-channel marker is general endpoint metadata in `NCode.Identity.AspNetCore`.** `IOpenIdEndpointFrontChannelMetadata`
  and `OpenIdEndpointFrontChannelMetadata` are `IFrontChannelEndpointMetadata` and `FrontChannelEndpointMetadata`;
  `OpenIdFrontChannelEndpointConventionBuilderExtensions` is `FrontChannelEndpointConventionBuilderExtensions`, and its
  `WithOpenIdFrontChannel()` and `Endpoint.IsOpenIdFrontChannel` are `WithFrontChannel()` and `Endpoint.IsFrontChannel`.
  The behavior ADR-1109's amendment describes, the CORS branch and the refused preflight, is unchanged, and the
  interactive bundle's asset route keeps the marker as
  [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md) decision
  1 says, through `WithFrontChannel()`. The `OpenId` prefix is dropped because the marker names no OpenID concept.
- **`NCode.Identity.AspNetCore` may reference `NCode.Buffers`.** This amends ADR-1355's "references only the ASP.NET Core shared
  framework": the package references the shared framework and the family's foundational `NCode.Buffers`, for the
  strict encodings, as `NCode.Identity.Json` does. It still references no identity package, which is what lets every package in
  the family reference it.
- **The tests move to `NCode.Identity.AspNetCore.Tests`.**

## Options considered

- **Keep the types in `NCode.Identity.OpenId.Authentication.Abstractions`.** Rejected. An Abstractions package holds
  contracts and data only, and these types hold logic.
- **Move them to `NCode.Identity.OpenId.Authentication`.** Rejected. `NCode.Identity.OpenId.Authentication.Interactive`
  writes pages and marks front-channel endpoints, and
  [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md) forbids it to reference another
  implementation package.
- **Keep `NCode.Identity.AspNetCore` free of `NCode.Buffers` and encode with `Encoding.UTF8`.** Rejected. It breaks the
  strict-encoding rule ([ADR-1266](1266-text-is-encoded-and-decoded-only-through-strict-encodings.md)) for a hash that
  must match the script the browser runs.
- **Keep the `OpenId` names.** Rejected. A general helper that carries an OpenID prefix tells a reader it is part of the
  protocol, and the package holds no OpenID concept.

## Consequences

- A consumer references `NCode.Identity.AspNetCore`, adds `using NCode.Identity.AspNetCore;` and calls `WithFrontChannel()`. The family
  is pre-release ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)), so the move is a clean cutover with
  no forwarding types.
- A package that references `NCode.Identity.AspNetCore` also references `NCode.Buffers`, which most of the family's packages
  already reference.
- [ADR-1382](1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)
  rejects building the BFF's policy from `HtmlPageSecurityPolicy` partly because the model lived in a package the BFF
  does not reference. That reason no longer holds; its other reason, that a host writes its policy as one string in
  configuration, does, so that decision stands.

## References

- `src/NCode.Identity.AspNetCore/`
- `tests/NCode.Identity.AspNetCore.Tests/`
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)
- [ADR-1109](1109-fapi-behavioral-rules-and-discovery-validation-details.md)
- [ADR-1254](1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md)
- [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)
- [ADR-1266](1266-text-is-encoded-and-decoded-only-through-strict-encodings.md)
- [ADR-1345](1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)
- [ADR-1355](1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md)
- [ADR-1382](1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)
- Issue [#513](https://github.com/NCodeGroup/Identity/issues/513)
