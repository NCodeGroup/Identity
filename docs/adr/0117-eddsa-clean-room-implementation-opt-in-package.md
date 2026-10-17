# 117. EdDSA (Ed25519 / Ed448) is a clean-room implementation in an opt-in package

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers

## Context

The JOSE library implements RSA (`RS*`/`PS*`) and ECDSA (`ES*`) signatures but not **EdDSA** (the JWA `alg` value
`EdDSA`, RFC 8037) over the Edwards curves **Ed25519** and **Ed448** (RFC 8032). EdDSA is a modern, recommended
signature scheme — FAPI 2.0 permits it, and the security-practice default points at it as the next addition.

Unlike the library's other self-implemented algorithms (AES-KW, Concat-KDF/ECDH, PBES2, AES-CBC-HMAC), which only
compose **BCL primitives** (`Aes.EncryptEcb`, `ECDiffieHellman`, `HMAC`), EdDSA has **no** BCL primitive to compose:
`System.Security.Cryptography` on .NET 10 ships the post-quantum set (`MLDsa`, `SlhDsa`, `MLKem`, `CompositeMLDsa`) but
**no** Edwards-curve type (`Ed25519`/`Ed448`/`EdDsa`). Native .NET EdDSA is an approved API proposal but is not in
.NET 10 (earliest a later release). The pure-managed third-party options are also unacceptable long-term: the one that
covers both curves is built only against an EOL target framework (an audit/SBOM finding on metadata regardless of the
runtime it executes on), and the native-backed options add an unmanaged dependency and cover only Ed25519.

The two hashes EdDSA needs — **SHA-512** (Ed25519) and **SHAKE256** (Ed448) — _are_ in the BCL (`SHA512`,
`Shake256`). Only the Edwards-curve group arithmetic is missing.

## Decision

1. **Implement EdDSA clean-room from the RFCs** (RFC 8032 / RFC 8037 / RFC 7748 / RFC 8410), composing the BCL hash
   primitives, with **zero third-party cryptographic dependency** in shipping code. The field, scalar, and group
   arithmetic for both curves is written from the specifications: Ed25519 over GF(2²⁵⁵−19) in radix-2⁵¹ (five limbs),
   Ed448 over GF(2⁴⁴⁸−2²²⁴−1) in radix-2⁵⁶ (eight limbs), with `System.UInt128` limb products.

2. **The implementation is constant-time.** No secret-dependent branch or memory access: scalar multiplication is
   double-and-add-always with masked conditional selects, scalars/field elements decode and reduce branch-free, and
   secret buffers are zeroed after use (`CryptographicOperations.ZeroMemory`). The crypto hot paths follow the
   aggressive-optimization convention ([ADR-0116](0116-memory-allocations-and-buffer-operations.md) §6) — the two
   constraints that override speed are constant-time behavior and correctness pinned by test vectors.

3. **EdDSA ships as a separate, opt-in package `NCode.Identity.Jose.EdDsa`** rather than being folded into the core
   JOSE library. The package depends **only on abstractions** (`NCode.Identity.Jose.Abstractions`,
   `NCode.Identity.Secrets.Abstractions`) plus foundational utilities, never a concrete sibling implementation
   ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)). A host turns it on with a single
   `serviceCollection.AddEdDsa()`, which registers the OKP key-type descriptor and the EdDSA signature algorithm via
   `TryAddEnumerable`. Because discovery enumerates the live algorithm collection and partitions signing algorithms by
   `typeof(AsymmetricSecretKey).IsAssignableFrom(KeyType)`, EdDSA is advertised in the asymmetric signing metadata
   automatically once registered — no discovery wiring is needed.

4. **The OKP (Octet Key Pair) secret key is a first-class `AsymmetricSecretKey`.** `OkpSecretKey` is a public abstract
   extension point consistent with the sibling key bases (`RsaSecretKey`, `EccSecretKey`): a public constructor over
   public primitives (the JWK `crv` name, the key size, the raw public key, an optional certificate), with the two
   concrete implementations (`internal` has-private and public-only) reached through DI per the `DefaultFoo` convention
   ([ADR-0006](0006-single-canonical-impl-is-defaultfoo.md)). Its public material is produced as `SubjectPublicKeyInfo`
   DER (RFC 8410) through the buffer-writer primitive ([ADR-0115](0115-asymmetric-keys-model-der-material-via-buffer-writers.md)),
   and the private seed is held data-protected in memory, exported only through `ExportPrivateKey`. The descriptor
   generates (random seed → PKCS#8), imports public JWK/PEM, exports the public JWK + SPKI PEM, and deserializes stored
   PKCS#8/SPKI — the single `ISecretKeyTypeDescriptor` seam ([ADR-0105](0105-secret-key-type-descriptor-first-class-seam.md)).

5. **The EdDSA curve set is closed, not an extensibility seam.** RFC 8032 defines exactly two curves (Ed25519, Ed448),
   and a third is a different algorithm, so the curve catalog (`OkpCurveInfo` / `OkpCurves`) is `internal` — deliberately
   unlike the ECC curve catalog (`EccCurveSpecification` / `IEccCurveCatalog`), which is public because ECC's named-curve
   set is genuinely open and consumer-extensible ([ADR-0068](0068-unified-extensible-elliptic-curve-catalog.md)). A
   consumer selects the curve by passing the raw JWK `crv` string (`Ed25519` / `Ed448`), matching how ECC consumers pass
   a raw curve name. A single `EdDSA` algorithm code covers both curves; the concrete curve is selected from the signing
   key's `crv` at sign/verify time.

## Consequences

- EdDSA signing and verification are available with **no** third-party cryptographic dependency and **no** EOL-TFM or
  native-library audit exposure. `F29` is delivered; `F28`'s EdDSA item is unblocked.
- Correctness is pinned by differential tests that cross-check the clean-room output **byte-for-byte** against an
  independent implementation used solely as a **test oracle** (never referenced by shipping code), plus the structural
  group/field/scalar invariants. The oracle lives only in the test project.
- The core JOSE library gains no new dependency and no always-on algorithm; EdDSA is additive and opt-in. A host that
  wants it calls `AddEdDsa()`; a host that does not is unaffected.
- When native .NET EdDSA eventually ships, the sign/verify leaves can be re-pointed at the BCL primitive behind the
  same `OkpSecretKey` / `EdDsaSignatureAlgorithm` seams without a surface change — the clean-room arithmetic becomes an
  implementation detail that can be retired.
