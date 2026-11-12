# 1390. The opt-in EdDSA package implements Ed25519, Ed448, X25519 and X448 clean-room over OKP keys

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

The JOSE library implements RSA and ECDSA signatures, but EdDSA ([RFC 8032](https://datatracker.ietf.org/doc/html/rfc8032),
[RFC 8037](https://datatracker.ietf.org/doc/html/rfc8037)) over Ed25519 and Ed448 is a modern, recommended signature
scheme that FAPI 2.0 permits. [RFC 8037 §3.2](https://datatracker.ietf.org/doc/html/rfc8037#section-3.2) also registers
`ECDH-ES` over the `OKP` curves X25519 and X448, the Diffie-Hellman functions of
[RFC 7748](https://datatracker.ietf.org/doc/html/rfc7748). Both uses share the `OKP` key type: a JSON Web Key with
`kty`, `crv`, `x` and, for a private key, `d`.

The library's other self-implemented algorithms compose BCL primitives, but .NET 10 has no Edwards or Montgomery curve
primitive, only the hashes the schemes need (`SHA512` and `Shake256`). The pure-managed third-party options are built
only against an end-of-life target framework, and the native-backed options add an unmanaged dependency and cover only
Ed25519. The curve arithmetic is therefore written here, from the specifications.

Two facts shape the keys. An X25519 key and an Ed25519 key are both 256 bits, so neither the key type nor the key size
tells them apart: only the curve does. And RFC 8037 §3.2 and RFC 7748 §6 warn that a public key of small order makes the
shared secret all zeros, which a party who chose that key knows in advance.

## Decision

EdDSA and the X25519 and X448 key agreements are implemented clean-room in the opt-in package
`NCode.Identity.Jose.EdDsa`, over one `OKP` key type, with no third-party cryptographic dependency in shipping code.

1. **The arithmetic is written from RFC 8032, RFC 8037, RFC 7748 and RFC 8410.** `Fe25519` works in radix 2⁵¹ (five
   limbs) and `Fe448` in radix 2⁵⁶ (eight limbs), with `UInt128` products, and the hashes are the BCL's `SHA512` and
   `Shake256`.
2. **The arithmetic is constant-time.** Scalar multiplication is double-and-add-always with masked selects; the
   X25519 and X448 functions are a Montgomery ladder with a masked conditional swap. Scalars are clamped, the X25519
   top bit is masked, a non-canonical `u` is processed as reduced, and secrets are zeroed with
   `CryptographicOperations.ZeroMemory`. Speed yields to constant time and to the test vectors.
3. **The package is opt-in and depends only on abstractions.** `NCode.Identity.Jose.EdDsa` references only
   `Jose.Abstractions` and `Secrets.Abstractions`
   ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
   `AddEdDsa()` extends `IServiceBuilder<JoseLibrary>`, so a host cannot call it without the JOSE library
   ([ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)). It registers
   with `TryAddEnumerable` the `OkpSecretKeyTypeDescriptor`, the internal `EdDsaAlgorithmDataSource`
   (`ICollectionDataSource<Algorithm>`) and `OkpEcdhKeyAgreement`. The core JOSE library gains no dependency.
4. **One signature algorithm class serves three codes.** The internal `EdDsaSignatureAlgorithm(string code,
   EdwardsOkpCurve? curve)` serves `EdDSA`, `Ed25519` and `Ed448`; a null curve is the polymorphic `EdDSA`. The
   algorithm codes and token hashes are those of
   [ADR-1392](1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md).
5. **Signing algorithms are advertised as asymmetric without EdDSA wiring.** The algorithm settings select
   `AcceptsOnly<AsymmetricSecretKey>()` algorithms, so discovery needs nothing specific to EdDSA.
6. **`OkpSecretKey` is a public abstract `AsymmetricSecretKey`.** It is built from the `crv`, the size, the raw public
   key and an optional certificate. Its internal has-private and public-only implementations are reached through DI
   ([project-template ADR-0105](project-template/0105-the-canonical-implementation-is-named-defaultfoo.md)). Its public
   material is SPKI DER written through the buffer writer
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)), and
   the private seed is held data-protected and exported only by `ExportPrivateKey`. The descriptor generates keys,
   imports and exports JWK and PEM, and reads stored PKCS#8 and SPKI (RFC 8410, OIDs `1.3.101.110` to `1.3.101.113`).
7. **The curve table is closed, and every curve has one use.** The internal `OkpCurves` table holds exactly the curves
   RFC 8032 and RFC 7748 define, so it is provably closed. Each entry is an internal `OkpCurve` of its kind: an
   `EdwardsOkpCurve` that signs (`Ed25519Curve`, `Ed448Curve`) or a `MontgomeryOkpCurve` that agrees (`X25519Curve`,
   `X448Curve`), indexed by `crv` and OID. A caller selects a curve by its `crv` string. A curve's use is the operations
   it has, so each algorithm lists only the curves of its use: X25519 never signs and Ed25519 never agrees, although
   their sizes match. A key's size is 8 × its public key's length, so an X448 key is 448 bits and an Ed448 key 456.
8. **One agreement serves both Montgomery curves.** `OkpEcdhKeyAgreement` is the `IEcdhKeyAgreement` for `OkpSecretKey`.
   It serves X25519 at 256 bits and X448 at 448 bits and resolves the curve from the key. The shared secret `Z` is 32
   or 56 bytes, and the Concat KDF stays the algorithm's. A key on a curve of the other use is refused.
9. **An all-zero shared secret is refused on both sides.** The recipient refuses a small-order `epk`, and the sender
   refuses a small-order recipient key rather than encrypting under a key anyone can derive. The check reads every
   byte whatever its value. Public keys are not otherwise validated, as RFC 7748 §6 allows. An `epk` holding `d` or on
   another curve than the recipient's key is refused, and a sent `epk` carries only `kty`, `crv` and `x`.
10. **Correctness is pinned by vectors and an oracle.** The RFC vectors, including RFC 7748's iterated vectors to 1,000
    iterations, and byte-for-byte differential tests against a test-only oracle that shipping code never references
    pin the functions. `XdhBenchmarks` measures them
    ([ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)).

## Options considered

- **A third-party library.** Rejected: the managed one that covers both curves targets an end-of-life framework, and the
  native ones add an unmanaged dependency and cover only Ed25519.
- **Folding EdDSA into the core library.** Rejected: the core would carry arithmetic most hosts never use; an opt-in
  package keeps the core free of it.
- **A separate key type or package for X25519 and X448.** Rejected: RFC 8037 defines one `OKP` key type for both uses,
  so a second type would split one `kty` across two descriptors, and a second package would depend on this package's
  keys, descriptor and field arithmetic or duplicate them.
- **Telling the curves apart by key size.** Rejected: X25519 and Ed25519 keys are both 256 bits.
- **A curve record with a use flag, dispatched by name.** Rejected: every operation would switch over the curve names,
  each new curve would be added to every switch, and a curve that cannot sign would carry a signature size it does not
  have. A curve object of its kind carries only the operations its use has.
- **Validating public keys against a list of small-order points.** Rejected: the all-zero check catches every one of
  them, including non-canonical encodings, at the point where the secret is used, as RFC 7748 §6 recommends.
- **The chosen design:** clean-room, constant-time arithmetic in an opt-in package over one `OKP` key type, with the
  curve's kind deciding its use.

## Consequences

- The core JOSE library gains no dependency. EdDSA and `OKP` `ECDH-ES` exist only with `AddEdDsa()`.
- With `AddEdDsa()`, `ECDH-ES` and its key-wrap variants accept `OKP` X25519 and X448 keys, and the JWE decryption key
  check, the credential selector and the capabilities document serve them unchanged.
- A key on a curve of the other use is refused when it is declared for an algorithm, as an Ed25519 key for `ECDH-ES` or
  an X25519 key for `EdDSA` is when a client registers it.
- If the BCL ships Edwards curves, the leaves can be re-pointed behind `OkpSecretKey` and `EdDsaSignatureAlgorithm`
  without a change to the public surface.
- Hand-written curve arithmetic carries the risk of subtle error; the RFC vectors and the oracle's differential tests
  are the check.

## References

- [RFC 7748](https://datatracker.ietf.org/doc/html/rfc7748), [RFC 8032](https://datatracker.ietf.org/doc/html/rfc8032),
  [RFC 8037](https://datatracker.ietf.org/doc/html/rfc8037), [RFC 8410](https://datatracker.ietf.org/doc/html/rfc8410)
- [ADR-1069](1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md),
  [ADR-1386](1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md),
  [ADR-1392](1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [project-template ADR-0105](project-template/0105-the-canonical-implementation-is-named-defaultfoo.md)
- Issue [#336](https://github.com/NCodeGroup/identity/issues/336), part of [#66](https://github.com/NCodeGroup/identity/issues/66)
- `Fe25519`, `Fe448`, `X25519`, `X448`, `OkpSecretKey`, `OkpCurves`, `EdDsaSignatureAlgorithm`, `OkpEcdhKeyAgreement` and
  `DefaultRegistration` in `NCode.Identity.Jose.EdDsa`
