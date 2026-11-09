# 1485. The host chart holds the console's key rings in a key management service under the console's own identity

- **Status:** Accepted (amends [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The management console signs client assertions with the key ring `mgmt-client` and DPoP proofs with the key ring
`mgmt-dpop`
([ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)). A key ring holds
PEM files, keys that a key management service (KMS) holds, or both, and the console reads and proves every key when it
starts
([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)).

The host chart deploys the console
([ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)) and
refuses a release the host would refuse
([ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)). A console
whose keys exist only as PEM files keeps its private keys in the memory of every replica.

A remote key provider registers when its `SecretProtection:RemoteKeyProviders:<Vendor>` section exists, and its options
validator requires `Region`, `VaultUri` or `KeyRing`. The provider uses that value only to create and list keys. It
reads every key where the key's own identifier names it, so a console that only reads keys needs a value that
satisfies the validator and agrees with its keys.

The server's workload identity under `secretProtection` creates, uses and destroys tenants' keys and wraps the root
key. The console has its own ServiceAccount.

## Decision

**The chart names each console key ring's PEM Secret and remote keys, registers each vendor a remote key names with a
location read from that key, and gives the console a workload identity of its own in the server's shapes.**

1. **Key ring values.** `managementConsole.keyRings.client` (`Bff:KeyRings:mgmt-client`) and
   `managementConsole.keyRings.dpop` (`Bff:KeyRings:mgmt-dpop`) each have `pemSecret`, a Secret that holds
   `mgmt-client.pem` or `mgmt-dpop.pem`, and `remoteKeys`, a map from key id (`kid`) to `provider`, `keyId`,
   `algorithm`, `secretType`, `keySizeBits` and an optional `curve`. The map mirrors
   `Bff:KeyRings:{name}:RemoteKeys:{kid}` and cannot name one id twice.
2. **A vendor is registered from the keys that name it.** The chart renders a vendor's
   `SecretProtection__RemoteKeyProviders__<Vendor>__<location>` exactly when a console remote key names that vendor.
   The location is read from the first such key, the client ring before the DPoP ring and key ids in sorted order: the
   region of an AWS ARN, `https://<host>` of a Key Vault key identifier, or the key ring of a Cloud KMS crypto key
   version name. `PendingWindowInDays` and `HardwareProtected` are not rendered.
3. **The console has its own workload identity.** `managementConsole.secretProtection.awsKms.webIdentity`,
   `.azureKeyVault.workloadIdentity` and `.googleCloudKms.workloadIdentityFederation` mirror the `secretProtection.*`
   values of the same names. A console pod carries a vendor's credentials only when a console remote key names that
   vendor, and the server's pods never carry the console's. The Google credential configuration is the ConfigMap
   `<console full name>-google-cloud`. An empty identity leaves the vendor's ambient identity in force.
4. **One rendering for both components.** The `identity-host.kmsCredentials.*` helpers render credential env, mounts,
   volumes, the Google credential configuration and the half-set checks from one dict: the full name, the vendors in
   use and the three identity values. `identity-host.kmsCredentials.server` and
   `identity-host.kmsCredentials.managementConsole` build that dict. The values schema defines the three identity shapes
   once, in shared `definitions`.
5. **Refusals.** The chart refuses a key ring with neither `pemSecret` nor a remote key, and two keys of a ring whose ids
   are equal when compared without case (a PEM key's id is `mgmt-client` or `mgmt-dpop`, and .NET configuration keys
   are case-insensitive). It refuses a `keyId` that is not in its provider's read form: an AWS key or alias ARN, a Key
   Vault key *version* identifier, or a Cloud KMS crypto key *version* name, as `AwsKmsKeyArn.IsValid`,
   `AzureKeyVaultKeyId.IsKeyVersionId` and `GoogleCloudKmsKeyName.IsCryptoKeyVersionName` accept. It refuses a
   half-set console identity. The schema refuses an unknown provider, a missing field, `keySizeBits` below 1, and a key
   id that is not letters and digits separated by single `.`, `_` or `-` and at most 64 characters, so a `kid` never
   contains the `__` configuration separator and is a valid environment variable name.
6. **The provider list is closed in the chart.** `aws-kms`, `azure-key-vault` and `google-cloud-kms` are the vendors
   whose sections and credentials the chart renders and the console image registers, so another vendor needs a chart
   change
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## Options considered

- **The server's workload identity.** Rejected: it creates and destroys tenants' keys and wraps the root key, its
  federated credentials trust the server's ServiceAccount, and the console has its own ServiceAccount, so least
  privilege gives the console an identity of its own.
- **Region, vault and key ring values for the console.** Rejected: a value that only satisfies a validator, and that
  can disagree with the keys, is worse than one the chart reads from the keys.
- **The server's `secretProtection.remoteKeyProviders` values.** Rejected: tenants could then hold keys there, and
  `remoteKeys.deployment` is required.
- **A list of remote keys with a `kid` field.** Rejected: a map cannot name one id twice, and the list can.
- **Copies of the server's credential helpers.** Rejected: two renderings of one shape drift.

## Consequences

- A deployment keeps the console's private keys out of its pods, and each console start reads and proves every remote
  key.
- The operator creates the keys, the console's identity and its grants: AWS `kms:GetPublicKey` plus `kms:Sign` or
  `kms:Decrypt`; Azure `Key Vault Crypto User` on each key; Google `cloudkms.cryptoKeyVersions.viewPublicKey` plus
  `.useToSign` or `.useToDecrypt`. The operator keeps the keys out of the `ncid-` groups and the deployment's tag.
- A remote DPoP key costs a KMS signature per proof, so the DPoP ring usually keeps its PEM Secret.

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)
- [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
- [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issue [#712](https://github.com/NCodeGroup/Identity/issues/712)
