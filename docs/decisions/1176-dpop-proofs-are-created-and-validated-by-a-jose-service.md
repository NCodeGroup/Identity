# 1176. DPoP proofs are created and validated by a JOSE service

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

DPoP (RFC 9449) binds an access token to a key the client holds. Each request carries a proof: a compact `dpop+jwt`
JWS whose protected header embeds the public `jwk` it is signed with, and whose claims bind it to one HTTP request
(`htm`, `htu`), one moment (`iat`), one identity (`jti`), optionally one access token (`ath`), and optionally one
server nonce (`nonce`). The token's `cnf.jkt` is the RFC 7638 thumbprint of that `jwk`.

A client library, a resource server, a test and the OP all create or validate proofs. If each built its own JWS
handling, the family would carry a second cryptographic path beside the JOSE library's, limited to the key types it
spelled out, and a key type or algorithm the JOSE catalog registers would still fail as a DPoP proof. The DPoP
vocabulary (`dpop+jwt`, `htm`, `htu`, `ath`) would also be declared twice.

## Decision

JOSE owns the proof. `IDPoPProofService` in `NCode.Identity.Jose.Abstractions` owns RFC 9449 proofs, and
`AddJoseLibrary()` registers it with `TryAddSingleton`, default `DefaultDPoPProofService`, so every caller creates and
validates proofs through one service.

- `CreateProofAsync(DPoPProofCreationOptions)` signs a proof with an asymmetric `JoseSigningCredentials`, through the
  asynchronous signing of [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md).
  The header is `typ: dpop+jwt`, `alg`, and the key's exported public `jwk`, with no `kid`. The claims are `htm`,
  `htu` (query and fragment removed), a random 128-bit `jti`, `iat` from `TimeProvider`, and `ath` and `nonce` when
  given.
- `TryValidateProof(string, DPoPProofValidationParameters, out DPoPProof?, out string?)` returns a `DPoPProof`
  (`Algorithm`, `PublicKey`, `JwkThumbprint`, `Jti`, `IssuedAt`, `Nonce`) or an `error_description`-ready reason.

Validation covers everything one proof proves on its own, in this order:

1. Compact structure, then `typ`. A `crit` header is refused, because no JWS extension is understood.
2. `alg` must be in the caller's allow-list and resolve to a JOSE signature algorithm with an asymmetric key type. A
   MAC or `none` is never accepted, whatever the allow-list says.
3. The `jwk` carries no private material. It is imported through the `ISecretKeyTypeCatalog` descriptor for its `kty`
   (`TryGetByJwkKeyType`), and the algorithm's key type and legal key sizes must both fit the imported key.
4. The signature is verified by the algorithm's own `Verify`, after an exact signature-size check.
5. `htm` equals the method. `htu` equals the request URI after both drop their query and fragment, and both are
   absolute `http` or `https` URIs; a rooted path such as `/token` on Unix would otherwise parse as a `file:` URI.
   `ath` is checked when an access token is given. `jti` is present. `iat` lies within ±`Lifetime` of now.

Stateful checks stay with the caller, because they need state and an HTTP response. The OP's
`DefaultDPoPProofValidator` passes the client's settings in (`dpop_signing_alg_values_supported`,
`dpop_proof_lifetime`), maps a rejection to `invalid_dpop_proof`, runs the nonce challenge (`use_dpop_nonce` with a
fresh `DPoP-Nonce`) and records `(jkt, jti)` in `IDPoPProofReplayCache`. Binding, `cnf`, nonce and replay are
[ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)'s.

One vocabulary: the JOSE names are `JoseTokenTypes.DPoPJwt` and `JoseClaimNames.Payload.Htm`, `.Htu` and `.Ath`. The
HTTP header names `DPoP` and `DPoP-Nonce` are an OpenID transport concern, `OpenIdProtocol.ProofOfPossession.HeaderName`
and `.NonceHeaderName` in `NCode.Identity.OpenId.Protocol.Abstractions`.

## Options considered

- **A second cryptographic path inside the OpenID stack**, with a hand-split JWS and raw `RSA`/`ECDsa` verification
  built from the JWK's public parameters. It supports only RSA and EC, duplicates the JOSE library's verification, and
  gives a client, a test or a sibling package nothing to create a proof with. It loses.
- **A JOSE service that creates and validates proofs.** It reuses the registered algorithms and key-type descriptors,
  so every key type the stack knows is a DPoP key type. It wins.

## Consequences

- Every asymmetric key type and signature algorithm the JOSE stack registers is a DPoP key type and algorithm, with no
  DPoP-specific code. `EdDSA` proofs validate once the opt-in EdDSA package is registered and the algorithm is in the
  allow-list.
- A client library, a resource server, a test and the OP create and validate proofs through the same service.
- `AddJoseLibrary()` registers `TimeProvider.System` when no `TimeProvider` is registered, and DPoP key import resolves
  the Secrets library's key-type descriptors, which need logging.
- A "you chose wrong" smell: DPoP-specific cryptography appearing outside the service, or a stateful check moving into
  it.

## References

- RFC 9449 (OAuth 2.0 Demonstrating Proof of Possession) and RFC 7638 (JWK Thumbprint).
- `IDPoPProofService`, `DefaultDPoPProofService`, `DPoPProofCreationOptions`, `DPoPProofValidationParameters` and
  `DPoPProof` in `NCode.Identity.Jose.Abstractions` and `NCode.Identity.Jose`; `DefaultDPoPProofValidator` and
  `IDPoPProofReplayCache` in the OpenID authentication stack.
- [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md):
  binding, `cnf`, nonce and replay.
- [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md): asynchronous signing.
