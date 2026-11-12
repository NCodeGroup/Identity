# 1485. The host chart enables server KMS vendors and holds console key rings under the console's identity

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The server registers a vendor's remote key provider when its `SecretProtection:RemoteKeyProviders:<Vendor>` section
exists, because the section is the operator's consent to reach that service with the deployment's identity. The
location, `Region`, `VaultUri` or `KeyRing`, is optional: with it the provider creates, lists and sweeps keys, and
without it the provider binds keys a key management service (KMS) already holds and creates none
([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)).

The host chart refuses a release the host would refuse
([ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md))
and mounts a vendor's workload credentials only into pods that use the vendor
([ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md),
[ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)). Every chart value has a default,
so a vendor's values under `secretProtection.remoteKeyProviders` always exist and cannot stand for a section that is
absent. A chart that renders a provider only when its location is set gives a least-privilege identity, one without
`kms:CreateKey` or `kms:ListKeys`, no way to bind keys.

The management console signs client assertions with the key ring `mgmt-client` and DPoP proofs with the key ring
`mgmt-dpop`
([ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)). A key ring holds
PEM files, keys that a KMS holds, or both, and the console reads and proves every key when it starts
([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)).
A console whose keys exist only as PEM files keeps its private keys in the memory of every replica. The console host
registers every vendor without a location and reads each key where the key's own identifier names it, so the console
needs no provider section.

The server's workload identity under `secretProtection` creates, uses and destroys tenants' keys and wraps the root
key. The console has its own ServiceAccount.

## Decision

**Each vendor under `secretProtection.remoteKeyProviders` has an `enabled` switch that renders its section and an
optional location; the console's key rings name their PEM Secret and remote keys, and the console has a workload
identity of its own in the server's shapes.**

1. **The switch.** `awsKms.enabled`, `azureKeyVault.enabled` and `googleCloudKms.enabled` under
   `secretProtection.remoteKeyProviders` are `false` by default. An enabled vendor renders
   `SecretProtection__RemoteKeyProviders__<Vendor>__*`, which registers its provider; a vendor that is not enabled
   renders nothing.
2. **The location is optional.** `region`, `vaultUri` and `keyRing` render as `Region`, `VaultUri` and `KeyRing` only
   when set. `awsKms.pendingWindowInDays` and the `hardwareProtected` of `azureKeyVault` and `googleCloudKms` render
   whenever the vendor is enabled, so the section exists without a location. A vendor without a location binds
   operator-created keys and creates, lists and sweeps none.
3. **Server credentials follow the switch.** A server pod carries a vendor's workload credentials when the vendor is
   the root key provider, is listed in `secretProtection.ownerRootKeys.providers` or is enabled under
   `secretProtection.remoteKeyProviders` (`identity-host.usesKeyManagementService`).
4. **Server refusals.** The chart refuses a location on a vendor that is not enabled, because a location registers
   nothing on its own; an enabled vendor with a location and no `secretProtection.remoteKeys.deployment`, because that
   provider creates keys
   ([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)); and a
   half-set workload identity. The schema refuses an `enabled` that is not a boolean, a malformed location and a
   `pendingWindowInDays` outside 7 to 30. A release whose enabled vendors have no location needs no deployment.
5. **Console key rings.** `managementConsole.keyRings.client` (`Bff:KeyRings:mgmt-client`, PEM file `mgmt-client.pem`)
   and `managementConsole.keyRings.dpop` (`Bff:KeyRings:mgmt-dpop`, PEM file `mgmt-dpop.pem`) each have `pemSecret`, a
   Secret that holds the PEM file, and `remoteKeys`, a map from key id (`kid`) to `provider`, `keyId`, `algorithm`,
   `secretType`, `keySizeBits` and an optional `curve`. A ring may have either or both. The chart renders each remote
   key as `Bff__KeyRings__mgmt-<ring>__RemoteKeys__<kid>__*`. A map cannot name one id twice.
6. **No provider section for the console.** The chart renders no `SecretProtection__RemoteKeyProviders__*` for the
   console: the console host registers every vendor without a location and reads each key where its identifier names
   it.
7. **The console has its own workload identity.** `managementConsole.secretProtection.awsKms.webIdentity`,
   `.azureKeyVault.workloadIdentity` and `.googleCloudKms.workloadIdentityFederation` mirror the `secretProtection.*`
   values of the same names. A console pod carries a vendor's credentials only when a console remote key names that
   vendor, and server pods never carry the console's. An empty identity leaves the vendor's ambient identity in force.
   The Google credential configuration is the ConfigMap `<console full name>-google-cloud`.
8. **One rendering for both components.** The `identity-host.kmsCredentials.*` helpers render credential env, mounts,
   volumes, the Google credential configuration and the half-set checks from one dict: the full name, the vendors in
   use and the three identity values. `identity-host.kmsCredentials.server` and
   `identity-host.kmsCredentials.managementConsole` build that dict. The values schema defines the three identity
   shapes once, in shared `definitions`.
9. **Console refusals.** The chart refuses a ring with neither `pemSecret` nor a remote key, and two ids in a ring that
   are equal without case (a PEM key's id is `mgmt-client` or `mgmt-dpop`, and .NET configuration keys are
   case-insensitive). It refuses a `keyId` that is not in its provider's read form: an AWS key or alias ARN, a Key
   Vault key *version* identifier, or a Cloud KMS crypto key *version* name, as `AwsKmsKeyArn.IsValid`,
   `AzureKeyVaultKeyId.IsKeyVersionId` and `GoogleCloudKmsKeyName.IsCryptoKeyVersionName` accept. It refuses a half-set
   console identity. The schema refuses an unknown provider, a missing field, `keySizeBits` below 1, and a `kid` that is
   not letters and digits separated by single `.`, `_` or `-` and at most 64 characters, so a `kid` never contains the
   `__` configuration separator and is a valid environment variable name.
10. **The vendors are closed in the chart.** `aws-kms`, `azure-key-vault` and `google-cloud-kms` are the vendors whose
    sections and credentials the chart renders and the images register, so another vendor needs a chart change
    ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## Options considered

- **The location as the switch.** Rejected: a provider that only binds has no location, so the chart could not register
  it.
- **A location that registers its vendor on its own, or the vendor's workload identity as the switch.** Rejected: a
  location left in a release's values after its vendor is turned off would register it again, and one identity serves
  the root key, the tenants' root keys and the tenants' keys, while a pod's ambient identity has no values.
- **A list of enabled vendors**, as `secretProtection.ownerRootKeys.providers`. Rejected: the host's setting is one
  section per vendor, so a switch in each vendor's block renders exactly that section and keeps every value of a vendor
  in one place.
- **`extraEnv`.** Rejected: it bypasses the chart's refusals and its credential mounts.
- **The server's workload identity for the console.** Rejected: it creates and destroys tenants' keys and wraps the
  root key, its federated credentials trust the server's ServiceAccount, and the console has its own ServiceAccount, so
  least privilege gives the console an identity of its own.
- **`secretProtection.remoteKeyProviders` for the console.** Rejected: tenants could then hold keys there, and
  `remoteKeys.deployment` is required.
- **A list of remote keys with a `kid` field.** Rejected: a map cannot name one id twice, and the list can.
- **Copies of the server's credential helpers.** Rejected: two renderings of one shape drift.
- **Chosen:** an `enabled` switch per server vendor, a key ring map with a PEM Secret and remote keys for the console,
  and a console identity rendered by the shared helpers.

## Consequences

- A least-privilege server identity binds keys through the chart, and a release that creates keys sets `enabled` beside
  its location.
- A vendor that stays enabled after its location is cleared still reads the keys it created and leaves them unswept.
- A deployment keeps the console's private keys out of its pods, and each console start reads and proves every remote
  key.
- The operator creates the keys, the identities and the grants: AWS `kms:GetPublicKey` plus `kms:Sign`, `kms:Decrypt`
  or `kms:DeriveSharedSecret`; Azure `Key Vault Crypto User` on each key; Google
  `cloudkms.cryptoKeyVersions.viewPublicKey` plus `.useToSign` or `.useToDecrypt`.
- A remote DPoP key costs a KMS signature per proof, so the DPoP ring usually keeps its PEM Secret.
- `ci/bind-only-remote-key-providers-values.yaml` and the `Host chart` workflow check a bind-only release and every
  refusal.

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)
- [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)
- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)
- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issues [#712](https://github.com/NCodeGroup/identity/issues/712) and
  [#725](https://github.com/NCodeGroup/identity/issues/725)
