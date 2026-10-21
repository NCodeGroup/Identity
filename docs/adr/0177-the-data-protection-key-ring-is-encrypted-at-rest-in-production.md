# 177. The Data Protection key ring is encrypted at rest in Production

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

ASP.NET Core Data Protection keeps its key ring in an `IXmlRepository`. The keys in that ring unprotect the server's
reversible secrets: persisted signing and encryption key material, sessions, and anti-forgery tokens. The reference
host persists the ring to the same Postgres database as those secrets (`PersistKeysToDbContext`), so every replica
shares it. Nothing wrapped the ring, so a database backup or dump held both the protected secret material and the keys
that unprotect it. Encrypting the secrets protected nothing against anyone holding the dump.

Data Protection protects a ring through an `IXmlEncryptor` on `KeyManagementOptions`. `ProtectKeysWithCertificate`,
`ProtectKeysWithAzureKeyVault`, and similar calls each set one. When no repository is configured, the key manager
falls back to a local directory or to memory; on Linux that fallback is unencrypted. Either way it is lost when the
replica is replaced. The fallback happens inside the key manager, so `KeyManagementOptions.XmlRepository` and
`XmlEncryptor` stay `null` for an unconfigured ring.

## Decision

1. **A Production host refuses to start with an unprotected or unpersisted key ring.** In Production,
   `AddProductionSafety` validates `KeyManagementOptions` on start. A `null` `XmlRepository` fails with a message
   asking for shared storage. A `null` `XmlEncryptor` fails with a message naming the `DataProtection:KeyProtection`
   section and the key management service alternative. The check runs as the host starts, before the server binds a
   port, because Data Protection is composed after the gate is registered.
2. **The check is provider-agnostic.** Any `IXmlEncryptor` satisfies it, whether a certificate, a cloud key management
   service or HSM, or a custom encryptor registered on `IIdentityServerBuilder.DataProtectionBuilder`. No list of
   approved providers exists to extend.
3. **A certificate is the configuration-driven default.** `AddConfiguration` binds `DataProtection:KeyProtection`
   (`KeyRingProtectionOptions`) strictly. When `Certificate` is set, it calls `ProtectKeysWithCertificate`. Each entry
   in `PreviousCertificates` goes to `UnprotectKeysWithAnyCertificate`, so the ring stays readable while a certificate
   is rotated. A certificate is either a PEM certificate with a separate PEM private key, which is how a Kubernetes TLS
   secret mounts it (the key may be encrypted, with `Password`), or a PKCS#12 bundle. It must carry an RSA private key,
   because Data Protection's certificate encryptor uses RSA.
4. **Certificates load during composition.** Data Protection takes certificate instances rather than a deferred
   factory. The private decryption options are internal, so the certificates cannot be supplied later. A missing file,
   a wrong password, a certificate without an RSA key, or previous certificates without a current one each stop the
   host while it is composed. The error names the configuration path.

5. **Readiness fails while the replica cannot unwrap a live key it found in the ring.** `AddIdentityServer` registers
   a readiness check (`HealthCheckTags.Readiness`) that asks the `IKeyManager` for every key and creates each live key's
   encryptor, which unwraps it. A key is live until it expires or is revoked. The check is provider-agnostic like
   decision 2. It considers only the keys that existed when the replica first checked its readiness: a replica started
   with the wrong certificate cannot read the existing keys, and Data Protection quietly creates a new default key it
   can read. That replica stays out of service, which stops a rolling update before it replaces the healthy replicas.
   The replicas that were already serving do not check the new key, so the misconfigured one cannot take them out of
   service too.

## Consequences

- A Production deployment must supply a protection key held outside the database. That can be a certificate from the
  secret store or a key management service. Otherwise it does not start. **Breaking** for Production deployments that
  relied on an unprotected ring. Existing unencrypted keys stay readable. Only new keys are encrypted, so rotation past
  the old keys' expiry completes the migration.
- Development, Conformance, and test hosts are unaffected. The check runs only in Production.
- Rotating the certificate means: deploy the new certificate as `Certificate` and list the old one in
  `PreviousCertificates`. A key stays in the ring after it expires and still unprotects the data it protected, and
  persisted secrets are long-lived. Keep the old certificate until no stored data depends on a key it wrapped, which
  means until those secrets are re-protected or retired, not merely until the keys expire.
- The certificate is therefore long-lived and rotated by hand. A key is unwrapped only by the certificate that wrapped
  it, matched by thumbprint, so a renewal written in place over the current certificate, which is cert-manager's
  default, leaves the ring unreadable after the next restart. The host chart's README describes the rotation steps and
  warns against in-place renewal. The readiness check of decision 5 catches such a renewal, and a previous certificate
  removed while a key it wrapped is still live, when a replica starts.
- Each readiness probe reads the ring and unwraps every live key, which is one repository read and one RSA decryption
  per key. A ring holds a handful of live keys, so the cost is small beside the database check.
- Envelope encryption of individual stored secrets (`S9`) and external keystores (`S7`) remain separate, complementary
  work. This decision protects only the ring itself.
