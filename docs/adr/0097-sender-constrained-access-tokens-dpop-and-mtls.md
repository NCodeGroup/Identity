# 97. Sender-constrained access tokens (DPoP — RFC 9449, mutual-TLS — RFC 8705)

- **Status:** Accepted
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
  `IProofOfPossessionService.EvaluateTokenRequestAsync`, which reads any `DPoP` proof header and (when the client is
  configured for it) the mutual-TLS certificate, enforces the client's `require_dpop` /
  `tls_client_certificate_bound_access_tokens` policies, and returns a `ProofOfPossessionBinding` (the `token_type` plus
  the `TokenConfirmation`). This is a **service**, not a mediator
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
  thumbprint is computed from the canonical JWK and returned as `jkt`. Signature verification is done with the raw
  `RSA`/`ECDsa` primitives against the JWK's public parameters because the `SecretKey` factory is private-key-oriented;
  the asymmetric algorithm set this accepts is exactly what the JOSE catalog registers (RSA `RS*`/`PS*`, EC `ES*`).

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
  defaults to `false` and is advertised `true` only once a deployment has wired certificate forwarding and opted in.

## Consequences

- Access tokens can now be DPoP- or certificate-bound end to end, satisfying a core FAPI (`F16`) prerequisite and
  removing the single most-recurring code-local TODO theme.
- New public surface: the `IProofOfPossessionService` / `IClientCertificateResolver` / `IDPoPProofReplayCache` /
  `IDPoPNonceService` seams, the `TokenConfirmation` / `ProofOfPossessionBinding` models, the
  `CreateSecurityTokenRequest.Confirmation` input, the `ProofOfPossessionBinding` parameter on
  `ITokenGrantHandler.HandleAsync`, the `MutualTlsForwardingOptions` / `MutualTlsClientCertificateFormat` configuration
  types, the `DPoP` / `ConfirmationMethods` constants, the `invalid_dpop_proof` / `use_dpop_nonce` error codes, and the
  DPoP/mTLS settings.
- **Deliberately scoped as issuance-side.** The following ride this foundation and are tracked as follow-ups in
  [`BACKLOG.md`](../../BACKLOG.md) under `F7`, not implemented here: **resource-server-side** proof/`ath` validation when
  a bound token is presented to a protected resource; **DPoP at the authorization endpoint** via the `dpop_jkt`
  parameter (RFC 9449 §10) for the code-bound case; **refresh-token binding continuity** (persisting `jkt`/`x5t#S256`
  with the grant so a rotated token stays bound to the same key); and `mtls_endpoint_aliases` (RFC 8705 §5) for
  deployments that expose separate mTLS hostnames.
