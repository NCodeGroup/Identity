# 1276. The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The protocol client ([ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md))
and the backend-for-frontend ([design](../backend-for-frontend.md), §4.1) are relying parties: they speak the OAuth 2.0
and OpenID Connect wire protocol, but they are not part of an OpenID Provider. The design forbids them from depending on
the OP's packages.

The protocol's names (parameter names, error codes, grant types, discovery members, media types) are constants, and
`OpenIdConstants` in `NCode.Identity.OpenId.Abstractions` is the OP's home for constants. That package pulls in
persistence, settings, ASP.NET Core, mediator and property-bag abstractions, so a relying party cannot reference it.
A relying party that cannot reach the constants repeats the names as string literals or as constant classes of its own,
and each name then exists in more than one copy (issue #353).

## Decision

**The wire vocabulary is defined once, in a package that depends on nothing but the runtime. The OP, the protocol client
and the BFF all read it from there.**

1. **The package.** `NCode.Identity.OpenId.Protocol.Abstractions` holds the static partial class `OpenIdProtocol` in the
   namespace `NCode.Identity.OpenId`, the namespace of `OpenIdConstants`. The package has no project reference and no
   framework reference. It follows the family's contract-only packages, such as `NCode.Identity.Jose.Abstractions`, and
   implementation packages depend on it
   ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
2. **Wire vocabulary only.** `OpenIdProtocol` holds only the names that cross the wire: `ParameterSeparatorChar`,
   `ParameterSeparatorString`, `PushedAuthorizationRequestUriPrefix`, and the groups `Parameters`, `ErrorCodes`,
   `GrantTypes`, `ResponseTypes`, `ResponseModes`, `CodeChallengeMethods`, `TokenTypes`, `TokenTypeIdentifiers`,
   `TokenTypeHints`, `ClientAuthenticationMethods`, `ClientAssertionTypes`, `PromptTypes`, `DisplayTypes`, `ScopeTypes`,
   `SubjectTypes`, `BackchannelTokenDeliveryModes`, `DPoP`, `ConfirmationMethods`, `MediaTypes` (with
   `FormUrlEncoded`, so all media types sit in one group), `ClaimTypes`, `AuthenticationMethods`,
   `AuthenticationContextClassReferences`, `MetadataNames`, `WellKnownPaths` and `BackChannelLogout`.
3. **`OpenIdConstants` holds only the OP's own names:** `EndpointNames`, `EndpointPaths`, `EndpointTags`,
   `TenantStrategyCodes`, `ContinueCodes`, `PersistedGrantTypes`, `SecurityTokenTypes`, the `RefreshToken*Policy` groups,
   `ClientCertificateRevocationMode`, `SecurityProfiles`, `TenantEnvironmentTypes`, `AuthenticationPropertyItems`,
   `AuthenticationContextClassReferencePrefixes`, `ClaimsByScope`, `ProtocolClaims`, `DefaultSubjectClaimTypes`,
   `RedirectUriKinds`, `SystemResourceServerIdentifiers` and `InternalParameters`. `InternalParameters` holds the
   `$`-prefixed parameters that never cross the wire.
4. **Claim names come from `JoseClaimNames`.** A claim name is JOSE vocabulary, so `OpenIdProtocol` does not duplicate
   it: the back-channel logout `events` claim is `JoseClaimNames.Payload.Events` (RFC 8417), and `OpenIdProtocol` keeps
   only the event name.
5. **One copy of each discovery name.** The OP's endpoint names and discoverable setting names are defined from the
   matching `OpenIdProtocol.MetadataNames` constant. The client-registration `jwks_uri` setting is a different concept
   with the same spelling and is not aliased.
6. **The constants close no vocabulary.** They name well-known members of open vocabularies, and a consumer or sibling
   package still registers its own grant types, client-authentication methods and the rest
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
7. **A test enforces the rule.** `ProtocolPackageDependencyTests` in `NCode.Identity.OpenId.Client.Tests` fails when the
   package gains a reference or when a client package references an OP package.

The placement rule: a wire name goes to `OpenIdProtocol`, and a name only the server uses goes to `OpenIdConstants`.

## Options considered

- **Keep the names in `OpenIdConstants`.** It pulls persistence, settings and ASP.NET Core into every relying party,
  which the design forbids. It loses.
- **Repeat the names as literals in the client.** Each name then has several copies that drift apart. It loses.
- **A dependency-free `OpenIdProtocol` package that the OP and the relying parties share** gives each name one copy and
  keeps relying parties free of OP packages. It wins.

## Consequences

- Each protocol name has one copy, and a call site names the group it needs from the class that owns it.
- The client and the BFF carry no protocol literals, and they reference no OP package.
- A new wire name goes to `OpenIdProtocol`, a server-only name to `OpenIdConstants`, and the dependency test fails when
  either rule is broken at the package level.
- The package stays a leaf: any need for a reference is a smell that a name belongs elsewhere.

## References

- `src/NCode.Identity.OpenId.Protocol.Abstractions` (`OpenIdProtocol`) and `OpenIdConstants` in
  `NCode.Identity.OpenId.Abstractions`.
- `tests/NCode.Identity.OpenId.Client.Tests/ProtocolPackageDependencyTests.cs`.
- Issue #353; RFC 8417.
- [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) and
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md).
