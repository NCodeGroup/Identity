# 1475. The management API creates a tenant's key in a key management service and binds one that proves it signs

- **Status:** Accepted (amends [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md); amended by [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md): the secret validator and the capabilities read `IRemoteKeyDescriptorCatalog`; amended by [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md): the bind reads the key's public half from the KMS, the capabilities list each provider's algorithms, and the console creates and binds remote keys; amended by [ADR-1477](1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md); amended by
  [ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md):
  the management API creates a key only in a provider that creates keys, and the capabilities say which do)
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A tenant's signing keys can be held in a key management service (KMS), and the host provisions them there through the
tenant setting `tenant_key_provider`
([ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)). An
operator who manages a tenant through the management API needs more than that setting gives:

- **Reading custody.** The secrets of a tenant list a `remote` secret like any other, and nothing in the resource says
  which provider holds its private half.
- **Creating a key on demand.** Provisioning creates keys for the algorithms a tenant's settings use. An operator also
  creates a key in a registered provider for an algorithm and lifetime of the operator's choosing.
- **Bringing a key the KMS already holds.** An organization that keeps its signing key in a KMS binds it to a tenant
  instead of creating another.
- **Reach of the deployment's identity.** The identity a deployment signs in with reaches every key the KMS lets it
  use, including keys another tenant or the deployment itself owns. A request that names a key identifier can point at
  any of them.
- **A remote key is bound to its algorithm.** Its key spec and signing algorithm are rows of the provider's mapping,
  and the stored public half is verified against that row when its tenant loads, so a remote key whose algorithm
  changes cannot be read.

The key identifier of a KMS key (an ARN, a vault URL, a key ring path) names the deployment's infrastructure. The
shared create and import requests serve tenants, clients and resource servers, and only a tenant's signing keys are
held in a KMS.

## Decision

**The management API shows which provider holds a remote secret, creates a tenant's signing key in a registered
provider, and binds a key that a provider already holds only after the key signs a challenge that verifies with the
public half the operator supplied.**

1. **`SecretResource.RemoteKeyProvider` names the provider.** `SecretResource` and `CreatedSecretResource` carry
   `RemoteKeyProvider`, the provider name of a `remote` secret's reference and `null` for a key the server holds. The
   KMS key identifier is never returned: a tenant administrator who reads secrets does not need the name of the
   deployment's infrastructure.
2. **Remote keys have tenant-only routes in a handler of their own.** `TenantRemoteSecretApiEndpointHandler` maps
   `POST /api/tenants/{tenantId}/secrets/remote` (`CreateRemoteSecretRequest`) and
   `POST /api/tenants/{tenantId}/secrets/remote/import` (`ImportRemoteSecretRequest`). Both answer `201` with a
   `SecretResource`, and declare their problem answers (`400`, `401`, `403`, `404`, `409`, `412`, `502`). `CreateRemoteSecretRequest` carries `RemoteKeyProvider`, `SecretType`, `KeySizeBits`, `Curve`,
   `Algorithm` and the lifecycle (`ActivatesWhen`, `RetiresWhen`, `ExpiresWhen`). `ImportRemoteSecretRequest` carries
   `RemoteKeyProvider`, `RemoteKeyId`, the public-import `SecretType`, `Format` and `KeyMaterial`, `Algorithm` and the
   lifecycle. The shared `CreateSecretRequest` and `ImportSecretRequest` stay as they are, because their routes also
   serve clients and resource servers, which hold no remote keys.
3. **Create authorizes, validates, then calls the KMS.** The route authorizes `Operations.Create` at the tenant node
   before it reads anything ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)).
   It validates everything before the KMS is called: the provider is registered, the key type can be held in a KMS, the
   algorithm resolves to a key size and curve, and the provider's mapping has a row for the provider, algorithm, key
   type, size and curve. It then calls `ISecretGenerator.GenerateRemoteSecretAsync` with `Use` `sig` and no
   `ManagedBy`, so the key keeps the lifecycle the request gives it with `Origin` `generated`, and removing the secret
   destroys the key in the KMS through `IRemoteSecretDestroyer`. A request that fails validation answers `400` without
   a KMS call, a missing tenant `404`, a generator that refuses its parameters `400`, a KMS failure or refusal `502`
   (logged with the provider and secret identifiers), and a secret identifier the tenant already holds `409` before the KMS is
   called. Once the KMS created the key, a record the store does not add or commit hands the key to
   `IRemoteSecretDestroyer` before the failure propagates, so a write that lost a concurrency race answers `412` and
   leaves no key behind.
4. **Import binds a key the KMS holds and proves the binding.** The server imports the supplied public half under the
   client-key policy (`IClientKeyImporter.TryImportKey`, `Use` `sig`, [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md),
   [ADR-1104](1104-import-public-asymmetric-secret-material.md)) and stores it as a `remote` secret with `Origin`
   `supplied` and no `ManagedBy`, so the server never destroys it. The `RemoteKeyId` is stored in the reference
   `<provider>:<keyId>`, which holds at most 8,000 characters (`SecretMaxLengths.EncodedValue`), so the identifier
   holds at most 8,000 less the provider name's length and the colon (7,992 for `aws-kms`); it holds no control
   character and no leading or trailing white space. The algorithm is a registered signature algorithm for the key's
   type. It reads the stored secret through the tenant load
   path (`ISecretSerializer.DeserializeRemoteSecret`), which refuses an unregistered provider, a missing mapping row or
   a size or curve that differs from the row, and signs 32 random bytes through the referenced key with
   `SignatureAlgorithm.SignAsync`, which verifies the KMS signature with the stored public half. A secret that does not
   load, a signature that does not verify, an unregistered provider or a missing row answers `400`; a KMS failure or
   refusal is logged and answers `502`; a secret identifier or `kid` the tenant already holds answers `409` before the binding is proved; a missing
   tenant answers `404`.
5. **Binding needs platform authority.** The import route authorizes `Operations.Create` at the tenant node, then
   `Operations.Update` at the platform node, both before any read. A tenant administrator alone cannot bind, because
   the deployment's identity reaches every key the KMS lets it use and the key identifier is the caller's to name.
6. **An update keeps a remote key's algorithm.** `ISecretValidator.ValidateUpdate` refuses, with `400`, an algorithm
   that differs from the stored algorithm of a `remote` secret.
7. **The capabilities list the registered providers.** `GET` capabilities carry the `remoteKeyProviders` section: the
   registered provider names, sorted ordinally (`RemoteKeyProviderCapabilityResource { Name }`), read from
   `IRemoteKeyProviderCatalog.Providers`.
8. **A remote secret is read without a protector.** `ISecretSerializer.DeserializeRemoteSecret(PersistedSecret)`
   reads a `remote` secret, which holds no master-key material (ADR-1471 rule 3), so a management request never loads
   a master key to check a binding.
9. **The console shows the provider.** `SecretTable` shows a chip with the provider name beside a remote secret's
   type. [ADR-1476](1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md)
   decides the dialogs that create and bind a remote key.

## Options considered

- **Fields on the shared create and import requests.** The client and resource-server routes would accept fields they
  must refuse, and every handler would validate them. Routes of their own for the tenant, the only owner that holds
  remote keys, keep the shared contracts as they are.
- **Reading the public half from the KMS.** It needs a member on every provider that returns a key's public half. The
  signing proof binds a supplied public half to the key as strongly, with the members the providers already have.
- **Trusting a supplied public half without a proof.** A mismatched public half would publish in the tenant's JWKS a
  key that does not verify the tokens the KMS signs, and a key identifier that names another owner's key would bind
  without the KMS ever being asked to sign with it.
- **Tenant authority alone for binding.** A tenant administrator could name any key the deployment's identity can
  sign with, including another tenant's, and have the server sign for it. Binding takes platform `Update` authority.
- **Returning the KMS key identifier.** It names the deployment's infrastructure, and the provider name is enough to
  tell custody apart.
- **Destroying a bound key with its record.** The server did not create it, and an organization that binds a key
  keeps the right to use it elsewhere, so a bound key has `Origin` `supplied`.

## Consequences

- Binding a key costs one KMS signature, and a KMS that is down or refuses the deployment's identity refuses the
  binding with `502`.
- A key the server created in a provider is destroyed with its record. A bound key is never destroyed by the server.
- A platform administrator can bind any key the deployment's identity can sign with to any tenant.
- A client of the API that reads `SecretResource` learns the provider of a remote key and never its key identifier.
- A tenant's remote key cannot change its algorithm. Another algorithm needs another key.
- The capabilities tell a client which providers a deployment registers, so a client offers only those.

## References

- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md): the remote
  keys this decision manages.
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md): authorization
  before any read.
- [ADR-1161](1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md),
  [ADR-1104](1104-import-public-asymmetric-secret-material.md): the import policy of a supplied public half.
- [#648](https://github.com/NCodeGroup/Identity/issues/648)
