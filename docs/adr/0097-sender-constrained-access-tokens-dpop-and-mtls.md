# 97. Sender-constrained access tokens (DPoP — RFC 9449, mutual-TLS — RFC 8705)

- **Status:** Accepted (nonce and proof-replay storage amended by
  [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md):
  both record into the atomic, tenant-scoped shared-state port, backed by Valkey for several replicas, rather than an
  `IDistributedCache` the library registers; certificate ingestion amended by
  [ADR-0172](0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md): forwarded formats are registered
  decoders that read the whole presented chain, and behind a proxy the header is the only certificate source;
  certificate binding split in place: advertised support and a client's requirement are separate settings; DPoP binding
  requirement named `require_dpop_bound_access_tokens` and registered as the RFC 9449 §5.2 client metadata)
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Every access token the server issued was a **bearer** token: possession alone authorizes use, so a token leaked from a
log, a proxy, a browser, or a compromised resource server can be replayed by anyone. Modern guidance
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700), the FAPI security profiles — backlog `F16`) steers high-value
deployments toward **sender-constrained** (proof-of-possession) tokens, where the token is cryptographically bound to a
key the legitimate client holds. The two standardized mechanisms are:

- **DPoP** ([RFC 9449](https://www.rfc-editor.org/rfc/rfc9449)) — application-layer. The client signs a per-request
  **proof JWT** (`typ: dpop+jwt`) with a key it generates, embedding the public JWK in the proof header; the server
  binds the issued token to that key via a `cnf` **confirmation** claim (`jkt`, the RFC 7638 JWK thumbprint) and
  advertises `token_type: DPoP`.
- **mutual-TLS client-certificate binding** ([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §3) — transport-layer.
  The token is bound to the client certificate presented on the TLS connection via a `cnf` claim (`x5t#S256`, the
  certificate's SHA-256 thumbprint); the `token_type` stays `Bearer`.

Until now every issuance path hard-coded `token_type = Bearer` with a `// TODO: add support for DPoP` marker, the
token-request pipeline never inspected a proof, and the emitted-claims handlers could not populate `cnf` because no
proof-of-possession subsystem existed.

The hard part is not the cryptography but the **deployment reality** of mTLS: in production TLS almost always terminates
at a reverse proxy, load balancer, or service mesh, so the application never sees the client certificate on its own
connection — it arrives as a **forwarded header** whose name and encoding differ per platform (Envoy/Istio
`X-Forwarded-Client-Cert`, AWS ALB `X-Amzn-Mtls-Clientcert`, nginx `ssl-client-cert`, …), and trusting that header from
anywhere but the trusted proxy is a binding-bypass vulnerability.

## Decision

**Evaluate proof of possession once per token request, behind a single `IProofOfPossessionService` seam, and bind the
issued access token's `token_type` and `cnf` claim uniformly across every grant. Keep certificate ingestion
infrastructure-agnostic behind an `IClientCertificateResolver` seam so any proxy topology is supported by host
configuration rather than issuance logic.**

Concretely:

- **One evaluation, at the token endpoint.** After client authentication and request load, the token endpoint calls
  `IProofOfPossessionService.EvaluateTokenRequestAsync`, which reads any `DPoP` proof header and (when the client
  requires certificate binding) the mutual-TLS certificate, enforces the client's
  `require_dpop_bound_access_tokens` / `require_tls_client_certificate_bound_access_tokens` policies, and returns a
  `ProofOfPossessionBinding` (the `token_type` plus the `TokenConfirmation`). This is a **service**, not a mediator
  command ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)): it is a capability called for an answer.

- **First-class threading, no ambient state.** The binding is passed explicitly to
  `ITokenGrantHandler.HandleAsync`, each grant handler sets the response `token_type` from it and carries its
  `TokenConfirmation` on the `CreateSecurityTokenRequest`, and the access-token payload-claims handler (the designated
  claims seam) reads that confirmation from the `SecurityTokenContext` to emit the `cnf` claim. The binding travels on
  the same request objects that already flow end-to-end rather than through an `OpenIdContext.PropertyBag` side channel,
  so every grant honors the same `token_type`/`cnf` without a new grant being able to forget it.

- **DPoP proof validation (RFC 9449 §4.3).** `DefaultDPoPProofValidator` parses the compact JWS, requires
  `typ: dpop+jwt`, requires `alg` to be an **asymmetric** algorithm in the discoverable
  `dpop_signing_alg_values_supported` set (symmetric and `none` are never accepted), verifies the signature with the
  **embedded public JWK** (which must carry no private material), checks `htm`/`htu` against the request, bounds `iat`
  by `dpop_proof_lifetime`, and rejects replays by `jti` through the `IDPoPProofReplayCache` seam. The RFC 7638
  thumbprint is computed from the canonical JWK and returned as `jkt`. The stateless part of this validation — structure,
  signature, HTTP binding, freshness, and `ath` — is the JOSE `IDPoPProofService` building block, which imports the
  embedded JWK through the registered key types and verifies with the registered JOSE signature algorithms
  ([ADR-0176](0176-dpop-proofs-are-a-jose-building-block.md)).

- **Server nonces (RFC 9449 §8)** are supported through the `IDPoPNonceService` seam (default **off** via
  `dpop_nonce_required`); when required and absent/invalid, the server emits `use_dpop_nonce` with a `DPoP-Nonce`
  response header. The nonce service and the replay cache (`IDPoPProofReplayCache`) both run over the standard
  `IDistributedCache` abstraction. The library registers an **in-memory** `IDistributedCache`
  (`AddDistributedMemoryCache`) so a single node works out of the box; a cluster makes replay detection and nonces
  cross-node simply by registering a shared provider (for example `AddStackExchangeRedisCache`) — our impls bind to
  whatever `IDistributedCache` is in DI, so there is no separate opt-in and `IDistributedCache` is a baseline the
  in-memory default satisfies without imposing Redis. (Unlike subject-metadata caching, which deliberately stays
  per-node — [ADR-0055](0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) — DPoP replay/nonce
  *want* shared state, which is exactly what swapping the `IDistributedCache` provider gives.)

- **mTLS certificate ingestion is a configurable seam in the library.** The default `IClientCertificateResolver` reads
  `HttpContext.Connection.ClientCertificate` (direct-TLS) and, when `MutualTlsForwardingOptions.CertificateHeaderName`
  is configured (bound from the `OpenId:MutualTls` section), the certificate carried by that **trusted** forwarded
  header — decoding the platform's encoding (`Base64Der`, `Pem`, `UrlEncodedPem`, or Envoy `EnvoyXfcc`). It lives in the
  Authentication library that owns the seam (not the composition layer), is configured from app settings with zero host
  code, and keeps the token-binding logic identical for every mechanism. **Trust is the operator's responsibility:**
  only set the header name when the app is reachable exclusively through the proxy that sets it, otherwise a client
  could spoof it.

- **Discovery (secure-by-default, [ADR-0083](0083-secure-by-default-discovery-metadata.md)).**
  `dpop_signing_alg_values_supported` advertises the asymmetric signing set; `tls_client_certificate_bound_access_tokens`
  states that the server supports certificate-bound access tokens (RFC 8705 §3.3). It defaults to `false` and is
  advertised `true` only once a deployment has wired certificate forwarding and opted in.

- **Certificate binding is a client requirement the server advertises support for.** As DPoP separates a client's
  `require_dpop_bound_access_tokens` from the server's advertised `dpop_signing_alg_values_supported`, two settings
  separate the support from the requirement:
  - `tls_client_certificate_bound_access_tokens` is the server's support and keeps the RFC 8705 §3.3 discovery name. It
    applies to tenants, so it is set on the server and on a tenant, merges by `Replace`, defaults to `false`, and is
    discoverable.
  - `require_tls_client_certificate_bound_access_tokens` is a client's requirement. It applies to clients, and a server
    or tenant may set it as a default for every client. It merges by `Or`, so a child cannot lift what a parent
    requires. It defaults to `false` and is not discoverable. Both requirements are named `require_` plus their RFC
    client metadata member.
  - **Issuance binds only on the requirement.** The token endpoint binds the certificate only when the client's
    effective requirement is `true`. Advertised support never forces a client to bind, and a certificate presented only
    for `tls_client_auth` does not bind the token, because binding is opt-in as presenting a DPoP proof is. A client
    that requires binding and presents no certificate is refused with `invalid_request` (RFC 8705 §3.4 leaves that
    answer to the server).
  - **Coherence checks the pair.** `CertificateBoundAccessTokenCoherenceRule`
    ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)) reports a client whose
    requirement is `true` while its effective support is `false`. It covers dynamic registration, the management client
    API, the conformance seed, and a tenant or server settings change. The runtime does not consult the support.
  - **Dynamic registration maps the RFC 8705 §3.4 metadata to the requirement.** The client metadata member
    `tls_client_certificate_bound_access_tokens` sets `require_tls_client_certificate_bound_access_tokens`, and the
    registration response reports the requirement under that member.
  - **Breaking:** the migration `SeparateCertificateBindingRequirement` translates stored documents. A client's value
    moves to the requirement. A server or tenant document whose support is `true` also gets the requirement, because
    that value had required every client below it to bind, and a translation does not loosen a requirement.
    A server configuration value is not translated, because configuration is not stored data: a deployment that sets
    `tls_client_certificate_bound_access_tokens` to `true` under `OpenId:Server:Settings` to make every client bind its
    tokens also sets `require_tls_client_certificate_bound_access_tokens` to `true` there, or those clients receive
    bearer tokens.
  - [ADR-0202](0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md) sets the requirement as a ceiling and
    the support as a default.

- **DPoP binding is a client requirement too.** The server advertises DPoP support through
  `dpop_signing_alg_values_supported`, and a client's requirement is its own setting:
  - `require_dpop_bound_access_tokens` is a client's requirement for DPoP-bound access tokens. It applies to clients,
    and a server or tenant may set it as a default for every client. It merges by `Or`, so a child cannot lift what a
    parent requires. It defaults to `false` and is not discoverable.
  - **Dynamic registration maps the RFC 9449 §5.2 metadata to the requirement.** The client metadata member
    `dpop_bound_access_tokens` sets `require_dpop_bound_access_tokens`, and the registration response reports the
    requirement under that member, as it does for `tls_client_certificate_bound_access_tokens`.
  - **Coherence checks the requirement.** `DPoPBoundAccessTokenCoherenceRule`
    ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)) reports a client whose
    requirement is `true` while its effective `dpop_signing_alg_values_supported` is set and empty. Support is a
    non-empty list of proof signing algorithms, so an empty list means that no DPoP proof can be accepted.
  - **Breaking:** the migration `RenameDPoPRequirement` renames a stored `require_dpop` to
    `require_dpop_bound_access_tokens` in the server, tenant, and client settings documents. Configuration is not
    stored data: a deployment that sets `OpenId:Server:Settings:require_dpop` renames it to
    `OpenId:Server:Settings:require_dpop_bound_access_tokens`, because the server refuses the old name as a setting
    that is not registered ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)). This implements backlog `R65`.

- **Resource-server validation (RFC 9449 §7.1, RFC 8705 §3).** When a bound access token reaches a protected resource
  (the UserInfo endpoint, the management API), `IProofOfPossessionService.ValidateResourceAccessAsync` re-checks the
  presented proof against the token's `cnf` claim: for a `jkt` binding it requires the `DPoP` authentication scheme, a
  single DPoP proof whose `ath` equals the base64url SHA-256 of the access token, and whose key thumbprint matches
  `cnf.jkt`; for an `x5t#S256` binding it re-resolves the client certificate and compares its thumbprint. The resource
  endpoints accept both the `Bearer` and `DPoP` authorization schemes.

- **DPoP at the authorization endpoint (RFC 9449 §10).** The `dpop_jkt` authorization-request parameter pre-registers
  the client's DPoP key with the authorization code; its format (a base64url SHA-256 thumbprint) is validated at the
  authorization endpoint, the token endpoint requires the redeeming proof's `jkt` to match it, and the hybrid
  front-channel access token is bound to it.

- **Refresh-token binding continuity (RFC 9449 §5).** The issuance `cnf` is persisted with the refresh-token grant, so
  a rotated access/refresh token stays bound to the original key and a bound refresh token presented without a matching
  proof is rejected.

- **mTLS endpoint aliases (RFC 8705 §5).** When `mtls_endpoint_alias_authority` is configured, the discovery document
  advertises an `mtls_endpoint_aliases` object whose endpoint URLs are rewritten to the mutual-TLS authority.
  _Superseded in part by [ADR-0195](0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md): the alias is now
  the tenant property `MtlsEndpointAliasAuthority`, and the host strategy routes its host; the setting is removed._

## Consequences

- Access tokens are DPoP- or certificate-bound end to end — from issuance (grant handlers), through persistence
  (refresh-token continuity), to resource-server validation — satisfying a core FAPI (`F16`) prerequisite and removing
  the single most-recurring code-local TODO theme.
- New public surface: the `IProofOfPossessionService` (including `ValidateResourceAccessAsync`) /
  `IClientCertificateResolver` / `IDPoPProofReplayCache` / `IDPoPNonceService` seams, the `TokenConfirmation` /
  `ProofOfPossessionBinding` models, the `CreateSecurityTokenRequest.Confirmation` input, the `ProofOfPossessionBinding`
  parameter on `ITokenGrantHandler.HandleAsync`, the `RefreshTokenGrant.Confirmation` field, the `dpop_jkt`
  authorization-request parameter, the `MutualTlsForwardingOptions` / `MutualTlsClientCertificateFormat` configuration
  types, the `DPoP` / `ConfirmationMethods` constants, the `invalid_dpop_proof` / `use_dpop_nonce` error codes, and the
  DPoP/mTLS settings (including `mtls_endpoint_alias_authority`).
