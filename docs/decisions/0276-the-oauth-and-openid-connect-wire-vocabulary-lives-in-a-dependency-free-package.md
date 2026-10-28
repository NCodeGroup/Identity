# 276. The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package

- **Status:** Accepted
- **Date:** 2026-10-27
- **Deciders:** NCode Group

## Context

The protocol client ([ADR-0270](0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md))
and the backend-for-frontend ([design](../backend-for-frontend.md), §4.1) are relying parties: they speak the OAuth 2.0
and OpenID Connect wire protocol, but they are not part of an OpenID Provider. The design forbids them from depending on
the OP's packages.

The protocol's names (parameter names, error codes, grant types, discovery members, media types) were defined as
constants in `OpenIdConstants`, which sits in `NCode.Identity.OpenId.Abstractions`. That package pulls in persistence,
settings, ASP.NET Core, mediator and property-bag abstractions, so the client could not reference it. The client
therefore repeated the names as string literals and as partial constant classes of its own, and each name existed in
more than one copy (issue #353).

## Decision

**The wire vocabulary is defined once, in a package that depends on nothing but the runtime. The OP and the client both
read it from there.**

1. **The package.** `NCode.Identity.OpenId.Protocol.Abstractions` holds the static partial class `OpenIdProtocol` in the
   namespace `NCode.Identity.OpenId`, the namespace of `OpenIdConstants`, so a call site changes only the class name. The
   package has no project reference and no framework reference. It follows the family's contract-only packages, such as
   `NCode.Identity.Jose.Abstractions`, and implementation packages depend on it
   ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)).
2. **Wire vocabulary only.** `OpenIdProtocol` holds the names that cross the wire: `ParameterSeparatorChar`,
   `ParameterSeparatorString`, `PushedAuthorizationRequestUriPrefix`, and the groups `Parameters`, `ErrorCodes`,
   `GrantTypes`, `ResponseTypes`, `ResponseModes`, `CodeChallengeMethods`, `TokenTypes`, `TokenTypeIdentifiers`,
   `TokenTypeHints`, `ClientAuthenticationMethods`, `ClientAssertionTypes`, `PromptTypes`, `DisplayTypes`, `ScopeTypes`,
   `SubjectTypes`, `BackchannelTokenDeliveryModes`, `DPoP`, `ConfirmationMethods`, `MediaTypes`, `ClaimTypes`,
   `AuthenticationMethods`, `AuthenticationContextClassReferences`, `MetadataNames`, `WellKnownPaths` and
   `BackChannelLogout`. `OpenIdConstants.ContentType` becomes `MediaTypes.FormUrlEncoded`, so all media types sit in one
   group. A `partial` class cannot span assemblies, which is why the class has a new name.
3. **`OpenIdConstants` keeps the OP-only names:** `EndpointNames`, `EndpointPaths`, `EndpointTags`,
   `TenantStrategyCodes`, `ContinueCodes`, `PersistedGrantTypes`, `SecurityTokenTypes`, the `RefreshToken*Policy` groups,
   `ClientCertificateRevocationMode`, `SecurityProfiles`, `AuthenticationPropertyItems`,
   `AuthenticationContextClassReferencePrefixes`, `ClaimsByScope`, `ProtocolClaims`, `DefaultSubjectClaimTypes`,
   `RedirectUriKinds`, `SystemResourceServerIdentifiers` and `InternalParameters`. `InternalParameters` receives the
   three `$`-prefixed parameters that never cross the wire.
4. **Claim names come from `JoseClaimNames`.** A claim name is JOSE vocabulary, so it is not duplicated here: the
   back-channel logout `events` claim becomes `JoseClaimNames.Payload.Events` (RFC 8417), and `OpenIdProtocol` keeps only
   the event name.
5. **One copy of each discovery name.** The OP's endpoint names and discoverable setting names that are discovery members
   are defined from the matching `OpenIdProtocol.MetadataNames` constant, with the same value. The client-registration
   `jwks_uri` setting is not aliased: it is a different concept with the same spelling.
6. **The constants do not close the vocabularies.** They name the well-known members of open vocabularies, and a
   consumer or sibling package still registers its own grant types, client-authentication methods and the rest
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
7. **A test enforces the rule.** `ProtocolPackageDependencyTests` in `NCode.Identity.OpenId.Client.Tests` fails when the
   package gains a reference, or when the client packages reference an OP package.

Every wire value stays byte-identical; only where the constants live changes.

## Consequences

- Each protocol name has one copy. A new wire name goes to `OpenIdProtocol`, and a name only the server uses goes to
  `OpenIdConstants`.
- The client and the BFF no longer carry protocol literals, and they reference no OP package.
- This is a breaking rename for consumers of the moved groups. It is accepted under the pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)), with a clean cutover and no forwarding members.
- CSharpier can wrap a qualified name across lines, splitting `OpenIdConstants` from `.Group`. A mechanical rename of
  these constants must therefore match across whitespace, or it leaves the wrapped references behind. This was a lesson
  of this change.
