# 1477. A tenant's encryption keys can be held in a key management service that decrypts every content key

- **Status:** Accepted (amends [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md), [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md), [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md), [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md), [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md); amended by [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

[ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) keeps a
tenant's signing private keys in a key management service (KMS) so that no replica holds one. An encryption private key
is as sensitive as a signing key, so these forces hold:

- **A replica holds every local encryption private key it loads.** A replica that loads a tenant's encryption private
  key can leak it, as it can a signing key, and every replica that decrypts a JWE for the tenant holds it.
- **JWE decryption is synchronous and span-based.** `KeyManagementAlgorithm.TryUnwrapKey`, the serializer's decode
  members and the JWT validator take a key that is in memory and complete without I/O, so a call to a service has no
  place in them.
- **The services differ.** AWS KMS decrypts RSA-OAEP and derives ECDH shared secrets, Azure Key Vault unwraps RSA-OAEP
  content keys, and Google Cloud KMS decrypts RSA-OAEP and agrees no keys.
- **Custody is one choice for every key.** One tenant setting chooses where a tenant's keys are held, signing and
  encryption alike, because a split by use adds vocabulary for a choice no threat model needs.
- **The management API and the console bind by row.** A create or bind names a provider's algorithm row, the server
  reads a bound key's public half from the KMS and proves the key by its algorithm's kind (a signature or a
  decryption), and the capabilities and the
  console offer each provider's accepted rows
  ([ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)).

## Decision

**A tenant's encryption keys can stay in a KMS like its signing keys: the service unwraps the content key of every JWE
sent to the tenant (RSA-OAEP decryption, or ECDH key agreement where the service offers it), decryption is asynchronous
to every caller, and one tenant setting chooses the custody of every provisioned key.**

1. **Rows are a family of data (`RemoteKeyAlgorithm`).** `RemoteKeyAlgorithm` is an abstract record (`Provider`,
   `Algorithm`, `SecretType`, `KeySizeBits`, `Curve`, `KeySpec` and an abstract `Use`) with three sealed kinds:
   `RemoteSigningAlgorithm` (`Use` `sig`; `SigningAlgorithm`, `DigestAlgorithm`, `IsDerEncodedSignature`),
   `RemoteDecryptionAlgorithm` (`Use` `enc`; `EncryptionAlgorithm`) and `RemoteKeyAgreementAlgorithm` (`Use` `enc`;
   `KeyAgreementAlgorithm`). The kinds are closed by the provider contract, because each kind is one
   `IRemoteKeyProvider` operation ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)'s closed test); the rows stay open
   data that a source registers. `RemoteKeyAlgorithmSelector` selects any row. `RemoteKeyAlgorithm.Selector` gives a
   row's selector, and `IRemoteKeyDescriptorCatalog.Algorithms` lists the rows of every kind. The descriptor source,
   the catalog, `RemoteKeyBinding`, `RemoteKeyCreationRequest` and `RemotePublicKeyRequest` carry `RemoteKeyAlgorithm`.
   `RemoteSigningAlgorithm` keeps its name as the signing kind.
2. **The provider decrypts and agrees into the caller's buffer.**
   `IRemoteKeyProvider.DecryptAsync(RemoteDecryptionRequest { KeyId, Algorithm, Ciphertext }, Memory<byte> plaintext,
   CancellationToken)` and `DeriveSharedSecretAsync(RemoteKeyAgreementRequest { KeyId, Algorithm,
   PeerSubjectPublicKeyInfo }, Memory<byte> sharedSecret, CancellationToken)` return a `ValueTask<int>` and write the
   service's bytes into the caller's buffer. A service that refuses the ciphertext is a `CryptographicException`, so
   the JWE fails at its tag ([RFC 7516](https://www.rfc-editor.org/rfc/rfc7516) §11.5). A response that names another key
   or algorithm, or fails a checksum, is an `IOException`
   ([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
   rule 4). A provider whose service has no such operation throws `NotSupportedException`, and its rows never select
   it. Creation uses the kind's key usage (AWS `SIGN_VERIFY`, `ENCRYPT_DECRYPT` or `KEY_AGREEMENT`; Azure key
   operations `sign` and `verify`, or `unwrapKey`; Google purpose `ASYMMETRIC_SIGN` or `ASYMMETRIC_DECRYPT`), and
   `GetPublicKeyAsync` checks it.
3. **Each service holds these rows.**

   | Service | Rows | Key specs |
   | --- | --- | --- |
   | AWS KMS | `RSA-OAEP` (`RSAES_OAEP_SHA_1`), `RSA-OAEP-256` (`RSAES_OAEP_SHA_256`) | `RSA_2048`, `RSA_3072`, `RSA_4096` |
   | AWS KMS | `ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW`, `ECDH-ES+A256KW` (`ECDH`, `DeriveSharedSecret`) | `ECC_NIST_P256`, `ECC_NIST_P384`, `ECC_NIST_P521` |
   | Azure Key Vault | `RSA-OAEP`, `RSA-OAEP-256` (`UnwrapKey`) | RSA 2048, 3072, 4096 |
   | Google Cloud KMS | `RSA-OAEP` (`RSA_DECRYPT_OAEP_{n}_SHA1`), `RSA-OAEP-256` (`RSA_DECRYPT_OAEP_{n}_SHA256`) | 2048, 3072, 4096 |
   | Google Cloud KMS | `RSA-OAEP-512` (`RSA_DECRYPT_OAEP_4096_SHA512`) | 4096 |

   Azure Key Vault and Google Cloud KMS agree no keys, so they hold no `ECDH-ES` key. Azure uses `UnwrapKey` rather
   than `Decrypt`: unwrapping a content key is the operation, and the key is created with `unwrapKey` only.
4. **Remote keys decrypt and agree through the service.** `IRemoteSecretKey` has
   `DecryptAsync(string algorithm, ReadOnlyMemory<byte> ciphertext, Memory<byte> plaintext, CancellationToken)` and
   `DeriveSharedSecretAsync(string algorithm, ReadOnlyMemory<byte> peerSubjectPublicKeyInfo, Memory<byte> sharedSecret,
   CancellationToken)`. An RSA remote key signs or decrypts and an ECC remote key signs or agrees, as its row's kind
   says. Each refuses an algorithm other than its row's and a kind it does not hold (`InvalidOperationException`). The
   factory refuses an RSA key bound to an agreement row and an ECC key bound to a decryption row
   (`ArgumentException`).
5. **A row's `Use` is the key's use.** The generator gives a created or bound remote key the `Use` of its row, so
   `GenerateRemoteSecretRequest` and `BindRemoteSecretRequest` name no use. The serializer refuses a stored remote
   secret whose `Use` differs from its row's, the provisioning check refuses a provider whose row for a provisioned key
   holds another use, and the management validator refuses a row whose use differs from the use its algorithm implies,
   so a remote encryption key carries and publishes `use` `enc`.
6. **JOSE unwraps a content key asynchronously.**
   `KeyManagementAlgorithm.UnwrapKeyAsync(SecretKey, JsonElement header, ReadOnlyMemory<byte> encryptedContentKey,
   Memory<byte> contentKey, CancellationToken)` returns a `ValueTask<int>`. Its default refuses a remote key
   (`JoseInvalidAlgorithmException`) and otherwise calls `TryUnwrapKey` behind a completed `ValueTask`. The RSA-OAEP
   family delegates to the remote key's `DecryptAsync`. `ECDH-ES` delegates the agreement (the `epk` as a
   `SubjectPublicKeyInfo`) and runs the Concat KDF locally ([RFC 7518](https://www.rfc-editor.org/rfc/rfc7518) §4.6),
   and `ECDH-ES+A*KW` also runs the AES key unwrap locally. The synchronous `TryUnwrapKey` of these algorithms refuses a
   remote key, so COSE (synchronous,
   [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md) rule 4) never sends one to a
   service. The JWE's tag authenticates every remote result.
7. **Decryption is asynchronous to every caller, as signing is.** `IJoseSerializer.DecodeAsync` and `DeserializeAsync`
   (each with a `CancellationToken`) are the decode members, a JWS decode completes synchronously behind its
   `ValueTask`, and `VerifyJws` stays synchronous. The serializer decodes every binary segment before the unwrap, so a
   malformed token never reaches a service. A `CryptographicException`, `JoseException` or `ArgumentException` from an
   unwrap substitutes a random content key ([RFC 7516](https://www.rfc-editor.org/rfc/rfc7516) §11.5), and any other
   exception (transport, `IOException`, cancellation) propagates.
8. **The JWT validator decrypts asynchronously and tries the key the header names.** When the JWE header names a `kid`
   that an eligible decryption key has, the validator tries only the keys with that id, so a well-addressed token costs
   at most one service call per key.
9. **One setting chooses the custody of every provisioned key.** The tenant setting `tenant_key_provider`
   (`OpenIdSettingKeys.TenantKeyProvider`, data source `TenantKeyProviderSettingDescriptorDataSource`) chooses the
   provider of every provisioned key, signature and encryption alike. The host refuses to start when a described
   provider cannot hold a provisioned key of either use
   ([ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) rule 11
   widened). A deployment that registers Azure Key Vault or Google Cloud KMS provisions `RSA-OAEP*` encryption keys
   rather than `ECDH-ES` ones.
10. **The management API and the console create and bind encryption keys.**
    `ISecretValidator.ValidateRemoteKey(RemoteKeyAlgorithmSelector)` resolves a key for a signature or key-management
    algorithm and refuses a row whose use is not the one the algorithm implies; create and bind name no use. The
    capabilities list each provider's accepted rows of every kind with their `use`
    (`RemoteKeyAlgorithmCapabilityResource.Use`), and the console's create form and bind dialog offer them, each
    labelled signing or encryption. A bind reads the key's public half from the KMS and proves the key by its
    algorithm's kind: a signing key signs 32 random bytes that the public half verifies, and an encryption key decrypts
    32 random bytes that the server encrypts to the public half as a compact JWE (`enc` `A256GCM`) and decodes through
    the key (`IJoseSerializer.DecodeAsync`), so a key that unwraps another content key fails at the JWE's tag. A failed
    proof is `400`, and any other failure of the service is `502`.
11. **Permissions.** AWS adds `kms:Decrypt` and `kms:DeriveSharedSecret`. Azure `Key Vault Crypto Officer` is unchanged,
    and a bound key needs `Key Vault Crypto User`. Google adds `cloudkms.cryptoKeyVersions.useToDecrypt`.
12. **Security posture.** The private key never leaves the service. The ECDH shared secret of one message passes through
    the replica, never the key. Content keys, key encryption keys and shared secrets live in sensitive buffers that are
    zeroed after use, and logs carry identifiers only.

## Options considered

- **A setting per use.** Rejected: an encryption private key is as sensitive as a signing key, and a per-use split adds
  vocabulary for a choice no threat model needs.
- **Synchronous twins beside the asynchronous decode members.** Rejected for the reason
  [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md) gives: two members for one
  operation split every caller and every test.
- **Verifying a remote decryption another way.** Rejected: the AEAD tag of the JWE authenticates every result, so a
  second check adds a service call and no assurance.
- **Azure `Decrypt` in place of `UnwrapKey`.** Rejected: a key that only unwraps cannot decrypt arbitrary data.
- **Exporting a key to agree locally.** Rejected: it defeats custody, since the private key reaches a replica.
- **Wrapping and unwrapping a content key directly** (`WrapNewKey`, `UnwrapKeyAsync`). Rejected: the JWE round trip
  runs the production decode path, the `epk` header of `ECDH-ES` included, and a wrong key fails at the tag.
- **A console that offers signing keys only.** Rejected: the API accepts encryption keys, and a console that hid them
  would offer less than the capabilities say.

## Consequences

- Each encrypted token costs a service call per key tried.
- A service outage stops the decryption of tokens sent to remote keys, while local keys continue to decrypt.
- Azure and Google tenants with remote custody hold `RSA-OAEP*` encryption keys only.
- A new kind of row is one more provider operation, a deliberate change to the contract.

## References

- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1472](1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)
- [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)
- [RFC 7516](https://www.rfc-editor.org/rfc/rfc7516) §11.5
- [RFC 7518](https://www.rfc-editor.org/rfc/rfc7518) §4.3 and §4.6
- Issue [#650](https://github.com/NCodeGroup/Identity/issues/650)
