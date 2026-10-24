# 181. Every issuer of a JWT addressed to a client resolves one public credential selector

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

The server addresses four kinds of JWT to a client: the ID token, the logout token, the UserInfo response, and the JWT
introspection response. Each is protected exactly as the client registered: signed with the algorithm its registration
names, and encrypted to one of its public keys when it registers an encryption algorithm
([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
[ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)). One selector,
`IClientJwtCredentialSelector`, applies that policy for every kind.

`ITokenService` is public so a deployment can replace it, and the ID token is one of its products. A replacement that
cannot reach the selector must either reproduce the policy — and drift from it — or skip it and protect the ID token
more weakly than the client registered. The access token has the same shape, and `IAccessTokenPolicyService` is the
public seam a replacement resolves
([ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)).

## Decision

**The client JWT credential selector is a public seam beside `ITokenService`.** `IClientJwtCredentialSelector`, the
`ClientJwtCredentials` it returns, and the `ClientJwtProtectionSettings` that describe each kind of JWT live in
`NCode.Identity.OpenId.Authentication.Abstractions`, in the `NCode.Identity.OpenId.Authentication.Clients` namespace.

- Every issuer of a JWT addressed to a client resolves the selector. A replacement `ITokenService` protects its ID
  tokens with the credentials the selector returns for `ClientJwtProtectionSettings.IdToken`.
- The default implementation stays internal and is registered only when no other is, so a deployment can extend the
  selection, for example to choose among the client's keys by a further rule. A replacement must not weaken the
  protection the client registered.
- `ClientJwtProtectionSettings` is an open description, not a closed list
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): `IdToken`, `UserInfo`, and `Introspection` are
  the server's kinds, and a package that addresses a further kind of JWT to a client describes its own settings with a
  new instance.

## Options considered

- **Keep the selector internal and document the policy.** Rejected: every replacement reimplements the policy, and the
  copies diverge as the policy changes.
- **Expose only the default implementation.** Rejected: a concrete class fixes the policy's dependencies into the
  public API, and a deployment could not replace the policy without replacing every issuer.

## Consequences

- A replacement `ITokenService` protects ID tokens exactly as the default does without depending on internals.
- The three types are public API: changing their shape is a breaking change once the family ships.

## References

- [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) (extended: the selector is public),
  [ADR-0158](0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md) (the access-token
  counterpart), [ADR-0168](0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md),
  [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md).
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientJwtCredentialSelector`, `ClientJwtCredentials`,
  `ClientJwtProtectionSettings` (Authentication.Abstractions); `DefaultClientJwtCredentialSelector` (Authentication).
