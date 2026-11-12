# 1118. ML-DSA post-quantum signatures ship in the core libraries over the BCL MLDsa type

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode Group

## Context

The JOSE library signs with RSA (`RS*`, `PS*`) and ECDSA (`ES*`). Post-quantum signing is the next algorithm on the
security-practice roadmap (issue [#66](https://github.com/NCodeGroup/identity/issues/66)): ML-DSA (FIPS 204), the
module-lattice scheme that NIST standardized to replace RSA and ECDSA against a cryptographically relevant quantum
computer.

Two facts settle how it lands:

1. ML-DSA's JOSE binding is a finalized RFC. [RFC 9964](https://datatracker.ietf.org/doc/rfc9964/) registers the `alg`
   values `ML-DSA-44`, `ML-DSA-65` and `ML-DSA-87`, the JWK and COSE key type `AKP` (Algorithm Key Pair), and the key
   representation: `pub` is the raw public key and `priv` is the 32-byte seed, never the expanded key. The PKCS#8 and
   X.509 DER container formats are fixed by the published [RFC 9881](https://www.rfc-editor.org/info/rfc9881).
2. The primitive is in the BCL. .NET 10's `System.Security.Cryptography.MLDsa` is a first-class type that does the
   cryptography: key generation, raw and container import and export, `SignData` and `VerifyData`. ML-DSA therefore
   composes a stable BCL API as the RSA and ECC algorithms do, with no third-party dependency and no clean-room
   implementation.

The sibling post-quantum algorithms are not ready on the same two tests. SLH-DSA's BCL type is `[Experimental]` and its
COSE draft expired. ML-KEM for JWE has a stable BCL type, but its JOSE binding still has code points to be assigned.

## Decision

ML-DSA ships in the core libraries, over the BCL `MLDsa` type.

1. **Placement.** `ML-DSA-44`, `ML-DSA-65` and `ML-DSA-87` live in `NCode.Identity.Jose(.Abstractions)`, and the `AKP`
   key type lives in `NCode.Identity.Secrets(.Abstractions)`. Both are registered by default, so a host gets ML-DSA with
   no extra call. The stable BCL primitive removes the reason an algorithm with no BCL primitive needs an opt-in
   package.
2. **The `AKP` key type is a first-class `AsymmetricSecretKey`.** `AkpSecretKey` is a public abstract class beside
   `RsaSecretKey`, `EccSecretKey` and `OkpSecretKey`. It carries the `MLDsaAlgorithm` and the raw public key. Two
   internal implementations, one with a private key and one public-only, are reached through
   `AkpSecretKeyTypeDescriptor`, the registered descriptor of the key-type seam
   ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)).
   Public material is written as `SubjectPublicKeyInfo` DER through the buffer writer of the same record. The
   descriptor generates a key pair, imports a public JWK or PEM, exports the public JWK and SPKI PEM, and deserializes
   stored PKCS#8 and SPKI.
3. **The JWK.** `kty` is `AKP`, `pub` is the raw public key and `priv` is the 32-byte seed only, never the expanded
   key.
4. **At-rest key material is PKCS#8 or SPKI DER** (RFC 9881), uniform with every other asymmetric family. The OID
   makes the parameter set self-describing, and X.509 interoperability stays open. The BCL flags these container-format
   members `[Experimental]` (`SYSLIB5006`) although the encoding is published, so the diagnostic is suppressed only at
   those call sites, each with a comment that points to this record. The raw public key and the seed go through the
   stable `MLDsa` members.
5. **ML-DSA is advertised only where the platform can fulfill it.** `DefaultAlgorithmDataSource` registers the three
   algorithms only when `MLDsa.IsSupported` is true (OpenSSL 3.5 or later, a recent Windows 11), so discovery metadata
   and the JWKS never offer an `alg` the running platform cannot sign or verify. The `AKP` descriptor is always
   registered; it throws `PlatformNotSupportedException` only when it is used on a platform without ML-DSA.
6. **The parameter-set catalog is closed.** FIPS 204 defines exactly three sets, and each is a distinct `alg`.
   `MlDsaParameterSets` is therefore internal, and a caller selects a set by its `alg` name.
7. **Advertisement.** ML-DSA is an asymmetric signing algorithm: the algorithm settings select
   `AcceptsOnly<AsymmetricSecretKey>()` algorithms, so it appears in the asymmetric signing metadata once registered.
8. **Not implemented.** SLH-DSA is tracked in issue [#66](https://github.com/NCodeGroup/identity/issues/66). ML-KEM for
   JWE is tracked in issue [#338](https://github.com/NCodeGroup/identity/issues/338); its keys will reuse `AKP` and
   `AkpSecretKey`.

## Options considered

- **An opt-in package, as EdDSA has.** Rejected: an opt-in package exists to isolate a clean-room implementation or a
  third-party dependency, and ML-DSA has neither. It would make post-quantum signing a second step for every host.
- **Advertise ML-DSA unconditionally.** Rejected: on a platform without ML-DSA, discovery and the JWKS would offer an
  `alg` that cannot be signed or verified, which is an interoperability bug.
- **The expanded private-key form.** Rejected: RFC 9964 defines the seed as the `priv` representation, and the seed is
  the compact, portable form.
- **Core libraries over the BCL `MLDsa` type, gated on `MLDsa.IsSupported`.** Chosen: no extra dependency or call, and
  nothing is advertised that the platform cannot use.

## Consequences

- Post-quantum signing is available out of the box with no third-party cryptographic dependency and no opt-in package.
- The cost is a narrowly scoped `#pragma warning disable SYSLIB5006` wherever the PKCS#8 and SPKI container formats are
  touched. When the BCL drops `[Experimental]` from those members, the suppressions are deleted with no other change.
- The tests are generate, sign and verify round trips and JWK and PEM public-key round trips. They no-op where
  `MLDsa.IsSupported` is false, so they run on every CI operating system.
- `AKP` is a general Algorithm Key Pair container (RFC 9964), so ML-KEM keys reuse the same type and seam without a
  change to the surface.

## References

- [RFC 9964](https://datatracker.ietf.org/doc/rfc9964/), JOSE and COSE for ML-DSA; [RFC 9881](https://www.rfc-editor.org/info/rfc9881),
  the PKCS#8 and X.509 encodings; FIPS 204.
- Issues [#66](https://github.com/NCodeGroup/identity/issues/66) and [#338](https://github.com/NCodeGroup/identity/issues/338).
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md), the
  key-type seam.
- Code: `NCode.Identity.Secrets` (`AkpSecretKey`, `AkpSecretKeyTypeDescriptor`) and `NCode.Identity.Jose`
  (`DefaultAlgorithmDataSource`, `MlDsaParameterSets`).
