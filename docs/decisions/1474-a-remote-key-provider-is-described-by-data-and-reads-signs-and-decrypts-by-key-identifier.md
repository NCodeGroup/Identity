# 1474. A remote key provider is described by data and reads, signs and decrypts by key identifier

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

[ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md) holds a tenant's keys
in a key management service through vendor packages. This record defines the provider contract those packages
implement, and these forces shape it:

- **Validators reach providers.** The setting data source and `DefaultTenantKeyPolicy` ask which algorithms a provider
  holds, so code that only validates a setting would resolve providers, and a provider's constructor could read
  options, build a client or call the network.
- **A creation that is retried or fails after the service acted leaves a key.** An SDK that retries a key-creation call
  can create two keys, and a provider that creates a key and then fails a check (usage, spec, public key, size, curve)
  or is cancelled drops the identifier of a key it created. A key that no record names is never destroyed.
- **Exception types must tell a retry from a refusal.** A response that fails an integrity check is worth repeating; a
  key the provider refuses is not.
- **Signatures are returned as arrays.** Each signature would allocate an array that the caller then copies into its own
  buffer.
- **A hash does not belong in an abstractions type.** `NCode.Identity.Secrets.Abstractions` holds contracts and data.
- **A location serves creation only.** Each vendor has a key-creation location: `Region` for AWS KMS, `VaultUri` for
  Azure Key Vault and `KeyRing` for Google Cloud KMS. A provider uses it only to create and list keys; it reads, signs
  with, decrypts with and destroys a key where the key's own identifier names it. A BFF host only reads the keys its key
  rings name and proves them when it starts
  ([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)),
  so it has no location to name. A server whose identity lacks `kms:CreateKey` and `ListKeys` binds keys an operator
  created and never creates one. Every consumer that creates, lists or sweeps a provider's keys (the
  `tenant_key_provider` domain, the tenant key policy's start check, the secret generator, the sweep service and job,
  the management API's create) must know whether the provider can.

## Decision

**A remote key provider is described by data that a source publishes, creates a key in one unretried call and returns
its identifier before anything else can fail, signs into the buffer the caller supplies, and creates and sweeps keys
only when its descriptor says it has a key-creation location. The server destroys every key it created and could not
keep.**

1. **The contract is `IRemoteKeyProvider` (`NCode.Identity.Secrets.RemoteKeys`).** `CreateKeyAsync` answers
   `RemoteKeyCreationResult { KeyId }` as soon as the service reports the key.
   `GetPublicKeyAsync(RemotePublicKeyRequest, IBufferWriter<byte>, CancellationToken)` reads the public half into the
   caller's writer and checks usage, spec and algorithm against the row. `SignAsync`, `DecryptAsync` and
   `DeriveSharedSecretAsync` write into the caller's `Memory<byte>` and return `ValueTask<int>`. `DestroyKeyAsync`
   completes for a missing, disabled or scheduled key, and `ListKeysAsync` lists the keys a sweep considers
   ([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)). A
   provider that lacks an operation throws `NotSupportedException`, and no row selects it. A provider never destroys a
   key itself, so destruction stays in one bounded, logged place.
2. **Providers and rows are data.** `IRemoteKeyDescriptorSource` publishes `Providers`
   (`RemoteKeyProviderDescriptor { Name, CreatesKeys }`) and `Algorithms`; a package registers one so anyone adds rows,
   and a source may describe no provider. `IRemoteKeyDescriptorCatalog` is built from sources only and exposes
   `Providers` in ordinal name order, `Algorithms`, `TryGetAlgorithm` and the extension members `CreatesKeys` and
   `ProvidersThatCreateKeys`. `IRemoteKeyProviderCatalog` exposes only `TryGetProvider`, and its construction fails when
   the registered providers and the described providers differ.
3. **Validation never constructs a provider.** Every validator and the capabilities read the descriptor catalog: the
   setting data source, `DefaultTenantKeyPolicy`, and the management API's `DefaultSecretValidator` and
   `remoteKeyProviders` capability. A provider's constructor does no I/O, reads no option and creates no client; a
   provider documents this on its type.
4. **The descriptor catalog fails at construction** on an invalid provider name (`RemoteKeyProviderNames` follows the
   rule of `KeyWrapperNames`), a duplicate name, or two rows with one selector. Rows of an unregistered provider are
   kept.
5. **Rows are `RemoteKeyAlgorithm`** (`Provider`, `Algorithm`, `SecretType`, `KeySizeBits`, `Curve`, `KeySpec`, `Use`,
   `Selector`) in three sealed kinds: `RemoteSigningAlgorithm` (`sig`; `SigningAlgorithm`, `DigestAlgorithm` or null,
   `IsDerEncodedSignature`), `RemoteDecryptionAlgorithm` and `RemoteKeyAgreementAlgorithm` (`enc`). The kinds are closed
   because each is one provider operation; rows stay open, and `RemoteKeyAlgorithmSelector` selects ordinally.
6. **The location is optional and says whether the provider creates keys.** Each vendor's options validator checks
   `Region`, `VaultUri` or `KeyRing` only when it is set, and null or empty means unset; every other option keeps its
   check. `RemoteKeyProviderDescriptor.CreatesKeys` is `true` by default, and each vendor source takes the vendor's
   options and sets it to whether the location is set. The catalog reads the sources when it is built, and the options
   are bound from configuration and checked by a validator that reads no service, so a consumer that resolves the
   catalog inside its own options validation builds them without a cycle. Without a location a provider works by key
   identifier, and its `CreateKeyAsync` and `ListKeysAsync` throw `InvalidOperationException`.
7. **Only a provider that creates keys creates, lists or sweeps.** The `tenant_key_provider` domain lists only those
   providers, and the tenant key policy's start check covers only them. `DefaultSecretGenerator` refuses any other with
   `RemoteKeyRefusedException` before it calls the provider. The remote key options validator requires `Deployment` only
   when such a provider is described. The sweep service dispatches, and the sweep job sweeps, only them. The management
   API's create refuses any other with `400`, and its capabilities carry `createsKeys` for each provider. One refusal
   text serves every consumer, `RemoteKeyRefusals.CreatesNoKeys`: "The remote key provider '{name}' creates no keys: it
   has no key-creation location." Binding works in any registered provider.
8. **A key-creation call is made exactly once.** The AWS client is configured with `MaxErrorRetry = 0` for creation,
   the Azure `KeyClient` with `MaxRetries = 0`, and Google `GoogleCloudKmsCallSettings.Creating` allows one attempt. A
   repeatable call (read, sign, decrypt, destroy) keeps the SDK's explicit retry: AWS `Standard` with 3 attempts, Azure
   exponential with 3 retries from 500 ms to 8 s, Google 4 attempts with jitter. The family adds no retry layer. A failed
   creation fails its seed or rollover pass, which runs again later.
9. **Creation hands the key on before anything can fail.** `DefaultSecretGenerator` hands a created key to
   `IRemoteSecretDestroyer` when anything after creation fails: a provider check, a read, cancellation, or its own size
   and curve check.
10. **A failed integrity check is an `IOException`, a refused key is a `CryptographicException`.** An `IOException`
    ("the call can be retried") is thrown for a response that fails an integrity check: a checksum mismatch, or a
    response that names a key, a key version or an algorithm other than the one requested. A `CryptographicException` is
    thrown for a key or ciphertext the provider refuses: its usage, spec, algorithms or key type, a missing or malformed
    public key or identifier. An unreadable key identifier is an `ArgumentException`. This is the rule the key wrappers
    follow ([ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md)).
11. **Signing writes into the caller's buffer.** `RemoteKeySigning` hashes the signing input into a rented buffer
    (`BufferFactory.Rent` with `isSensitive: false`,
    [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)),
    writes a row whose signature is not DER straight into the caller's buffer, and converts a DER ECDSA signature to
    `r‖s` (halves of `(KeySizeBits + 7) / 8` bytes) through the strict `EcdsaSignatureConverter`, which parses with
    `AsnDecoder`. The AWS provider passes an array-backed input without copying (`MemoryMarshal.TryGetArray`); the Azure
    provider copies the digest because its SDK takes an array of exactly the digest's length.
12. **The group name is computed in `NCode.Identity.Secrets.Persistence`.** `RemoteKeyGroupName.Create` (internal)
    computes the required `RemoteKeyCreationRequest.GroupName`: `ncid-` plus the hex of 16 bytes of SHA-256 over the
    deployment, owner, provider, algorithm, spec, size and curve. `RemoteKeyReference` keeps its text form (parse, format
    and name check): a value type's own wire form is data, as `SecretOwner.TryParse` is.
13. **Registration is `Add<Vendor>RemoteKeyProvider` and `Add<Vendor>RemoteKeyProviderIfConfigured`,** extending
    `IServiceBuilder<SecretsLibrary>` and binding `SecretProtection:RemoteKeyProviders:<Vendor>` strictly with
    `ValidateOnStart`, reusing the wrapper's client factory. The OP host registers a vendor only when its section
    exists, because the section is the operator's consent to reach that service with the deployment's identity. The
    console host registers every vendor unconditionally, because the provider does no I/O and reads each key where the
    key's identifier names it.

## Options considered

- **A provider destroys the key it refuses.** Rejected: each of the three providers would carry its own copy of bounded,
  logged destruction, and a provider cancelled after creation would still lose the identifier.
- **An exception that carries the created identifier.** Rejected: each provider must remember to wrap every failure
  after creation, and a failure it misses leaves a key.
- **Retry only throttling on creation.** Rejected: the AWS and Azure SDKs cannot limit retry to throttling for one
  operation without replacing their pipeline.
- **One catalog for descriptors and providers.** Rejected: the consumers of data would construct providers.
- **A required location.** Rejected: a host that only reads keys has no location to give, and a value that only
  satisfies a validator can disagree with the keys.
- **An `Enabled` option.** Rejected: a second switch beside the location that can disagree with it, and a provider with
  a location and `Enabled` off is a state no consumer needs.
- **The provider reading the location.** Rejected: a provider that reads an option in its constructor breaks the
  rule that validation never constructs one, so its source reads the option and the descriptor carries the result.
- **The chosen design:** descriptors from sources, an optional location that sets `CreatesKeys`, one unretried
  creation, and signing into the caller's buffer.

## Consequences

- A throttled creation fails its seed or rollover pass, and the next pass creates the key.
- Azure creation costs one more read (`GetKey`), because creation and the public-key read are two phases.
- A key that the server created and could not keep is destroyed in one place, whichever provider created it.
- A signature written into the caller's buffer allocates no array for a non-DER row.
- A setting, a policy or a management request is validated, and the capabilities list the providers, without
  constructing a provider.
- A least-privilege identity binds a key a KMS already holds without `kms:CreateKey`.
- A provider that loses its location leaves the keys it created unswept.
- A `tenant_key_provider` that names a provider without a location is refused as an unregistered one.
- A BFF host registers the providers without a section.
- `GetPublicKeyAsync` reads the public half of any key a provider holds, which binding a key reads in place of a
  supplied public half
  ([ADR-1476](1476-the-management-api-creates-a-tenants-remote-key-and-binds-one-only-after-proving-it.md)).

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)
- [ADR-1260](1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md)
- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)
- `NCode.Identity.Secrets.AwsKms`, `NCode.Identity.Secrets.AzureKeyVault`, `NCode.Identity.Secrets.GoogleCloudKms`
