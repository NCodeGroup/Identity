# 118. ML-DSA (post-quantum) signatures ship in the core libraries via the BCL

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers

## Context

The JOSE library implements RSA (`RS*`/`PS*`) and ECDSA (`ES*`) signatures, and the opt-in `NCode.Identity.Jose.EdDsa`
package adds `EdDSA` ([ADR-0117](0117-eddsa-clean-room-implementation-opt-in-package.md)). The next algorithm on the
security-practice roadmap (backlog `F28`) is **post-quantum signing** — **ML-DSA** (FIPS 204), the module-lattice
scheme NIST standardized to replace RSA/ECDSA against a cryptographically-relevant quantum computer.

Two facts settle how it should land, and both changed since `F28` was first written:

1. **ML-DSA's JOSE binding is a finalized RFC, not a draft.** [RFC 9964](https://datatracker.ietf.org/doc/rfc9964/)
   (May 2026) registers the `alg` values `ML-DSA-44` / `ML-DSA-65` / `ML-DSA-87` (IANA "Recommended: Yes"), the new
   JWK/COSE key type **`AKP`** (Algorithm Key Pair), and the key representation: `pub` is the raw public key and `priv`
   is the **32-byte seed** (seed form only, never the expanded key). The PKCS#8 / X.509 DER container formats are fixed
   by the published [RFC 9881](https://www.rfc-editor.org/info/rfc9881).

2. **The primitive is in the BCL and is permanent.** .NET 10's `System.Security.Cryptography.MLDsa` is a first-class,
   in-box type (not `[Experimental]` on the class) that does all the crypto — key generation, raw + container import/
   export, `SignData`/`VerifyData`. Unlike EdDSA (which has no BCL primitive and therefore needed a clean-room
   implementation), ML-DSA composes a stable BCL API exactly as the existing RSA/ECC algorithms do.

The sibling PQC algorithms are **not** ready on the same two tests and are deliberately out of scope here: **SLH-DSA**
(the BCL `SlhDsa` class is `[Experimental]`; the COSE SPHINCS+ draft is expired) and **ML-KEM** for JWE (the BCL
`MLKem` is stable, but its JOSE binding is still `draft-ietf-jose-pqc-kem` with **TBD** code points and "Recommended:
No").

## Decision

1. **ML-DSA ships in the core libraries, not an opt-in package.** Because it composes a stable BCL primitive with no
   third-party dependency, EdDSA's reason for a separate package does not apply. The `AKP` secret-key type lives in
   `NCode.Identity.Secrets(.Abstractions)` and the `ML-DSA-44/65/87` signature algorithms in
   `NCode.Identity.Jose(.Abstractions)`, registered by default alongside RSA/ECC. A host gets ML-DSA with no extra
   call.

2. **`AKP` is a first-class `AsymmetricSecretKey`, modeled on the OKP seam.** `AkpSecretKey` is a public abstract
   extension point consistent with `RsaSecretKey` / `EccSecretKey` / `OkpSecretKey`: it carries the ML-DSA parameter
   set (`MLDsaAlgorithm`) and the raw public key, exports its public material as `SubjectPublicKeyInfo` DER through the
   buffer-writer primitive ([ADR-0115](0115-asymmetric-keys-model-der-material-via-buffer-writers.md)), and reaches the
   two concrete implementations (`internal` has-private and public-only) through the single
   `ISecretKeyTypeDescriptor` seam ([ADR-0105](0105-secret-key-type-descriptor-first-class-seam.md)). The descriptor
   generates a key pair, imports a public JWK (`kty`=`AKP`) / PEM, exports the public JWK + SPKI PEM, and deserializes
   stored PKCS#8 / SPKI. Because discovery partitions signing algorithms by
   `typeof(AsymmetricSecretKey).IsAssignableFrom(KeyType)`, ML-DSA is advertised in the asymmetric signing metadata
   automatically once registered.

3. **At-rest key material is standard PKCS#8 / SPKI DER.** This keeps AKP uniform with every other asymmetric family at
   rest (the `SecretEncodings.Pkcs8` / `Spki` axis), makes the parameter set self-describing via its OID, and leaves
   room for X.509 certificate interop. The BCL marks these container-format members `[Experimental]` (`SYSLIB5006`)
   because they were caution-flagged at .NET 10 ship time, but their encoding is fixed by the **published** RFC 9881
   (and consumed by RFC 9964); the diagnostic is therefore suppressed at the specific call sites with a comment
   pointing here. The raw public key (for the JWK `pub`) and the 32-byte seed round-trip through the stable,
   non-experimental `MLDsa` members.

4. **ML-DSA is advertised only where the OS can fulfill it.** `MLDsa.IsSupported` is OS-dependent (OpenSSL 3.5+, a
   recent Windows 11). The algorithm data source gates the three ML-DSA entries on `MLDsa.IsSupported`, so a host's
   discovery metadata / JWKS never offer an `alg` the running platform cannot actually sign or verify — advertising an
   unfulfillable algorithm is an interop bug. The `AKP` secret-key descriptor is registered unconditionally (a benign
   capability that throws `PlatformNotSupportedException` only if exercised on an unsupported OS).

5. **The ML-DSA parameter-set catalog is closed.** FIPS 204 defines exactly three parameter sets, and each is a
   distinct `alg`, so the catalog (`MlDsaParameterSets`) is `internal` — deliberately unlike the open, consumer-
   extensible ECC curve catalog ([ADR-0068](0068-unified-extensible-elliptic-curve-catalog.md)). A caller selects the
   set by its `alg` name (`ML-DSA-44` / `ML-DSA-65` / `ML-DSA-87`).

## Consequences

- Post-quantum signing is available out of the box with **no** third-party cryptographic dependency and **no** opt-in
  package — the `F28` ML-DSA item is delivered. SLH-DSA and ML-KEM remain open in `F28` behind their own readiness
  gates.
- The one cost is a narrowly-scoped `#pragma warning disable SYSLIB5006` wherever the PKCS#8 / SPKI container formats
  are touched. When the BCL removes the `[Experimental]` flag (its spec is already published), the suppressions can be
  deleted with no other change.
- Signing/verification correctness is covered by generate → sign → verify and JWK/PEM public-key round-trip tests that
  no-op where `MLDsa.IsSupported` is false, so they stay portable across CI OSes.
- The `AKP` key type is a general Algorithm-Key-Pair container (RFC 9964); when ML-KEM's JOSE binding finalizes, its
  keys reuse the same `AKP` type and the same `AkpSecretKey` seam without a surface change.
