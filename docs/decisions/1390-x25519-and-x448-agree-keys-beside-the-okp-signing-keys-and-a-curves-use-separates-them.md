# 1390. X25519 and X448 agree keys beside the OKP signing keys, and a curve's use separates them

- **Status:** Accepted (amended by [ADR-1391](1391-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md):
  tenant provisioning generates the `Ed25519` and `Ed448` algorithms' keys on their curves, and only an algorithm that
  names no curve, such as `EdDSA` or `ECDH-ES`, still cannot be provisioned with an `OKP` key)
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Amends:** [ADR-1117](1117-eddsa-clean-room-implementation-opt-in-package.md), decision 3's registration and
  decision 5's curve set

## Context

[RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2) registers `ECDH-ES` over the `OKP` curves
X25519 and X448, the Diffie-Hellman functions of [RFC 7748](https://datatracker.ietf.org/doc/html/rfc7748), beside the
Ed25519 and Ed448 signing curves of §3.1. Both uses share the `OKP` key type: a JSON Web Key with `kty`, `crv`, `x`
and, for a private key, `d`. [ADR-1389](1389-one-algorithm-name-serves-every-key-type-it-declares.md) made the
`ECDH-ES` algorithms serve every key type that has a registered `IEcdhKeyAgreement`, so only the agreement, its
arithmetic and the keys were missing ([#336](https://github.com/NCodeGroup/Identity/issues/336)). Like the Edwards
curves, the Montgomery curves have no primitive in the BCL on .NET 10.

Two facts shape the design. First, an X25519 key and an Ed25519 key are both 256 bits, so neither the key type nor the
key size tells them apart: only the curve does. Second, RFC 8037 §3.2 and RFC 7748 §6 warn that a public key of small
order makes the shared secret all zeros, which a party who chose that key knows in advance.

## Decision

1. **X25519 and X448 are clean-room, constant-time Montgomery ladders.** `X25519` and `X448` in
   `NCode.Identity.Jose.EdDsa` implement the function of RFC 7748 §5 over the package's existing field arithmetic
   (`Fe25519`, `Fe448`): the scalar is clamped, the X25519 u-coordinate's most significant bit is masked, a
   non-canonical u-coordinate is processed as if reduced, and every ladder step performs the same operations with a
   masked conditional swap. The functions are pinned by the RFC 7748 §5.2 and §6 vectors, the iterated vectors to 1,000
   iterations, and a byte-for-byte differential test against a test-only oracle.
2. **The keys are `OKP` keys, and every curve has one use.** `OkpSecretKey`, its descriptor and its PKCS#8 / SPKI
   encoding (RFC 8410, OIDs `1.3.101.110` and `1.3.101.111`) serve all four curves; generation, import, export and
   storage are those of the signing keys. The internal curve catalog records each curve's use, signature or key
   agreement, and every consumer resolves a curve through its use: the `EdDSA`, `Ed25519` and `Ed448` algorithms accept
   only a signing curve, and the agreement only a key-agreement curve. An X25519 key therefore never signs and an
   Ed25519 key never agrees, although their sizes match. A key's size is `8 ×` its public key's length, so an X448 key
   is 448 bits and an Ed448 key 456.
3. **One agreement serves both curves.** `OkpEcdhKeyAgreement` is the `IEcdhKeyAgreement` for `OkpSecretKey`, because
   ADR-1389 lets no two agreements serve the same key type; it accepts 256- and 448-bit keys and resolves the curve
   from the key. The shared secret `Z` is the function's output, 32 or 56 bytes, and the Concat KDF stays the
   algorithm's.
4. **An all-zero shared secret is refused on both sides.** The recipient refuses an `epk` of small order, and the
   sender refuses a recipient key of small order rather than encrypting under a key anyone can derive. The check reads
   every byte whatever its value. Public keys are not otherwise validated, as RFC 7748 §6 allows. An `epk` holding `d`
   is refused, the `epk` of a sent message carries only `kty`, `crv` and `x`, and an `epk` on another curve than the
   recipient's key, including a signing curve, is refused (ADR-1389 decision 3).
5. **`AddEdDsa()` registers the agreement.** The package's one registration adds the `OKP` key type, the signing
   algorithms and the agreement, because they share the key type and a host that adds `OKP` keys expects every
   registered use of them. The package keeps its name; it remains opt-in, and a host that does not call it is
   unaffected.

## Options considered

- **A separate key type for the key-agreement curves.** Rejected: RFC 8037 defines one `OKP` key type for both uses,
  so a second secret type would split one JSON Web Key `kty` across two descriptors, and the catalog's JSON Web Key
  lookup could no longer resolve a received `OKP` key.
- **A separate package and registration for X25519 and X448.** Rejected: the keys, descriptor and field arithmetic are
  this package's; a second package would depend on its internals or duplicate them.
- **Telling the curves apart by key size.** Rejected: X25519 and Ed25519 keys are both 256 bits.
- **Validating public keys against a list of small-order points.** Rejected: the all-zero check catches every one of
  them, including non-canonical encodings, at the point where the secret is used, as RFC 7748 §6 recommends.

## Consequences

- With `AddEdDsa()`, `ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW` and `ECDH-ES+A256KW` accept `OKP` X25519 and X448
  keys after the `EC` keys, and the JWE decryption key check, the credential selector and the capabilities document
  serve them unchanged.
- Tenant key provisioning generates a key from an algorithm and a size and cannot choose a curve, so it cannot
  provision an `OKP` key of either use; the `OKP` descriptor refuses generation without a curve.
- `XdhBenchmarks` measures the two functions, as [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)
  requires of the package's primitives.

## References

- [RFC 7748](https://datatracker.ietf.org/doc/html/rfc7748), [RFC 8037](https://datatracker.ietf.org/doc/html/rfc8037),
  [RFC 8410](https://datatracker.ietf.org/doc/html/rfc8410), [RFC 7518 §4.6](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6)
- [ADR-1117](1117-eddsa-clean-room-implementation-opt-in-package.md),
  [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md),
  [ADR-1389](1389-one-algorithm-name-serves-every-key-type-it-declares.md)
- Issue [#336](https://github.com/NCodeGroup/Identity/issues/336), part of [#66](https://github.com/NCodeGroup/Identity/issues/66)
- `X25519`, `X448`, `OkpCurves`, `OkpEcdhKeyAgreement` and `DefaultRegistration` in `NCode.Identity.Jose.EdDsa`
