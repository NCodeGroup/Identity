# 1476. A bound KMS key's public half is read from its service, and the console offers each provider's algorithms

- **Status:** Accepted (amends [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

The management API creates a tenant's signing key in a registered remote key provider and binds a key that a key
management service (KMS) already holds
([ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)).
Every provider reads a key's public half with `IRemoteKeyProvider.GetPublicKeyAsync` and checks the key against a row
of its mapping vocabulary, and `IRemoteKeyDescriptorCatalog` holds those rows without constructing a provider
([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)).
The forces:

- **The KMS holds the public half.** A bind that takes the public half from the operator makes the operator export
  it from the KMS and paste it, and a mismatched half is found only when the key signs a challenge.
- **A row selects the key.** A JOSE algorithm such as `RS256` maps to one row per key size, and a provider checks a
  key's specification against the one row it is given.
- **Clients learn what a provider holds.** A client that knows only the providers' names offers every asymmetric
  type and signature algorithm, and learns from a `400` which ones a provider holds.
- **Operators work in the console.** The console shows which provider holds a tenant's key, and an operator creates
  and binds such keys there as well as through the API.

## Decision

**A bind names the key and its algorithm row; the server reads the key's public half from the KMS, checks it
against that row and proves the key signs before it stores it; the capabilities list each provider's algorithm
rows; and the console creates and binds a tenant's remote keys from them.**

1. **The bind request names the key, not its public half.** `ImportRemoteSecretRequest` carries
   `RemoteKeyProvider`, `RemoteKeyId`, `SecretType`, `KeySizeBits`, `Curve`, `Algorithm` and the lifecycle
   (`ActivatesWhen`, `RetiresWhen`, `ExpiresWhen`): the fields of `CreateRemoteSecretRequest` and the key
   identifier. `ISecretValidator.ValidateImportRemote` checks the lifecycle and the key identifier's length and
   characters, and it and `ValidateCreateRemote` check the rest with `ISecretValidator.ValidateRemoteKey`: a key type
   a KMS can hold, a described provider, an algorithm that resolves the key's size and curve, and a row for them, all
   before any KMS call.
2. **The generator reads the public half.** `ISecretGenerator.BindRemoteSecretAsync(BindRemoteSecretRequest)`
   finds the provider and the row, reads the key's `SubjectPublicKeyInfo` with `GetPublicKeyAsync`, which refuses a
   key whose usage, specification or signing algorithms do not match the row, and checks that the half is a key of
   the row's type, size and curve, as `GenerateRemoteSecretAsync` checks a key it created. It returns a `remote`
   secret with `Origin` `supplied` and no `ManagedBy`, so the server never destroys the key.
3. **The binding is proved.** Before the store adds the secret, the server signs 32 random bytes through the key and
   verifies the signature with the half the KMS returned (`IRemoteSecretImporter.VerifyBindingAsync`), so a key is
   stored only once the KMS signs with it for the row's algorithm under the deployment's identity.
4. **A refusal is a `400`, a failure a `502`.** A key identifier the provider does not read, a key it refuses and a
   half that does not match the row answer `400` with the reason; any other failure of the KMS is logged and
   answers `502`. The generator signals its own refusal with `RemoteSecretRefusedException`, and a provider signals a
   key identifier it does not read with `ArgumentException` and a key it refuses with `CryptographicException`, as
   `IRemoteKeyProvider.GetPublicKeyAsync` documents; no other exception, such as an `InvalidOperationException` from
   a KMS client or its credentials, is read as a refusal. A create answers the generator's refusal `400` and any
   other failure `502` the same way. A secret identifier or `kid` the tenant already holds answers `409` before the
   binding is proved.
5. **The capabilities list each provider's algorithms.** `IRemoteKeyDescriptorCatalog.Algorithms` lists every row,
   ordered. Each `remoteKeyProviders` entry carries `Algorithms`: the provider's rows
   (`RemoteKeyAlgorithmCapabilityResource { Algorithm, SecretType, KeySizeBits, Curve }`) that
   `ISecretValidator.ValidateRemoteKey` accepts, the check a create and a bind run, so a client offers exactly what a
   create or bind accepts.
6. **The console creates and binds a tenant's remote keys.** On a tenant's Secrets tab, the create-secret form
   offers a custody choice: the server, or each provider that lists an algorithm. A provider's custody offers that
   provider's algorithm rows, each of which sets the key's type, size, curve and algorithm, and calls
   `POST /api/tenants/{tenantId}/secrets/remote`, which reveals nothing. A **Bind KMS key** dialog takes the
   provider, the KMS key identifier, one of the provider's algorithm rows and the expiry, and calls
   `POST /api/tenants/{tenantId}/secrets/remote/import`. Each dialog shows the problem detail of a refusal, such as
   the `403` of a caller without platform authority. A client's and a resource server's Secrets tabs offer neither.

## Options considered

- **A supplied public half proved by a signature.** The operator exports the half from the KMS and pastes it, and
  a mismatched half is found only by the proof, while the KMS serves the half itself.
- **Naming only the algorithm.** `RS256` maps to rows whose key specifications differ, and the provider checks the
  specification against one row, so the server would have to guess the key's size.
- **Reading the public half without the proof.** The read shows that a key of the row's specification exists; the
  signature also shows that the KMS signs with it under the deployment's identity before a token depends on it.
- **Offering every asymmetric type and signature algorithm.** A client learns from a `400` which keys a provider
  holds.

## Consequences

- Binding costs one KMS read and one KMS signature, and the deployment's identity needs the permission to read a
  key's public half, which creating a key already needs.
- An operator binds a key by its identifier and algorithm.
- The offered algorithms follow the registered algorithm packages: a row whose algorithm no package registers is
  not offered.
- An operator creates or binds a tenant's remote key from the console as well as through the API.

## References

- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md):
  the routes this decision amends.
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md):
  the descriptor catalog and `GetPublicKeyAsync`.
- [#687](https://github.com/NCodeGroup/Identity/issues/687),
  [#690](https://github.com/NCodeGroup/Identity/issues/690),
  [#691](https://github.com/NCodeGroup/Identity/issues/691)
