# 115. Asymmetric secret keys model public material as DER via buffer writers (PQC-ready)

- **Status:** Accepted
- **Date:** 2026-10-16
- **Deciders:** NCode.Identity maintainers

## Context

The asymmetric secret-key model ([`AsymmetricSecretKey`](../../src/NCode.Identity.Secrets.Abstractions/Keys/AsymmetricSecretKey.cs))
exposed its public half through `ExportAsymmetricAlgorithm()` returning a
`System.Security.Cryptography.AsymmetricAlgorithm`, and the base `ExportSubjectPublicKeyInfoPem()` was derived from it.
That coupled **every** asymmetric key to the BCL `AsymmetricAlgorithm` type hierarchy.

.NET 10 added post-quantum cryptography — `MLDsa`, `SlhDsa`, `MLKem`, `CompositeMLDsa` — and those types **deliberately
do not derive from `AsymmetricAlgorithm`** (they extend `object`, are `IDisposable`, and use the template-method
pattern). Microsoft's stated rationale: `KeySize` and other `AsymmetricAlgorithm` members do not generalize to the new
algorithms, and "don't have a common base class when two things don't really have anything in common"
([Post-Quantum Cryptography in .NET](https://devblogs.microsoft.com/dotnet/post-quantum-cryptography-in-dotnet/)). Their
**only** common surface with RSA/ECC is **DER key material** — `ExportSubjectPublicKeyInfo()` / `ExportPkcs8PrivateKey()`
/ `ImportSubjectPublicKeyInfo` / PEM — with the actual operations (`SignData`/`VerifyData`, `Encapsulate`/`Decapsulate`)
as **algorithm-specific** members, not a shared base.

The same constraint applies to Edwards-curve **OKP** keys (Ed25519/Ed448): they have no `AsymmetricAlgorithm`
representation in the BCL at all. An `AsymmetricAlgorithm`-centric model therefore **cannot** represent PQC or OKP keys
as first-class `AsymmetricSecretKey` instances — which blocks the forward-looking algorithm roadmap ([#66](https://github.com/NCodeGroup/Identity/issues/66)/[#68](https://github.com/NCodeGroup/Identity/issues/68)).

## Decision

1. **The universal public-key-material primitive is `SubjectPublicKeyInfo` (SPKI) DER, not `AsymmetricAlgorithm`.**
   `AsymmetricSecretKey.ExportAsymmetricAlgorithm()` is removed; `ExportSubjectPublicKeyInfo<TWriter>(ref TWriter
destination)` replaces it as the one primitive every asymmetric family implements. RSA/ECC implement it from their
   family exports (`ExportRSA()` / `ExportECDsa()` → `TryExportSubjectPublicKeyInfo`); a future PQC or OKP key implements
   it from its own DER encoder. Callers that genuinely need a live classical algorithm still use the **family-specific**
   `ExportRSA()` / `ExportECDsa()` accessors (unchanged). Cryptographic **operations** (sign/verify, encapsulate/
   decapsulate) remain algorithm-specific seams layered on top (for example `SignatureAlgorithm`), never on the key
   base — matching Microsoft's separation.

2. **The public-key-material primitive follows the general buffer convention** ([ADR-0116](0116-memory-allocations-and-buffer-operations.md)):
   it writes into a caller-supplied `IBufferWriter<byte>` (`void ExportSubjectPublicKeyInfo<TWriter>(ref TWriter
destination) where TWriter : IBufferWriter<byte>, allows ref struct`) rather than returning a `byte[]`. The derived
   `ExportSubjectPublicKeyInfoPem()` convenience rents a **non-sensitive** pooled writer
   (`BufferFactory.CreatePooledBufferWriter(isSensitive: false)`), writes the SPKI DER, flattens the `Sequence<byte>` to a
   contiguous span via `GetSpanLease`, and PEM-encodes — the SPKI public half is not secret, so the shared `MemoryPool`
   (not the pinned `SecureMemoryPool`) is used.

## Consequences

- The asymmetric key model is now forward-compatible with post-quantum (ML-DSA / SLH-DSA) and Edwards-curve **OKP**
  keys **without further base changes**: such a key derives from `AsymmetricSecretKey` and implements the DER primitive,
  and its operations attach as algorithm-specific seams. This unblocks [#68](https://github.com/NCodeGroup/Identity/issues/68) (EdDSA) and the PQC direction in [#66](https://github.com/NCodeGroup/Identity/issues/66).
- `PublicAPI.Unshipped.txt` churn in `NCode.Identity.Secrets.Abstractions` (removed `ExportAsymmetricAlgorithm`; added
  the writer-based `ExportSubjectPublicKeyInfo<TWriter>`). Expected and acceptable under the greenfield posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)); it supersedes the `ExportAsymmetricAlgorithm`-shaped
  surface described in [ADR-0067](0067-secret-type-capability-catalog-and-capabilities-discovery.md) and
  [ADR-0105](0105-secret-key-type-descriptor-first-class-seam.md).
- This change adds **no** new algorithms or key types — only the enabling model and convention. The EdDSA (OKP)
  implementation itself is tracked as issue [#68](https://github.com/NCodeGroup/Identity/issues/68).
