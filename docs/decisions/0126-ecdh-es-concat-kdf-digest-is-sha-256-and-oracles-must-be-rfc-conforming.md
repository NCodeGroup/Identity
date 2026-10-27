# 126. The ECDH-ES Concat KDF digest is SHA-256, and third-party oracles must be RFC-conforming

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** NCode Group

## Context

JWE key agreement (`ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW`, `ECDH-ES+A256KW`) derives the content encryption key
from the ECDH shared secret with the Concat KDF of [NIST SP 800-56A] §5.8.1.
[RFC 7518 §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2) pins one parameter that an implementer
is tempted to infer:

> Key derivation is performed using the Concat KDF, as defined in Section 5.8.1 of [NIST.800-56A], **where the Digest
> Method is SHA-256**.

The digest is a property of the **JWE key-agreement algorithm**, not of the key. It does not scale with the curve: a
P-521 agreement still derives through SHA-256. The intuition that a stronger curve should pair with a longer digest is
wrong here, and nothing in the `alg` value or the key encodes a digest choice.

Two independent facts made the wrong intuition easy to adopt and hard to detect:

1. **Windows CNG derives the curve-dependent way.** `NCryptDeriveKey` with the `SP800_56A_CONCAT` KDF, invoked without
   an explicit `KDF_HASH_ALGORITHM`, does not derive the RFC-conforming key on the larger NIST curves. The jose-jwt
   source states it plainly: *"Microsoft CNG implementation is unusable on NIST-384 and NISP-521 curves due to broken
   key derivation."*
2. **The cross-check tests used that CNG path as their oracle.** The ECDH tests asserted our output equalled
   jose-jwt's `EcdhKeyManagementWin` driven by a `CngKey`. Two implementations agreeing is only evidence when at least
   one is known to conform; here both carried the same deviation, so the suite reported green while the library
   produced keys no other JOSE implementation could reproduce.

The two oracles disagree on exactly the affected curves, which is the whole signal:

| Curve | jose-jwt CNG (`EcdhKeyManagementWin`) vs managed (`EcdhKeyManagementUnix`) |
| ----- | -------------------------------------------------------------------------- |
| P-256 | identical                                                                  |
| P-384 | **differs**                                                                |
| P-521 | **differs**                                                                |

A third, unrelated constraint pointed the same way: the JOSE test project was the only project in the family targeting
`net10.0-windows`, purely to reach `CngKey`. That single TFM made the whole solution unbuildable on a Linux CI runner.

## Decision

**The Concat KDF digest for ECDH-ES is SHA-256, unconditionally.** `EcdhKeyManagementAlgorithm.DeriveKey` fixes the
digest and the `reps` computation at SHA-256 and no longer accepts the curve size. The curve size remains in use where
it genuinely belongs — emitting and validating the `epk` `crv` field — but it has no bearing on key derivation.

**A third-party control oracle must be chosen for conformance, not for convenience or platform.** When a test pins our
output against another implementation, that implementation must be the one known to follow the specification. Where a
library exposes both a platform-native and a managed path, the test drives the **managed, RFC-conforming** path. For
ECDH this is jose-jwt's `EcdhKeyManagementUnix` (BCL `ECDiffieHellman`) — despite the name it is the cross-platform
path, and `EcdhKeyManagementWin` itself delegates to it for any `ECDiffieHellman` / `ECDsa` / `Jwk` key. The CNG path
is never an oracle.

**Test projects target the portable TFM.** No project in the family targets a platform-specific TFM in order to reach a
platform-native cryptographic type, because that both forecloses Linux CI and tends to smuggle the platform's
non-conforming behavior into the assertions.

## Options considered

- **Keep the curve-dependent digest.** Rejected: it contradicts the explicit normative text of RFC 7518 §4.6.2 and
  makes the library non-interoperable on P-384 and P-521 — the failure is silent, surfacing as an undecryptable token
  rather than an error.
- **Keep the CNG oracle and assert the RFC result only for P-256.** Rejected: it preserves a green suite over the
  precise curves where the defect lives, which is the condition that hid it.
- **Keep the CNG oracle for Windows and add the managed oracle for Linux.** Rejected: a test whose expected value
  depends on the host platform cannot express a conformance requirement, and it would keep asserting known-broken
  behavior on Windows.
- **Fix the digest and move every oracle to the RFC-conforming managed path (chosen).** The suite now fails on exactly
  the defect, and the same assertions hold on every platform.

## Consequences

- ECDH-ES over P-384 / P-521 is now interoperable with conforming JOSE implementations. This is a **breaking change to
  token compatibility**: a JWE this library previously issued on those curves can no longer be decrypted by it. The
  pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) makes this a clean cutover with
  no compatibility shim. P-256 output is unchanged.
- `NCode.Identity.Jose.Tests` targets `net10.0` rather than `net10.0-windows`, which is what lets the family build and
  test on a Linux CI runner ([ADR-0125](0125-trunk-based-development-on-main.md) brings that CI online).
- The ECDH cross-check tests are now a genuine regression test for this defect: reinstating the curve-dependent digest
  fails them on P-384 and P-521.
- The general rule — *an oracle is only evidence if it is known to conform* — applies beyond ECDH. Where an independent
  implementation is used to validate a cryptographic primitive, prefer the specification's own test vectors, and treat
  a second implementation as corroboration only after confirming it conforms on the inputs under test.

## References

- [RFC 7518 §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2) — Key Derivation for ECDH Key
  Agreement; the normative "Digest Method is SHA-256".
- [NIST SP 800-56A](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-56Ar3.pdf) §5.8.1 — the Concat
  KDF this derivation implements.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release posture that makes the token-format
  correction a clean cutover.
- [ADR-0125](0125-trunk-based-development-on-main.md) — the continuous-integration gate whose Linux runner surfaced the
  platform-specific target framework.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — why the oracle-selection rule is recorded here
  rather than left as a review habit.
- `src/NCode.Identity.Jose/Algorithms/KeyManagement/EcdhKeyManagementAlgorithm.cs` — the derivation.
- `tests/NCode.Identity.Jose.Tests/Algorithms/KeyManagement/EcdhKeyManagementAlgorithmTests.cs`,
  `tests/NCode.Identity.Jose.Tests/Algorithms/KeyManagement/EcdhWithAesKeyManagementAlgorithmTests.cs` — the
  cross-check tests and their oracle.
