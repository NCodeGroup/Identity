# 1473. A key management service's adapter package is named for its service

- **Status:** Accepted (amends [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

One package per key management service holds both of the service's roles: its key wrapper and its remote key provider
([ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) rule 17).
[ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md) rule 3 names an
adapter `<contract area>.<vendor>` with the area `Secrets.KeyWrapping`, and that area names one of the two roles. A
package that also holds remote signing keys is named for half of what it does.

## Decision

**A key management service's adapter package is named for the secrets family and the service:
`NCode.Identity.Secrets.<Vendor>`.**

The area in ADR-1279's rule is the secrets family, so the packages (project, assembly, root namespace and test
project) are `NCode.Identity.Secrets.AwsKms`, `NCode.Identity.Secrets.AzureKeyVault` and
`NCode.Identity.Secrets.GoogleCloudKms`. A role is a namespace inside the family (`KeyWrapping`, `RemoteKeys`), not a
package segment. A package that adds another service names itself the same way.

## Options considered

- **One package per role** (`Secrets.KeyWrapping.<Vendor>` and `Secrets.RemoteKeys.<Vendor>`) splits a service's client,
  credentials and options across two packages that configure the same service.
- **`Secrets.Kms.<Vendor>`** adds a segment that no other adapter uses.

## Consequences

- The packages register both roles on the `SecretPersistenceLibrary` builder.
- A host references one package per service, whichever roles it uses.
- A new role for a service is a new namespace in its package, not a new package.

## References

- [ADR-1279](1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md)
- [ADR-1471](1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md)
- [ADR-1260](1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md)
- Issue [#652](https://github.com/NCodeGroup/Identity/issues/652)
