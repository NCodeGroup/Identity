# 1476. The management API creates a tenant's remote key and binds one only after proving it

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A tenant's signing and encryption keys can be held in a key management service (KMS)
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)), and every remote
key provider is described by data and reads a key's public half with `IRemoteKeyProvider.GetPublicKeyAsync`
([ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md)). An
operator who manages a tenant through the management API needs more than provisioning gives. The forces:

- **Reading custody.** The secrets of a tenant list a `remote` secret like any other, and nothing in the resource says
  which provider holds its private half.
- **Creating a key on demand.** Provisioning creates keys for the algorithms a tenant's settings use. An operator also
  creates a key in a registered provider for an algorithm and lifetime of the operator's choosing.
- **Bringing a key the KMS already holds.** An organization that keeps its key in a KMS binds it to a tenant instead of
  creating another.
- **Reach of the deployment's identity.** The identity a deployment signs in with reaches every key the KMS lets it
  use, including keys another tenant or the deployment itself owns, and a request that names a key identifier can point
  at any of them.
- **A remote key is bound to its algorithm.** Its key specification and algorithm are rows of the provider's mapping,
  and the stored public half is verified against that row when its tenant loads, so a remote key whose algorithm
  changes cannot be read.
- **The KMS holds the public half.** A bind that takes the public half from the operator makes the operator export it
  from the KMS and paste it.
- **A row selects the key.** A JOSE algorithm such as `RS256` maps to one row per key size, and a provider checks a
  key's specification against the one row it is given.
- **Clients and operators learn what a provider holds.** The key identifier of a KMS key (an ARN, a vault URL, a key
  ring path) names the deployment's infrastructure and stays out of the resource, while the console and other clients
  need each provider's algorithms to offer only what works.
- **Only tenants hold remote keys.** The shared create and import requests also serve clients and resource servers,
  which hold none.

## Decision

**The management API shows which provider holds a remote secret, creates a tenant's key in a registered provider, and
binds a key that a provider already holds only after reading its public half from the KMS, checking it against the
row the operator names and proving that the key signs or decrypts under the deployment's identity.**

1. **`RemoteKeyProvider` names the provider, never the key.** `SecretResource` and `CreatedSecretResource` carry
   `RemoteKeyProvider`, the provider name of a `remote` secret's reference and `null` otherwise. The KMS key
   identifier is never returned, because it names the deployment's infrastructure. `SecretTable` shows a chip with the
   provider name beside a remote secret's type.
2. **Remote keys have tenant-only routes in a handler of their own.** `TenantRemoteSecretApiEndpointHandler` maps
   `POST /api/tenants/{tenantId}/secrets/remote` (`CreateRemoteSecretRequest`) and
   `POST /api/tenants/{tenantId}/secrets/remote/import` (`ImportRemoteSecretRequest`). Each answers `201` with a
   `SecretResource` and declares `400`, `401`, `403`, `404`, `409`, `412`, `415` and `502`. The shared create and
   import contracts carry none of these fields, because clients and resource servers hold no remote keys.
3. **The requests name the key's kind and algorithm, not a use or a public half.** Both requests carry
   `RemoteKeyProvider`, `SecretType`, `KeySizeBits`, `Curve`, `Algorithm` and the lifecycle (`ActivatesWhen`,
   `RetiresWhen`, `ExpiresWhen`); import adds `RemoteKeyId`. Neither names a use, because the row of the provider's
   mapping decides it.
4. **Validation runs before any KMS call.** `ISecretValidator.ValidateCreateRemote` and `ValidateImportRemote` check
   the lifecycle, and both call `ValidateRemoteKey(RemoteKeyAlgorithmSelector)`, which checks a key type a KMS can
   hold, a described provider, an algorithm that resolves the key's size and curve, and a row whose use matches the
   algorithm. Create also needs a provider that creates keys. For import, the key identifier fits
   `SecretMaxLengths.EncodedValue` (8,000) minus the provider name's length and the colon of the reference
   `<provider>:<keyId>`, and holds no control character and no leading or trailing white space.
5. **Create authorizes, then calls the KMS.** The route authorizes `Operations.Create` at the tenant node before it
   reads anything ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)).
   It calls `ISecretGenerator.GenerateRemoteSecretAsync` with no `ManagedBy` and `Origin` `generated`, so removing the
   secret destroys the key in the KMS through `IRemoteSecretDestroyer`. A secret identifier the tenant already holds
   answers `409` before any KMS call. Once the KMS created the key, a record the store does not add or commit hands the
   key to `IRemoteSecretDestroyer` before the failure propagates, so a write that lost a concurrency race answers `412`
   and leaves no key behind.
6. **Bind authorizes at the tenant and the platform.** The import route authorizes `Operations.Create` at the tenant
   node and `Operations.Update` at the platform node, both before any read, because the deployment's identity reaches
   every key the KMS allows and the key identifier is the caller's to name. A tenant administrator alone cannot bind.
7. **The server reads the public half from the KMS.**
   `ISecretGenerator.BindRemoteSecretAsync(BindRemoteSecretRequest)` resolves the provider and the row, reads the
   key's `SubjectPublicKeyInfo` through `IRemoteKeyResolver`, which refuses a key whose usage, specification or
   algorithms do not match the row, and checks the half against the row's type, size and curve, as
   `GenerateRemoteSecretAsync` checks a key it created. It returns a `remote` secret with `Origin` `supplied` and no
   `ManagedBy`, so the server never destroys the key.
8. **The binding is proved before the store adds it.** `IRemoteSecretImporter.VerifyBindingAsync` proves the key
   through `IRemoteKeyProver` by the row's kind: a signature over 32 random bytes that verifies with the half the KMS
   returned, or the decryption of a compact `A256GCM` JWE. A secret identifier or `kid` the tenant already holds
   answers `409` before the proof.
9. **A refusal is a `400`, a failure a `502`.** `RemoteKeyRefusedException`, a provider's `ArgumentException` (a key
   identifier it does not read) and its `CryptographicException` (a key it refuses) answer `400` with the reason. Any
   other failure of the KMS, such as an `InvalidOperationException` from a KMS client or its credentials, is logged
   with the provider and secret identifiers and answers `502`. A missing tenant answers `404`.
10. **An update keeps a remote key's algorithm.** `ISecretValidator.ValidateUpdate` refuses, with `400`, an algorithm
    that differs from the stored algorithm of a `remote` secret.
11. **A remote secret is read without a protector.** `ISecretSerializer.DeserializeRemoteSecret` reads a `remote`
    secret, which holds no master-key material, so no request loads a master key to check it.
12. **The capabilities list each provider's rows.** The `remoteKeyProviders` section carries
    `RemoteKeyProviderCapabilityResource { Name, CreatesKeys, Algorithms }`, whose rows are
    `RemoteKeyAlgorithmCapabilityResource { Algorithm, Use, SecretType, KeySizeBits, Curve }`. They list exactly the
    rows `ValidateRemoteKey` accepts, so a client offers what a create or bind accepts.
13. **The console creates and binds a tenant's remote keys.** On a tenant's Secrets tab, the create-secret form offers
    a custody choice: the server, or each provider that creates keys, with its rows labeled signing or encryption. The
    **Bind KMS key** dialog takes the provider, the key identifier, one of the provider's rows and the expiry. Each
    dialog shows the problem detail of a refusal, such as the `403` of a caller without platform authority. The
    Secrets tabs of clients and resource servers offer neither.

## Options considered

- **Fields on the shared create and import requests.** The client and resource-server routes would accept fields they
  must refuse, and every handler would validate them. Routes of their own for the tenant keep the shared contracts
  free of them.
- **A supplied public half, proved or not.** The operator exports the half from the KMS and pastes it, a mismatched
  half is found only by the proof, and without the proof it would publish in the tenant's JWKS a key that does not
  verify what the KMS signs. The KMS serves the half itself.
- **Naming only the algorithm.** `RS256` maps to rows whose key specifications differ, and the provider checks the
  specification against one row, so the server would guess the key's size.
- **Tenant authority alone for binding.** A tenant administrator could name any key the deployment's identity can
  use, including another tenant's, and have the server sign or decrypt for it.
- **Returning the key identifier.** It names the deployment's infrastructure, and the provider name is enough to tell
  custody apart.
- **Destroying a bound key with its record.** The server did not create it, and an organization that binds a key
  keeps the right to use it elsewhere.
- **Offering every type and algorithm.** A client would learn from a `400` which keys a provider holds.
- **Chosen:** tenant-only routes that name the key and its row, read the public half from the KMS, prove the key and
  require platform authority to bind, with capabilities that list each provider's rows.

## Consequences

- A bind costs one KMS read and one proof, and a KMS that is down or refuses the deployment's identity refuses the
  binding with `502`. The deployment's identity needs permission to read a key's public half, which creating a key
  already needs.
- A platform administrator can bind any key the deployment's identity can reach to any tenant.
- A key created in a provider is destroyed with its record, and a bound key is never destroyed by the server.
- A client of the API learns the provider of a remote key and never its key identifier.
- A tenant's remote key cannot change its algorithm; another algorithm needs another key.
- The offered rows follow the registered algorithm packages: a row whose algorithm no package registers is not
  offered.
- An operator creates or binds a tenant's remote key from the console as well as through the API.

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md): the remote keys
  this decision manages.
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md): the
  descriptor catalog and `GetPublicKeyAsync`.
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md):
  `IRemoteKeyResolver`, `IRemoteKeyProver` and `RemoteKeyRefusedException`.
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md):
  authorization before any read.
- [#648](https://github.com/NCodeGroup/identity/issues/648),
  [#687](https://github.com/NCodeGroup/identity/issues/687),
  [#690](https://github.com/NCodeGroup/identity/issues/690),
  [#691](https://github.com/NCodeGroup/identity/issues/691)
