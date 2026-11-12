# 1126. The ECDH-ES Concat KDF digest is SHA-256, and a test oracle must conform to the RFC

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Context

JWE key agreement (`ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW`, `ECDH-ES+A256KW`) derives the content encryption key
from the ECDH shared secret with the Concat KDF of NIST SP 800-56A §5.8.1.
[RFC 7518 §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2) pins one parameter that an implementer
is tempted to infer:

> Key derivation is performed using the Concat KDF, as defined in Section 5.8.1 of [NIST.800-56A], **where the Digest
> Method is SHA-256**.

The digest is a property of the key-agreement algorithm, not of the key. It does not scale with the curve: a P-521
agreement still derives through SHA-256. The intuition that a stronger curve pairs with a longer digest is wrong here,
and nothing in the `alg` value or the key encodes a digest choice.

Windows CNG derives the key differently. `NCryptDeriveKey` with the `SP800_56A_CONCAT` KDF, invoked without an explicit
`KDF_HASH_ALGORITHM`, does not produce the RFC-conforming key on P-384 and P-521. The jose-jwt source says so: "Microsoft
CNG implementation is unusable on NIST-384 and NISP-521 curves due to broken key derivation." A test that takes the CNG
path as its oracle therefore agrees with an implementation that carries the same deviation. Two implementations agreeing
is evidence only when at least one is known to conform.

The two jose-jwt paths disagree on exactly the affected curves:

| Curve | jose-jwt CNG (`EcdhKeyManagementWin`) against managed (`EcdhKeyManagementUnix`) |
| ----- | ------------------------------------------------------------------------------- |
| P-256 | identical                                                                       |
| P-384 | differs                                                                         |
| P-521 | differs                                                                         |

A platform-specific target framework, reached only to use `CngKey`, would also make the solution unbuildable on a Linux
CI runner.

## Decision

**The Concat KDF digest for ECDH-ES is SHA-256 for every curve and key type.** `EcdhKeyManagementAlgorithm.DeriveKey`
fixes the digest and the `reps` computation at SHA-256, as RFC 7518 §4.6.2 requires. The curve matters only to the
`epk`: its size is used to emit and validate the `crv` field and has no bearing on key derivation.

**A third-party test oracle must conform to the RFC.** When a test pins the implementation's output against another
implementation, that implementation must be the managed, RFC-conforming path, chosen for conformance and not for
convenience or platform. For ECDH it is jose-jwt's `EcdhKeyManagementUnix` over the BCL's `ECDiffieHellman`. Despite the
name it is the cross-platform path, and `EcdhKeyManagementWin` itself delegates to it for any `ECDiffieHellman`,
`ECDsa` or `Jwk` key. The CNG path is never an oracle.

**The specification's own test vectors are preferred.** A second implementation corroborates a result only once it is
confirmed to conform on the inputs under test.

**Test projects target the portable TFM, `net10.0`.** No project targets a platform-specific TFM to reach native
cryptography, so `NCode.Identity.Jose.Tests` builds and runs on Linux CI and does not carry the platform's
non-conforming behavior into its assertions.

## Options considered

- **A curve-dependent digest.** Rejected: it contradicts the normative text of RFC 7518 §4.6.2 and makes the library
  non-interoperable on P-384 and P-521. The failure is silent, surfacing as an undecryptable token and not as an error.
- **Keep the CNG oracle and assert the RFC result for P-256 only.** Rejected: it leaves the suite green over the
  curves where a defect would live.
- **A platform-dependent expected value, CNG on Windows and managed on Linux.** Rejected: a test whose expected value
  depends on the host cannot express a conformance requirement, and it asserts the non-conforming behavior on Windows.
- **Fix the digest at SHA-256 and take every oracle from the RFC-conforming managed path (chosen).** The suite fails
  on exactly that defect, and the same assertions hold on every platform.

## Consequences

- ECDH-ES on P-384 and P-521 interoperates with conforming JOSE implementations; P-256 is identical on every path.
- The cross-check tests are a regression test for the digest: a curve-dependent digest fails them on P-384 and P-521.
- The rule that an oracle is evidence only when it is known to conform applies to every cryptographic primitive the
  family checks against an independent implementation, not to ECDH alone.
- The family builds and tests on a Linux CI runner, because no test project needs a Windows target framework.

## References

- [RFC 7518 §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2): Key Derivation for ECDH Key
  Agreement, the normative "Digest Method is SHA-256".
- [NIST SP 800-56A](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-56Ar3.pdf) §5.8.1: the Concat
  KDF this derivation implements.
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md): why the
  oracle-selection rule is recorded here and not left as a review habit.
- `src/NCode.Identity.Jose/Algorithms/KeyManagement/EcdhKeyManagementAlgorithm.cs`: the derivation.
- `tests/NCode.Identity.Jose.Tests/Algorithms/KeyManagement/EcdhKeyManagementAlgorithmTests.cs` and
  `tests/NCode.Identity.Jose.Tests/Algorithms/KeyManagement/EcdhWithAesKeyManagementAlgorithmTests.cs`: the cross-check
  tests and their oracle.
