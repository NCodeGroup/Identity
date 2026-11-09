# 1489. A remote key provider registers without a key-creation location, and only one that has one creates and sweeps keys

- **Status:** Accepted (amends [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md), [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md), [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md), [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md), [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md), [ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

Each vendor has a key-creation location: `Region` for AWS KMS, `VaultUri` for Azure Key Vault and `KeyRing` for Google
Cloud KMS. A provider uses it only to create keys and to list them
([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)).
It reads, signs with, decrypts with and destroys a key where the key's own identifier names it.

A BFF host only reads the keys its key rings name and proves them when it starts
([ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)).
It has no location to name, and a host chart that had to supply one would derive it from the console's keys
([ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md)).
A server whose identity lacks `kms:CreateKey` and `ListKeys` has the same need: it binds keys an operator created and
never creates one.

Every consumer that creates, lists or sweeps a provider's keys assumes the provider can: the `tenant_key_provider`
domain, the tenant key policy's start check, the secret generator, the sweep service and job, and the management API's
create.

## Decision

**A provider's location is optional, its descriptor says whether it creates keys, and the OP creates, lists and sweeps
only in a provider that does.**

1. **The location is optional.** Each vendor's options validator checks `Region`, `VaultUri` or `KeyRing` only when it
   is set, and null or empty means unset. Every other option keeps its check.
2. **The descriptor says whether the provider creates keys.** `RemoteKeyProviderDescriptor.CreatesKeys` is `true` by
   default, because a provider that implements creation creates keys unless its source says otherwise. Each vendor
   source takes the vendor's options and sets it to whether the location is set. The provider's constructor still reads
   no option
   ([ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
   rule 1).
3. **A provider without a location works by identifier.** It reads, signs with, decrypts with, agrees on a secret with
   and destroys a key by the key's identifier, and its `CreateKeyAsync` and `ListKeysAsync` throw
   `InvalidOperationException`.
4. **The server creates, lists and sweeps only in a provider that creates keys.** The `tenant_key_provider` domain
   lists only those providers, and the tenant key policy's start check covers only them. The secret generator refuses
   any other with `RemoteKeyRefusedException` before it calls the provider. The remote key options validator requires
   `Deployment` only when such a provider is described. The sweep service dispatches, and the sweep job sweeps, only
   them. The management API's create refuses any other with `400`, and its capabilities carry `createsKeys` for each
   provider, so the console offers creation only in those. One refusal text serves every consumer: `The remote key
   provider '{name}' creates no keys: it has no key-creation location.`
5. **Binding works in any registered provider.** A least-privilege identity binds and signs with keys an operator
   created. The OP host keeps `Add<Vendor>RemoteKeyProviderIfConfigured`, because a vendor's section is the operator's
   consent to reach that service with the deployment's identity.
6. **The console host registers every vendor unconditionally.** `Add<Vendor>RemoteKeyProvider` constructs a provider
   that does no I/O and creates no client, and the provider reads each key where the key's identifier names. The chart
   renders no `SecretProtection__RemoteKeyProviders__*` for the console.

## Options considered

- **A required location.** Rejected: a host that only reads keys has no location to give, and a value that only
  satisfies a validator can disagree with the keys.
- **An `Enabled` option.** Rejected: a second switch beside the location that can disagree with it, and a provider with
  a location and `Enabled` off is a state no consumer needs.
- **A location required for every OP provider.** Rejected: a least-privilege identity that binds keys and never creates
  one could not register its provider.
- **The provider reading the location.** Rejected: a provider that reads an option in its constructor breaks
  [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
  rule 1, so its source reads the option and the descriptor carries the result.

## Consequences

- A least-privilege identity binds a key a KMS already holds without `kms:CreateKey`.
- A provider that loses its location leaves the keys it created unswept.
- A `tenant_key_provider` that names a provider without a location is refused as an unregistered one.
- A bind-only server provider through the host chart needs a chart change: the chart registers a server provider only
  when its location is set.
- A BFF host registers the providers without a section.

## References

- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md)
- [ADR-1475](1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md)
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)
- [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)
- [ADR-1485](1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md)
- `NCode.Identity.Secrets.AwsKms`, `NCode.Identity.Secrets.AzureKeyVault`, `NCode.Identity.Secrets.GoogleCloudKms`
- Issue [#721](https://github.com/NCodeGroup/Identity/issues/721)
