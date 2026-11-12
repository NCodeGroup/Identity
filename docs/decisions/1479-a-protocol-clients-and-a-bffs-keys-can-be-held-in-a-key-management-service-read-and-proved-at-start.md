# 1479. A protocol client's and a BFF's keys can be held in a key management service, read and proved at start

- **Status:** Accepted (amends [ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md), [ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md), [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md), [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md), [ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md), [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md); amended by
  [ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md):
  a BFF host registers the providers without a key-creation location)
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A protocol client's key ring is a keyed `ISecretKeyCollectionProvider`
([ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)), and a BFF loads its
key rings from PEM files
([ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md)). The private halves of the
client-assertion, DPoP and decryption keys therefore live in the memory of every replica.

A key management service (KMS) already holds a tenant's signing and encryption keys, and the key's row kind gives each
its use
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md),
[ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md),
[ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md),
[ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)).
The secret generator reads a bound key's public half and the management API proves that the key signs or decrypts
([ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md),
[ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)).
Each of those reads and proves a key in its own code.

A keyed provider is resolved synchronously, so a key that is read from a KMS when the provider is first resolved blocks
on I/O and surfaces a broken key at the first login. The orphaned-key sweep destroys only keys that carry its
deployment's tag
([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)).

## Decision

**A key ring entry can name a key that a KMS holds. One resolver reads such a key for the secret generator, the
serializer and every key ring, one prover proves it for the management API and the BFF, and a BFF reads and proves
every key ring when the host starts.**

1. **One resolver.** `IRemoteKeyResolver` (`NCode.Identity.Secrets.Abstractions`, namespace
   `NCode.Identity.Secrets.RemoteKeys`; `DefaultRemoteKeyResolver` in `NCode.Identity.Secrets`, registered by
   `AddSecretsLibrary`) has three members:
   - `RemoteKeyResolution Resolve(RemoteKeyAlgorithmSelector selector)` finds, without a KMS call, a secret type whose
     descriptor is an `IRemoteSecretKeyTypeDescriptor`, the registered provider and its row of any kind.
   - `ValueTask<SecretKey> ReadKeyAsync(RemoteKeyBinding binding, KeyMetadata metadata, CancellationToken)` reads the
     `SubjectPublicKeyInfo` with `GetPublicKeyAsync` and calls `CreateKey`.
   - `SecretKey CreateKey(RemoteKeyBinding binding, KeyMetadata metadata, ReadOnlySpan<byte> subjectPublicKeyInfo)`
     creates the `IRemoteSecretKey` through the descriptor's `CreateRemoteKey`.

   The row decides the key's use: `CreateKey` returns the key with
   `metadata with { Use = binding.Algorithm.Use, Algorithm = binding.Algorithm.Algorithm }`. The public
   `RemoteKeyResolution { Provider, Algorithm }` has `RemoteKeyBinding Bind(string keyId)`. The secret generator's
   create and bind, the serializer's stored remote secrets, every BFF key ring and a host that composes a client key
   ring in code use the resolver, so one path resolves and reads a remote key.
2. **One refusal signal.** `RemoteKeyRefusedException` (sealed, `Exception`, `NCode.Identity.Secrets.Abstractions`,
   namespace `NCode.Identity.Secrets.RemoteKeys`) is what the resolver, the generator and the prover throw for a
   refused request. A provider's documented `ArgumentException`, `CryptographicException` and `IOException` from
   `GetPublicKeyAsync` propagate unwrapped. The management API's `400` mapping catches `RemoteKeyRefusedException`.
   The serializer wraps a refusal in its `InvalidOperationException` as
   `The secret '{secretId}' cannot be read as a remote key: {refusal message}`.
3. **The resolver's messages.**
   - `The '{type}' secret type cannot be held in a key management service.`
   - `The remote key provider '{provider}' is not registered.`
   - `The remote key provider '{provider}' has no algorithm '{alg}' for the '{type}' secret type, key size {bits} and
     curve '{curve}'.`
   - An unreadable half (`CryptographicException` or `FormatException` from `CreateRemoteKey`, kept as the inner
     exception): `The remote key provider '{provider}' returned a public key for the key '{keyId}' that cannot be read
     as a '{type}' key.`
   - A mismatch (`ArgumentException`, kept as the inner exception): `The key '{keyId}' of the remote key provider
     '{provider}' does not have the key size {bits} and curve '{curve}' of its row '{alg}'.`, with the row's values.
4. **One proof.** `IRemoteKeyProver` (`NCode.Identity.Jose.Abstractions`, namespace `NCode.Identity.Jose`;
   `DefaultRemoteKeyProver` in `NCode.Identity.Jose`, registered by `AddJoseLibrary`) has
   `ValueTask ProveAsync(SecretKey secretKey, CancellationToken)`. The key must be an `IRemoteSecretKey`
   (`ArgumentException` otherwise), and its row's `alg` selects the proof:
   - A `SignatureAlgorithm` signs 32 random bytes through the KMS, and `SignAsync` verifies the signature with the
     key's public half.
   - A `KeyManagementAlgorithm` decrypts 32 random bytes that are encrypted to the half as a compact JWE (`A256GCM`).

   A failed proof (`JoseException`, `CryptographicException` or a payload that differs) and an `alg` that is no
   registered signature or key-management algorithm throw `RemoteKeyRefusedException`; any other exception
   propagates. The management importer and the BFF use the prover, so the proof logic exists once.
5. **Remote key providers register on the secrets library.** `Add<Vendor>RemoteKeyProvider` and
   `Add<Vendor>RemoteKeyProviderIfConfigured` extend `IServiceBuilder<SecretsLibrary>`, because the port and its
   catalogs are secrets-library services and a BFF composes no persistence library. The key wrappers stay on
   `IServiceBuilder<SecretPersistenceLibrary>`. The section is `SecretProtection:RemoteKeyProviders:<Vendor>` in every
   host, so one provider has one section; a `Bff:` child is not used because `Bff` binds strictly and the child would
   give one provider two names. The OP host calls the methods on `identityServer.SecretsLibraryBuilder`, and the
   management console's host on `managementConsole.IdentityBffBuilder.SecretsLibraryBuilder`.
6. **The protocol client needs no library change.** `PrivateKeyJwtClientAuthenticator` and `DefaultDPoPProofService`
   sign through `SignatureAlgorithm.SignAsync`, the provider-JWT validator decrypts through the asynchronous JWE path,
   and the credential selector matches a key's metadata `Use` and `Algorithm`, which the row sets. A host that
   composes the client in code reads each key with `IRemoteKeyResolver` and proves it with `IRemoteKeyProver` before
   its key ring is first resolved.
7. **BFF key ring entries.** `Bff:KeyRings:{name}:RemoteKeys:{kid}` binds
   `BffRemoteKeyOptions { Provider, KeyId, Algorithm, SecretType, KeySizeBits, Curve }`. The entry's name is the
   `kid`, as a PEM file's name is. All five selector fields are explicit, because an RSA algorithm fits several sizes.
   A key ring lists PEM files, remote keys or both, at least one key, and no id twice. The options validator fails
   startup with one message per problem:
   - `Bff:KeyRings:{name} must list at least one PEM file or remote key.`
   - `Bff:KeyRings:{name} holds more than one key with the id '{id}'.`
   - `Bff:KeyRings:{name}:RemoteKeys has a key without an id.`
   - `Bff:KeyRings:{name}:RemoteKeys:{kid}:Provider must name a remote key provider: lowercase letters and digits in
     groups separated by single hyphens.`
   - `Bff:KeyRings:{name}:RemoteKeys:{kid}:KeyId must not be blank.` (likewise `Algorithm` and `SecretType`)
   - `Bff:KeyRings:{name}:RemoteKeys:{kid}:KeySizeBits must be positive.`

   Whether the provider is registered and has the row is checked when the ring loads (rule 8).

   The row decides each remote key's use, so one client-authentication ring holds a signing key and a decryption key
   side by side, and `/bff/jwks` publishes both halves with their `use`.
8. **Every key ring loads when the host starts, and a failure stops the host.** `BffKeyRingLoader`
   (`IHostedLifecycleService.StartingAsync`) loads every `Bff:KeyRings` entry into `BffKeyRings`, and the keyed
   `ISecretKeyCollectionProvider` that `AddBffConfiguration` registers per entry returns the loaded ring. A remote key
   is resolved, read and proved before its ring loads, so a key that the deployment's identity cannot read or use
   stops the host with `The BFF cannot load the remote key '{kid}' of the key ring '{ring}' from the remote key
   provider '{provider}': {reason}`, with the cause as the inner exception. PEM keys load at start too, so a malformed
   file stops the host rather than the first login. A ring loads once per process, so a rotation takes effect on
   restart. A ring's keyed provider resolves once `StartingAsync` has run: a service that resolves it earlier, such as
   an eager singleton or an options validator, gets an `InvalidOperationException` that names the ring. The BFF
   resolves a ring only when a request uses it, and the set check of
   [ADR-1446](1446-a-set-of-frontends-that-names-an-unregistered-key-ring-is-refused.md) asks only whether the ring is
   registered (`IClientKeyRingProvider.HasKeyRing`), so nothing resolves a ring before it loads.
9. **A key ring's KMS key is the operator's.** Neither the BFF nor the protocol client creates, tags or destroys a KMS
   key, so neither needs `SecretProtection:RemoteKeys:Deployment`, and no stored secret names a key ring's key: it is
   not a reference that `IRemoteKeyReferenceStore` reads. The sweep lists only keys whose group name begins `ncid-`
   and that carry its deployment's tag
   ([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md) rules 1
   and 2), so a key ring's key is never swept unless the operator gives it both. The operator gives it neither, and
   keeps it in a scope of its own, as for a bound tenant key
   ([ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)).
10. **DPoP keys can be KMS-held by the same entries.** The vocabulary is not closed by use. Every DPoP proof, each
    token request and each forwarded call with a DPoP-bound token, is a KMS signature (a round trip, a fee and the
    service's quota), whereas a client-authentication key signs once per token request. An operator holds the
    client-authentication key and the decryption key in the KMS, and keeps the DPoP key local unless the threat model
    needs it and the request rate fits the quota. A per-session ephemeral DPoP key never goes to a KMS.
11. **Permissions for a key ring's remote key.** AWS needs `kms:GetPublicKey` plus `kms:Sign`, `kms:Decrypt` or
    `kms:DeriveSharedSecret` as the row says. Azure needs `Key Vault Crypto User` on the key. Google needs
    `cloudkms.cryptoKeyVersions.viewPublicKey` plus `.useToSign` or `.useToDecrypt`.

## Options considered

- **The public half in configuration.** Rejected: the operator exports and pastes it, and a mismatch shows only at the
  first use.
- **Lazy loading on first use.** Rejected: it blocks on I/O, and a broken key surfaces at the first login.
- **Configuration-driven key rings in the protocol client package.** Rejected: keyed dependency injection is the
  library's seam, and the turnkey BFF owns configuration.
- **A `Bff:RemoteKeyProviders` section.** Rejected: it gives one provider two names (rule 5).
- **No start-time proof.** Rejected: a missing sign or decrypt permission surfaces only at the first login.
- **Key ring keys restricted to signing.** Rejected: the row decides the use, and the client decrypts with its ring.
- **Recording key ring keys for the sweep.** Rejected: the BFF has no secret store, and the keys are the operator's.

## Consequences

- A replica needs its KMS reachable to start.
- Each start costs one public-key read and one proof per remote key.
- Each token request with a remote client key costs one KMS signature, each DPoP proof with a remote DPoP key another,
  and each encrypted token a KMS decryption or agreement.
- JWKS needs no KMS call.
- A key ring change takes effect on restart.
- An operator keeps key ring keys out of the `ncid-` groups and the deployment's tag.

## References

- [ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md)
- [ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)
- [ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md)
- [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)
- [ADR-1446](1446-a-set-of-frontends-that-names-an-unregistered-key-ring-is-refused.md)
- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md)
- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)
- [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)
- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)
- RFC 7523 §2.2
- RFC 9449
- Issue [#651](https://github.com/NCodeGroup/identity/issues/651)
