# 1474. A remote key provider is described by data, creates a key once and signs into the caller's buffer

- **Status:** Accepted (amends [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

[ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) defines the
remote key provider contract and its vendor packages. Five of its rules (2, 7, 8, 16 and 17) leave these forces:

- **Validators reach providers.** The setting data source and `DefaultTenantKeyPolicy` ask
  `IRemoteKeyProviderCatalog` which algorithms a provider holds, so code that only validates a setting resolves
  providers, and a provider's constructor can read options, build a client or call the network.
- **A creation that is retried or fails after the service acted leaves a key.** An SDK that retries a key-creation call
  can create two keys, and a provider that creates a key and then fails a check (usage, spec, public key, size, curve)
  or is cancelled drops the identifier of a key it created. A key that no record names is never destroyed.
- **Exception types are mixed.** A response that fails an integrity check and a key the provider refuses surface as
  the same exception type, so a caller cannot tell a call worth repeating from a key worth refusing.
- **Signatures are returned as `byte[]`.** Each signature allocates an array that the caller then copies into its own
  buffer.
- **A hash sits in an abstractions type.** The group name's SHA-256 is computed in `NCode.Identity.Secrets.Abstractions`,
  which holds contracts and data.

## Decision

**A remote key provider is described by data that a source publishes, creates a key in one unretried call and returns
its identifier before anything else can fail, and signs into the buffer the caller supplies. The server destroys every
key it created and could not keep.**

1. **Providers and rows are described by data (`IRemoteKeyDescriptorSource`, `IRemoteKeyDescriptorCatalog`).**
   `IRemoteKeyDescriptorSource` exposes `Providers` (`RemoteKeyProviderDescriptor`, which has a `Name`) and
   `Algorithms` (`RemoteSigningAlgorithm` rows); a package registers one so anyone adds rows, and a source may describe
   no provider. `IRemoteKeyDescriptorCatalog` is built from sources only and exposes `Providers` (ordinal name order)
   and `TryGetAlgorithm`. `IRemoteKeyProviderCatalog` exposes only `TryGetProvider`, and its construction fails when the
   registered providers and the described providers differ. The setting data source and `DefaultTenantKeyPolicy` read
   the descriptor catalog, so a validator never constructs a provider. A provider's constructor does no I/O, reads no
   options value and creates no client; a provider documents this on its type.
2. **Creation returns the identifier, and a second call reads the key (`CreateKeyAsync`, `GetPublicKeyAsync`).**
   `IRemoteKeyProvider.CreateKeyAsync` returns `RemoteKeyCreationResult` (`KeyId`) as soon as the service reports the
   key. `GetPublicKeyAsync(RemotePublicKeyRequest, IBufferWriter<byte>, CancellationToken)` reads the public half into
   the caller's writer and checks it against the row. `DefaultSecretGenerator` hands the created key to
   `IRemoteSecretDestroyer` when anything after creation fails: a provider check, a read, cancellation, or its own size
   and curve check. A provider never destroys a key by itself, so destruction stays in one bounded, logged place.
3. **A key-creation call is made exactly once.** The AWS client is configured with `MaxErrorRetry = 0` for creation,
   the Azure `KeyClient` with `MaxRetries = 0`, and Google `GoogleCloudKmsCallSettings.Creating` allows one attempt.
   A repeatable call (read, sign, destroy) keeps the SDK retry of ADR-1471 rule 16. A failed creation fails its seed or
   rollover pass, which runs again later.
4. **A failed integrity check is an `IOException`, a refused key is a `CryptographicException`.** An `IOException`
   ("the call can be retried") is thrown for a response that fails an integrity check: a checksum mismatch, or a
   response that names a key, a key version or an algorithm other than the one requested. A `CryptographicException` is
   thrown for a key the provider refuses: its usage, spec, algorithms or key type, a missing or malformed public key, or
   a malformed created identifier. This is the rule the key wrappers follow
   ([ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md) rule 4).
5. **Signing writes into the caller's buffer.**
   `IRemoteKeyProvider.SignAsync(RemoteSignatureRequest, Memory<byte> signature, CancellationToken)` returns a
   `ValueTask<int>` and writes the service's bytes into `signature`. `RemoteKeySigning` hashes the signing input into a
   rented buffer, passes the caller's buffer for a row whose signature is not DER and a rented buffer for a DER row
   (`BufferFactory.Rent` with `isSensitive: false`, [ADR-1116](1116-memory-allocations-and-buffer-operations.md)).
   `EcdsaSignatureConverter` parses the DER signature from a span with `AsnDecoder`. The AWS provider passes an
   array-backed input without copying (`MemoryMarshal.TryGetArray`); the Azure provider copies the digest because its
   SDK takes an array of exactly the digest's length.
6. **The group name is computed in `NCode.Identity.Secrets.Persistence`.** `RemoteKeyGroupName.Create` computes the
   group name of ADR-1471 rule 2, and `RemoteKeyCreationRequest.GroupName` is required data that the caller supplies.
   `RemoteKeyReference` keeps its text form (parse, format and name check): a value type's own wire form is data, as
   `SecretOwner.TryParse` is.

## Options considered

- **A provider destroys the key it refuses.** Rejected: each of the three providers would carry its own copy of bounded,
  logged destruction, and a provider cancelled after creation would still lose the identifier.
- **An exception that carries the created identifier.** Rejected: each provider must remember to wrap every failure
  after creation, and a failure it misses leaves a key.
- **Retry only throttling on creation.** Rejected: the AWS and Azure SDKs cannot limit retry to throttling for one
  operation without replacing their pipeline.
- **One catalog for descriptors and providers.** Rejected: the consumers of data would construct providers.

## Consequences

- A throttled creation fails its seed or rollover pass, and the next pass creates the key.
- Azure creation costs one more read (`GetKey`), because creation and the public-key read are two phases.
- A key that the server created and could not keep is destroyed in one place, whichever provider created it.
- A signature written into the caller's buffer allocates no array for a non-DER row.
- A setting or a policy is validated without constructing a provider.

## References

- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)
- [ADR-1116](1116-memory-allocations-and-buffer-operations.md)
- Issues #653, #655, #656, #657, #659, #663, #664
