# 75. Runtime consumption of resource-server secrets and settings at access-token issuance

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode.Identity maintainers

## Context

[ADR-0066](0066-resource-server-settings-and-secret-owner.md) made resource servers (APIs, in Auth0
terms) first-class **settings and secret owners** — but delivered **management (CRUD) only**. It
explicitly deferred two follow-ups, both tracked as backlog `U7`:

1. **Runtime consumption** — the stored per-audience secrets (notably a JWE key so tokens minted for an
   audience are readable only by it) were never used to actually encrypt access tokens. Resource servers
   had no runtime domain object and were resolved at token-issuance time only to compute the `aud` claim
   ([ADR-0027](0027-access-token-audience-from-resource-servers.md) /
   `DefaultClientScopeService.ResolveAudiencesAsync`).
2. **Auditing** — the resource-server management handler emitted no audit events. This ADR is about (1);
   (2) is a uniform retrofit of the existing `IManagementAuditRecorder` pattern and needs no ADR.

Today `DefaultTokenService.CreateAccessTokenAsync` selects signing and encryption credentials from the
**tenant's** secret collection (`openIdTenant.SecretsProvider.Collection`) using the **client's**
effective access-token algorithm settings. An access token's audience, however, is resolved from its
effective scopes and can bind to **multiple** resource servers at once.

ADR-0066 made a resource server a secret owner that _can_ hold an HS256 shared signing secret as well as
a JWE key, raising the question of whether the OP should ever **sign** an access token with a resource
server's own key. The settled answer here is **no**: access tokens are always signed with the tenant's
asymmetric key. This is the modern industry standard — **RFC 9068 (JWT Profile for OAuth 2.0 Access
Tokens)** contemplates only asymmetric AS signing validated through JWKS, and the **FAPI** security
profiles go further by permitting only `PS256`/`ES256` and **forbidding** symmetric (`HS256`) signing
outright. Letting a resource server hold a token-signing key also lets it forge its own tokens and has no
JWKS rotation story. The one piece of per-audience material that _does_ belong to the audience boundary
is **encryption**: encrypting an access token to a specific resource server's key so only it can read the
token cannot be expressed as "encrypt to the tenant."

The remaining design question is therefore only about **encryption**: a compact JWE encrypts to exactly
one recipient, so how should a per-audience encryption key be applied when a token can target more than
one resource server?

## Decision

Introduce a runtime resolution of resource servers at access-token issuance, but consume their stored
material **for encryption only** — signing is always the tenant's asymmetric key (RFC 9068; FAPI
`PS256`/`ES256`).

Concretely:

- A new seam `IResourceServerCredentialResolver` (abstraction in
  `NCode.Identity.OpenId.Authentication.Abstractions`, default implementation in
  `NCode.Identity.OpenId.Authentication`) resolves, for the effective scopes of a token request, **all**
  enabled resource servers whose scopes intersect those scopes. For each it materializes a disposable
  runtime object carrying the resource server's **effective settings** (server → tenant → resource-server
  merge, mirroring the management `effective-settings` view), and — **only when exactly one** resource
  server is targeted — a disposable `ISecretKeyCollectionProvider` built from that resource server's
  persisted secrets. The resolution exposes the ordered set of audiences and the single resolved resource
  server.
- `DefaultTokenService.CreateAccessTokenAsync` consumes the resolution:
    - **Signing** is always the tenant's asymmetric key over the client's effective access-token signing
      settings — unchanged from before, for every token regardless of how many resource servers it
      targets. Resource servers are never token-signing-key owners.
    - **Encryption**, when a resource server's effective settings require it:
        - **Exactly one** targeted resource server → encrypt preferring that resource server's own key so
          only it can read the token, falling back to the tenant's key.
        - **Zero or multiple** targeted resource servers → the client's effective settings over the
          tenant's keys (no per-audience encryption).
    - **Downgrade guard** — if the token targets multiple resource servers and **any** of them _requires_
      access-token encryption (`AccessTokenEncryptionRequired` in its effective settings), the request is
      rejected rather than issued with weaker protection, because a compact JWE cannot encrypt to multiple
      recipients.

The resolver and its single resolved resource server are `IAsyncDisposable`; the token service disposes
the resolution after issuance so the transient per-request secret-key provider (and its key material) is
released.

Per-audience **encryption** is an opt-in capability: it applies only when a resource server's effective
settings require encryption and it owns a suitable key. The mainstream confidentiality mechanism for this
family remains **opaque tokens + introspection** ([RFC 7662](https://www.rfc-editor.org/rfc/rfc7662)),
which already exists; JWE access tokens are the self-contained alternative for callers that want one.

## Consequences

- Stored per-audience **JWE** keys are consumed: a single-audience token can be encrypted so only the
  target resource server can read it, without touching the signing path. An HS256 secret stored on a
  resource server is **not** used for token signing — signing stays on the tenant's asymmetric key for
  every token — keeping the platform RFC 9068- and FAPI-aligned.
- The "one resource server ⇒ its encryption key; otherwise tenant" rule keeps the semantics unambiguous
  and avoids the impossible "encrypt to many recipients" case. The encryption-required downgrade guard
  turns a silent weakening into an explicit rejection.
- A new per-request resolution reads the tenant's resource servers (already loaded, with settings and
  secrets, by `IResourceServerStore.GetPageAsync`) under an explicit ambient tenant scope
  ([ADR-0018](0018-tenant-scoped-data-access-at-the-persistence-layer.md)), mirroring the existing
  audience-resolution read. Credential material is deserialized per request and disposed after issuance.
- FAPI readiness is advanced on the signing axis (asymmetric-only). The larger FAPI items —
  sender-constrained tokens (DPoP / mTLS), PAR, and JAR/JARM — are a separate epic tracked in
  [`BACKLOG.md`](../../BACKLOG.md).
- New public surface: `IResourceServerCredentialResolver` and its resolution/result models. Greenfield,
  so `PublicAPI.Unshipped.txt` churn is expected ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
