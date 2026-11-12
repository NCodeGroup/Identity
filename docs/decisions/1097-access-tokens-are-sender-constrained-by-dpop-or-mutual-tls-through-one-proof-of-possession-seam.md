# 1097. Access tokens are sender-constrained by DPoP or mutual TLS through one proof-of-possession seam

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

A bearer access token authorizes whoever holds it, so a token leaked from a log, a proxy, a browser or a compromised
resource server can be replayed by anyone. Current guidance
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700), the FAPI security profiles, issue
[#54](https://github.com/NCodeGroup/identity/issues/54)) steers high-value deployments toward sender-constrained
(proof-of-possession) tokens, which are cryptographically bound to a key the legitimate client holds. Two standardized
mechanisms exist:

- **DPoP** ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449)) works at the application layer. The client signs a
  per-request proof JWT (`typ: dpop+jwt`) with a key it generates and embeds the public JWK in the proof header. The
  server binds the issued token to that key through a `cnf` confirmation claim (`jkt`, the
  [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638) JWK thumbprint) and answers `token_type: DPoP`.
- **Mutual-TLS client-certificate binding** ([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §3) works at the
  transport layer. The token is bound to the client certificate presented on the TLS connection through a `cnf` claim
  (`x5t#S256`, the certificate's SHA-256 thumbprint), and the `token_type` stays `Bearer`.

The hard part is the deployment reality of mutual TLS rather than the cryptography. In production TLS almost always
terminates at a reverse proxy, load balancer or service mesh, so the application never sees the client certificate on its
own connection. The certificate arrives as a forwarded header whose name and encoding differ per platform, and trusting
that header from anywhere but the trusted proxy is a binding-bypass vulnerability.

Every grant must bind its tokens alike, and a new grant must not be able to forget to. A client must be able to require
binding, and a server must be able to say whether it supports it, as two separate facts.

## Decision

**Proof of possession is evaluated once per token request, behind a single `IProofOfPossessionService` seam, and the
issued access token's `token_type` and `cnf` claim are bound uniformly across every grant. Certificate ingestion stays
infrastructure-agnostic behind an `IClientCertificateResolver` seam, so any proxy topology is supported by host
configuration rather than by issuance logic.**

### One evaluation at the token endpoint

After client authentication the token endpoint calls `IProofOfPossessionService.EvaluateTokenRequestAsync` once. It
reads a `DPoP` proof header and, when the client requires certificate binding, the client certificate. It enforces the
client's requirements and returns a `ProofOfPossessionBinding`: the `token_type` (`DPoP` or `Bearer`) plus a
`TokenConfirmation`. It is a service, not a mediator command
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)), because it is
a capability called for an answer.

The binding is passed explicitly to `ITokenGrantHandler.HandleAsync` and carried on
`CreateSecurityTokenRequest.Confirmation`. The access-token payload-claims handler reads the confirmation and emits the
`cnf` claim (`jkt` or `x5t#S256`). The binding travels on the request objects that already flow end to end, never through
ambient state, so every grant binds alike.

### DPoP proofs

`DefaultDPoPProofValidator` validates a proof as [RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) §4.3 requires:

- `typ` is `dpop+jwt`.
- `alg` is an asymmetric algorithm within the discoverable `dpop_signing_alg_values_supported` set. Symmetric algorithms
  and `none` are never accepted.
- The signature verifies with the embedded public JWK, which carries no private material.
- `htm` and `htu` match the request.
- `iat` is within `dpop_proof_lifetime`.
- `jti` is not a replay, checked through `IDPoPProofReplayCache`.

`jkt` is the RFC 7638 thumbprint of the embedded key. The stateless checks are the JOSE `IDPoPProofService` building
block ([ADR-1176](1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md)).

Server nonces ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) §8) use `IDPoPNonceService`. `dpop_nonce_required`
defaults to off. When it is on and the nonce is missing or invalid, the server answers `use_dpop_nonce` with a
`DPoP-Nonce` response header. The replay cache and the nonces record into the atomic shared-state port `IExpiringKeySet`
([ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md)),
so every replica sees them.

### Client certificates

`IClientCertificateResolver`, configured by `MutualTlsForwardingOptions`, reads the certificate that a forwarded header
carries through the registered format decoders
([ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md)). Behind a proxy the header is the
only certificate source. Trust is the operator's responsibility: the operator ensures that only the trusted proxy sets
the header, because otherwise a client could spoof it. The binding logic is identical for every mechanism.

### Support and requirement are separate settings

- `tls_client_certificate_bound_access_tokens` is the server's support ([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705)
  §3.3). It applies to tenants, merges by `Replace`, defaults to `false` and is discoverable.
- `require_tls_client_certificate_bound_access_tokens` is a client's requirement. It applies to clients, and a platform
  or tenant may set it as a default for every client. It merges by `Or`, so a child cannot lift what a parent requires.
  It defaults to `false` and is not discoverable.
- `require_dpop_bound_access_tokens` is a client's DPoP requirement, shaped the same way. DPoP support is a non-empty
  `dpop_signing_alg_values_supported`.
- Dynamic registration maps the client metadata members `tls_client_certificate_bound_access_tokens` (RFC 8705 §3.4) and
  `dpop_bound_access_tokens` ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) §5.2) to the two requirements, and the
  registration response reports each requirement under its member name.
- A certificate binds a token only when the client's requirement is `true`. Advertised support never forces a client to
  bind, and a certificate presented only for `tls_client_auth` does not bind. A client that requires binding and presents
  no certificate is answered `invalid_request`.
- `CertificateBoundAccessTokenCoherenceRule` and `DPoPBoundAccessTokenCoherenceRule`
  ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md))
  refuse a requirement without effective support. The runtime does not consult the support.
  [ADR-1202](1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md) sets the
  FAPI 1.0 Advanced requirement as a ceiling and the support as a default.

### Resource servers

`IProofOfPossessionService.ValidateResourceAccessAsync` re-checks a bound token at the first-party resources (UserInfo
and the management API), which accept the `Bearer` and `DPoP` authorization schemes. A `jkt` binding requires the `DPoP`
scheme and one proof whose `ath` is the base64url SHA-256 of the access token and whose key thumbprint matches
`cnf.jkt`. An `x5t#S256` binding re-resolves the client certificate and compares its thumbprint.

### Authorization endpoint and refresh

The `dpop_jkt` authorization-request parameter binds the authorization code: its format is validated at the
authorization endpoint, and the redeeming proof's `jkt` must match it. It also binds the front-channel access token. The
refresh grant persists `RefreshTokenGrant.Confirmation`, so rotated tokens stay bound to the original key and a proof
that does not match is rejected.

### Discovery and errors

Discovery advertises `mtls_endpoint_aliases` from the tenant's `MtlsEndpointAliasAuthority`
([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)). The error
codes are `invalid_dpop_proof` and `use_dpop_nonce`.

## Options considered

- **Keep bearer tokens.** Rejected: a leaked token is replayable by anyone, and FAPI requires sender-constrained tokens.
- **Offer only DPoP or only mutual TLS.** Rejected: DPoP suits browsers and public deployments, mutual TLS suits
  service-to-service and regulated deployments, and FAPI 1.0 Advanced requires mutual TLS
  ([ADR-1202](1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md)) while
  FAPI 2.0 accepts either.
- **Evaluate binding inside each grant handler.** Rejected: each grant would repeat the evaluation, and a new grant
  could forget it. One evaluation at the token endpoint, passed to every handler, makes the binding uniform.
- **Carry the binding in an ambient property bag.** Rejected: the explicit parameter and request field are visible to
  the compiler and to every reader.
- **Make the binding a mediator command.** Rejected: it is a capability called for an answer, which is a service.
- **One setting for support and requirement.** Rejected: the server's capability and a client's obligation differ, and
  they merge differently (`Replace` against `Or`).
- **Read only the connection's certificate.** Rejected: behind a proxy the application never sees it, so the resolver
  reads the forwarded header the operator trusts.
- **Chosen:** one `IProofOfPossessionService` evaluation per token request, explicit threading of the binding, a
  configurable certificate resolver, and separate support and requirement settings.

## Consequences

- Access tokens are DPoP- or certificate-bound end to end, from issuance through the refresh grant's persistence to
  resource-server validation, which satisfies a core FAPI ([#54](https://github.com/NCodeGroup/identity/issues/54))
  prerequisite.
- The public surface is the `IProofOfPossessionService`, `IClientCertificateResolver`, `IDPoPProofReplayCache` and
  `IDPoPNonceService` seams, the `TokenConfirmation` and `ProofOfPossessionBinding` models, the
  `CreateSecurityTokenRequest.Confirmation` input, the `ProofOfPossessionBinding` parameter of
  `ITokenGrantHandler.HandleAsync`, `RefreshTokenGrant.Confirmation`, `MutualTlsForwardingOptions` and the DPoP and
  mutual-TLS settings.
- A host behind a proxy must ensure that only the trusted proxy sets the certificate header. A misconfigured host that
  lets clients set it can be made to bind a token to a certificate the client does not hold.
- Tests cover proof validation, nonces and replay, both binding mechanisms at every grant, resource-server validation,
  `dpop_jkt`, refresh continuity, dynamic registration and the coherence rules.

## References

- [RFC 9449](https://www.rfc-editor.org/rfc/rfc9449) (DPoP), [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705)
  (mutual TLS), [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638) (JWK thumbprint) and
  [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) (Security BCP).
- Issue [#54](https://github.com/NCodeGroup/identity/issues/54) (FAPI) and issue
  [#256](https://github.com/NCodeGroup/identity/issues/256) (the DPoP requirement setting).
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md),
  [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md),
  [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md),
  [ADR-1172](1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md),
  [ADR-1176](1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md),
  [ADR-1202](1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md) and
  [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md).
