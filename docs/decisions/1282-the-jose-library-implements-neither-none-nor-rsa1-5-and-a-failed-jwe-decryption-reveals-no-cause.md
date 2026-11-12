# 1282. The JOSE library implements neither none nor RSA1_5, and a failed JWE decryption reveals no cause

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group

## Context

RFC 7518 §3.6 defines `none`, the unsecured JWS: its signature is empty and proves nothing about who produced the
content. RFC 8725 §3.2 tells applications to reject it unless an unsecured token is the explicit intent. RFC 7518 §4.2
defines `RSA1_5`, key encryption with RSAES-PKCS1-v1_5, and RFC 8725 §3.2 says to avoid every RSA-PKCS1 v1.5 encryption
algorithm in favour of RSAES-OAEP. draft-ietf-jose-deprecate-none-rsa15 asks libraries to deprecate both algorithms and
applications to disable them by default, enabling them only "for the specific objects or operations that require it and
not at a global level". NIST SP 800-131A Rev. 2 disallows PKCS#1 v1.5 key transport for federal use.

A default algorithm registry that contains either algorithm exposes every direct consumer of `IJoseSerializer`: it
verifies a JWS that names `none`, or decrypts a JWE that names `RSA1_5`. `IJwtValidator` accepts them as well whenever
its list of valid algorithms is `null`, which means "any registered algorithm". No component of the family produces or
consumes an unsecured JWS or an `RSA1_5` JWE, so a registered algorithm serves no caller of the family and stays one
empty-key call, or one padding oracle, away from accepting a forged token or leaking a content key.

The padding oracle is a difference that a caller can observe. RFC 7516 §11.5 requires a JWE recipient not to
distinguish between format, padding and length errors of encrypted keys, and strongly recommends substituting a randomly
generated content encryption key (CEK) when the encrypted key cannot be unwrapped, so the decryption fails at the
authentication tag and its timing reveals nothing either. The attacks it guards against (RFC 3218, Bleichenbacher) turn
any observable difference between "the key did not unwrap" and "the tag did not verify" into an oracle on the key
encryption. That holds for every key management algorithm, not only `RSA1_5`: a bad AES key wrap integrity value or an
encrypted key of the wrong length is a difference too.

The OpenID provider and the protocol client are not exposed through the registry. They validate with explicit
allow-lists and refuse both names themselves. A consumer that uses the Jose or Jwt packages directly, without an
allow-list, is the one at risk.

## Decision

**The JOSE library implements neither the `none` signature algorithm nor the `RSA1_5` key management algorithm, and
every failure of a JWE decryption that depends on the encrypted key, the ciphertext or the tag is one
`JoseEncryptionException` with no cause visible.**

### No `none` and no `RSA1_5`

1. `DefaultAlgorithmDataSource` registers neither `none` nor `RSA1_5`, and no registration method adds them. A JWS that
   names `none` or a JWE that names `RSA1_5` fails like any unregistered algorithm, with a
   `JoseInvalidAlgorithmException`: "The `none` algorithm is not supported for digital signatures." or "The `RSA1_5`
   algorithm is not supported for key management."
2. RSA key management is only `RSA-OAEP`, `RSA-OAEP-256`, `RSA-OAEP-384` and `RSA-OAEP-512`, through the internal
   `RsaOaepKeyManagementAlgorithm(string code, HashAlgorithmName hashAlgorithmName)`. It builds
   `RSAEncryptionPadding.CreateOaep(hashAlgorithmName)` and has no PKCS#1 branch, so PKCS#1 v1.5 padding cannot be
   expressed. It is named by its scheme like `Pbes2KeyManagementAlgorithm` and `AesGcmKeyManagementAlgorithm`.
3. The names stay. `AlgorithmCodes.DigitalSignature.None` (`"none"`) and `AlgorithmCodes.KeyManagement.RsaPkcs1`
   (`"RSA1_5"`) are the IANA-registered `alg` values that policies refuse by name.
4. The guards stay. `SettingAlgorithms.IsSelectable`, `ClientDecryptionPolicy.GetPermittedKeyManagementAlgorithms`,
   `DefaultJwtValidator`, `ClientAssertionClientAuthenticationHandler`, `DefaultLoadAuthorizationRequestHandler`,
   `ProviderSignaturePolicy`, the protocol client's `ClientRegistration` and `OpenIdClientOptionsValidator` refuse `none`
   or `RSA1_5` by name even when a host registers its own. The provider never advertises or accepts them, and the
   protocol client never decrypts with them or accepts an unsigned token. The guards' tests register a viable algorithm
   beside a control (for `RSA1_5`, an `RSA-OAEP-256` control), so each passes only through the name rule.
5. A consumer with a legacy need implements its own `SignatureAlgorithm` or `KeyManagementAlgorithm` and registers it
   through `ICollectionDataSource<Algorithm>`. The vocabulary stays open
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)); the registration is a global choice and that
   consumer's explicit risk.
6. `ISecretKeyFactory.Empty` stays: it is a general primitive, not part of any algorithm.

### A failed JWE decryption reveals no cause

The rule applies to the compact serialization and to every recipient of the JSON serialization (RFC 7516 §11.5).

1. **The key is checked first.** Before unwrapping, the recipient's key must be one of the algorithm's key types, of a
   legal size, and pass `IsKeyCompatible` ([ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md)). A
   misfit is an `ArgumentException`, a configuration error that depends only on the caller's key, never on the token.
2. **A failed unwrap substitutes a random CEK.** The content key is unwrapped with
   `KeyManagementAlgorithm.UnwrapKeyAsync` ([ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md)).
   When it writes a length other than the content encryption key's, or throws a `CryptographicException`, a
   `JoseException` or an `ArgumentException`, the CEK buffer is filled from `RandomNumberGenerator` and decryption
   proceeds. Any other exception from the unwrap, such as a key management service's transport failure or cancellation,
   propagates.
3. **The authentication tag decides.** The content encryption algorithm then fails to verify the tag, as it does for any
   tampered JWE. Every failure that depends on the encrypted key, the ciphertext or the tag is one
   `JoseEncryptionException`, "Failed to decrypt the JWE.", with no inner exception, so no cause is observable from the
   exception.

## Options considered

- **Keep either algorithm registered with a documented warning.** The library stays compatible with every peer, but the
  empty-key and padding-oracle risks remain in every default composition, and RFC 8725 §3.2 and the draft ask for the
  opposite default. Rejected.
- **Keep `none` registered and verify it only against the empty key.** The family is safe, but every direct consumer
  keeps a registered algorithm that proves nothing and that the draft asks to disable by default. Rejected.
- **An opt-in registration method.** The default is safe, but a registration method is a global switch, the draft allows
  enabling an algorithm only for the specific objects or operations that require it, nothing in the family needs one,
  and `RSA1_5` would also keep the RFC 7516 §11.5 hardening burden. Rejected.
- **Remove both.** The default and every composition are safe, the RSA key management class cannot express PKCS#1 v1.5,
  the guards keep refusing a host's own registration, and the open vocabulary leaves a legacy need to the consumer.
  Chosen.
- **Distinct failure messages for an unwrap and a tag failure.** They help debugging, but they are the oracle RFC 7516
  §11.5 forbids. Rejected.
- **One message that keeps the inner exception.** The inner exception's type and message still distinguish the causes to
  anything that logs or serializes it. Rejected.
- **A uniform throw at the unwrap without a random CEK.** The message is uniform, but the failure returns earlier than a
  tag failure, so timing distinguishes them. Rejected.
- **A random CEK on a failed unwrap and one tag-failure message.** Every cause that depends on the token fails at the
  tag, identically in message and timing. Chosen.

## Consequences

- Both algorithms that draft-ietf-jose-deprecate-none-rsa15 deprecates are absent from the library. An unsecured JWS and
  an `RSA1_5` JWE fail to decode with an unsupported-algorithm error.
- A legacy need is met by the consumer's own registered algorithm, which is a global choice and the consumer's explicit
  risk; the provider's algorithm settings and the protocol client still refuse both names.
- A tampered encrypted key, a wrong-length encrypted key and a tampered tag fail identically.
- A contributed key management algorithm needs nothing new: its unwrap failures are absorbed the same way.
- A misconfigured recipient is still diagnosed precisely by the key checks.

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)
- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md)
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md)
- [ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md)
- [ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md)
- [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md)
- RFC 3218 (preventing the million message attack on CMS)
- RFC 7516 §11.5 (timing attacks and Bleichenbacher)
- RFC 7518 §3.6, §4.2–4.3 (the `none` algorithm; RSAES-PKCS1-v1_5 and RSAES-OAEP key encryption)
- RFC 8725 §3.2 (use appropriate algorithms; avoid all RSA-PKCS1 v1.5 encryption algorithms)
- draft-ietf-jose-deprecate-none-rsa15 (deprecating `none` and `RSA1_5`)
- NIST SP 800-131A Rev. 2 (transitioning the use of cryptographic algorithms and key lengths)
- Issues #339, #379, #380
