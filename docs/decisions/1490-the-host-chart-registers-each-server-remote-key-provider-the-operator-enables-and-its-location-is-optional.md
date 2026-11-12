# 1490. The host chart registers each server remote key provider the operator enables, and its location is optional

- **Status:** Accepted (amends [ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The server registers a vendor's remote key provider when its `SecretProtection:RemoteKeyProviders:<Vendor>` section
exists, because the section is the operator's consent to reach that service with the deployment's identity
([ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)
rule 5). Its location, `Region`, `VaultUri` or `KeyRing`, is optional: with it the provider creates, lists and sweeps
keys, and without it the provider binds keys a KMS already holds and creates none.

The host chart refuses a release the host would refuse
([ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)) and mounts a
vendor's workload credentials only into pods that use the vendor
([ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md),
[ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)). Every chart value has a default,
so a vendor's values under `secretProtection.remoteKeyProviders` always exist and cannot stand for a section that is
absent. A chart that renders a provider only when its location is set gives a least-privilege identity, one without
`kms:CreateKey` or `kms:ListKeys`, no way to bind keys.

## Decision

**Each vendor under `secretProtection.remoteKeyProviders` has an `enabled` switch that renders its section, and its
location is optional and rendered only when set.**

1. **The switch.** `awsKms.enabled`, `azureKeyVault.enabled` and `googleCloudKms.enabled` are `false` by default. An
   enabled vendor renders `SecretProtection__RemoteKeyProviders__<Vendor>__*`, which registers its provider; a vendor
   that is not enabled renders nothing.
2. **The location is optional.** `region`, `vaultUri` and `keyRing` render as `Region`, `VaultUri` and `KeyRing` only
   when set. `pendingWindowInDays` and `hardwareProtected` render whenever the vendor is enabled, so the section exists
   without a location: `PendingWindowInDays` is the wait of any key the provider destroys, and `HardwareProtected`
   applies only to the keys it creates.
3. **Credentials follow the switch.** A pod carries a vendor's workload credentials when the vendor is the root key
   provider, is listed in `secretProtection.ownerRootKeys.providers` or is enabled under
   `secretProtection.remoteKeyProviders`.
4. **Refusals.** The chart refuses a location on a vendor that is not enabled, because a location registers nothing on
   its own; an enabled vendor with its location and no `secretProtection.remoteKeys.deployment`, because that provider
   creates keys
   ([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md),
   [ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)
   rule 4); and a half-set workload identity of an enabled vendor. The schema refuses an `enabled` that is not a
   boolean, a malformed location and a `pendingWindowInDays` outside 7 to 30. A release whose enabled vendors have no
   location needs no deployment.
5. **The vendors are closed in the chart.** The values hold one block per vendor the host image registers, as the
   console's do
   ([ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md)
   rule 6), so another vendor needs a chart change.

## Options considered

- **The location as the switch.** Rejected: a provider that only binds has no location, so the chart could not register
  it.
- **No switch, as the host has none**
  ([ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)).
  Rejected: the host's switch is whether a section exists, and the chart has no absent section to read. `enabled`
  renders exactly that section and never disagrees with a location, because a location without it is refused.
- **A list of enabled vendors**, as `secretProtection.ownerRootKeys.providers`. Rejected: the host's setting is one
  section per vendor, so a switch in each vendor's block renders exactly that section and keeps every value of a vendor
  in one place. `ownerRootKeys.providers` is a list because `SecretProtection:OwnerRootKeys:Providers` is one.
- **A location that registers its vendor on its own.** Rejected: two ways to register a vendor, and a location left in
  a release's values after its vendor is turned off would register it again.
- **The vendor's workload identity as the switch.** Rejected: one identity serves the root key, the tenants' root keys
  and the tenants' keys, and a pod's ambient identity has no values.
- **`extraEnv`.** Rejected: it bypasses the chart's refusals and its credential mounts.

## Consequences

- A least-privilege identity binds keys through the chart: AWS `kms:GetPublicKey` plus `kms:Sign`, `kms:Decrypt` or
  `kms:DeriveSharedSecret`; Azure `Key Vault Crypto User` on each key; Google `cloudkms.cryptoKeyVersions.viewPublicKey`
  plus `.useToSign` or `.useToDecrypt`.
- A release that creates keys sets `enabled` beside its location.
- A vendor that stays enabled after its location is cleared still reads the keys it created and leaves them unswept
  ([ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)).
- `ci/bind-only-remote-key-providers-values.yaml` and the `Host chart` workflow check a bind-only release and every
  refusal.

## References

- [ADR-1458](1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)
- [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)
- [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)
- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)
- [ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md)
- [ADR-1489](1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issue [#725](https://github.com/NCodeGroup/identity/issues/725)
