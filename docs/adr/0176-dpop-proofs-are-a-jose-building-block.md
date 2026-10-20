# 176. DPoP proofs are a JOSE building block

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

DPoP (RFC 9449) binds an access token to a key the client holds. Each request carries a proof: a compact `dpop+jwt`
JWS whose protected header embeds the public `jwk` it is signed with, and whose claims bind it to one HTTP request
(`htm`, `htu`), one moment (`iat`), one identity (`jti`), optionally one access token (`ath`), and optionally one
server nonce (`nonce`). The token's `cnf.jkt` is the RFC 7638 thumbprint of that `jwk`.

[ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) put proof validation inside the OpenId
authentication stack. `DefaultDPoPProofValidator` split the compact JWS by hand and verified the signature with raw
`RSA`/`ECDsa` primitives built from the JWK's public parameters. That was a second cryptographic code path next to the
JOSE library's. It supported only the key types it spelled out, RSA and EC, so an Ed25519 proof failed even though the
JOSE catalog registers `EdDSA` ([ADR-0117](0117-eddsa-clean-room-implementation-opt-in-package.md)). Nothing could create a
proof, so a client, a test, or a sibling package had to build one by hand. The DPoP vocabulary (`dpop+jwt`, `htm`,
`htu`, `ath`) was declared twice, once in JOSE and once in `OpenIdConstants.DPoP`.

## Decision

- **JOSE owns the proof.** `NCode.Identity.Jose.Abstractions` declares `IDPoPProofService` with two operations, and
  `AddJoseLibrary()` registers the default implementation:
  - `CreateProof(DPoPProofCreationOptions)` signs a proof with an asymmetric `JoseSigningCredentials`. The header is
    `typ: dpop+jwt`, `alg`, and the key's exported public `jwk`, with no `kid`. The claims are `htm`, `htu` (query and
    fragment removed), a random 128-bit `jti`, `iat` from `TimeProvider`, and `ath` and `nonce` when given.
  - `TryValidateProof(string, DPoPProofValidationParameters, out DPoPProof?, out string?)` returns a `DPoPProof`
    (`Algorithm`, `PublicKey`, `JwkThumbprint`, `Jti`, `IssuedAt`, `Nonce`) or an `error_description`-ready reason.
- **Validation covers everything one proof proves on its own**, in this order:
  1. Compact structure, then `typ`. A `crit` header is rejected, because no JWS extension is understood.
  2. `alg` must be in the caller's allow-list **and** resolve to a JOSE signature algorithm whose key type is
     asymmetric. A MAC or `none` is never accepted, whatever the allow-list says.
  3. The `jwk` must carry no private material. It is imported through the registered `ISecretKeyTypeCatalog`
     descriptor for its `kty`. The algorithm's key type and legal key sizes must both fit the imported key.
  4. The signature is checked with the algorithm's own `Verify`, after an exact signature-size check.
  5. `htm` must equal the method. `htu` must equal the request URI after both drop their query and fragment and both
     are absolute `http`/`https` URIs; on Unix a rooted path such as `/token` would otherwise parse as a `file:` URI.
     `ath` is checked when an access token is given. `jti` must be present. `iat` must lie within ±`Lifetime` of now.
- **Stateful checks stay with the caller.** Replay detection and the server-nonce challenge need state (a replay cache,
  a nonce service) and an HTTP response. `DefaultDPoPProofValidator` in the OpenId stack passes the client's settings
  in (`dpop_signing_alg_values_supported`, `dpop_proof_lifetime`), maps a rejection to `invalid_dpop_proof`, and then
  runs the nonce challenge (`use_dpop_nonce` with a fresh `DPoP-Nonce`) and records `(jkt, jti)` in
  `IDPoPProofReplayCache`.
- **One vocabulary.** `JoseClaimNames.Payload.Htm`, `.Htu`, and `.Ath` sit beside the existing `JoseTokenTypes.DPoPJwt`.
  `OpenIdConstants.DPoP` keeps only the HTTP header names (`DPoP`, `DPoP-Nonce`), which are an OpenId transport
  concern.

## Consequences

- Every asymmetric key type and signature algorithm the JOSE stack registers is a DPoP key type and algorithm, with no
  DPoP-specific code. `EdDSA` proofs validate once the opt-in EdDSA package is registered and the algorithm is in the
  allow-list.
- A client library, a resource server, or a test creates proofs through the same service that validates them.
- `AddJoseLibrary()` registers `TimeProvider.System` when no `TimeProvider` is registered, and DPoP key import resolves
  the Secrets library's key-type descriptors, which need logging.
- **Breaking:** `OpenIdConstants.DPoP.ProofJwtType`, `.HttpMethodClaim`, `.HttpUriClaim`, and `.AccessTokenHashClaim`
  are removed in favor of `JoseTokenTypes.DPoPJwt` and `JoseClaimNames.Payload.Htm`/`.Htu`/`.Ath`.
- This supersedes the signature-verification paragraph of
  [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md). Its binding, `cnf`, nonce, and replay
  decisions stand.
