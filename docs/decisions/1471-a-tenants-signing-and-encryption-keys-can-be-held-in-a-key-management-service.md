# 1471. A tenant's signing and encryption keys can be held in a key management service

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A tenant's private keys sign its tokens and decrypt the JWEs sent to it. A replica that loads a private key into memory
can leak it, and every replica that serves the tenant holds it. A key management service (KMS) keeps the private key
inside the service, so no replica holds one, and the same holds for encryption keys, which are as sensitive as signing
keys.

- **The public half is enough locally.** Algorithm checks, JWK export and verification need only the public key, so a
  stored key can carry its SPKI and leave the private half in the service.
- **The services differ.** AWS KMS signs, decrypts RSA-OAEP and derives ECDH shared secrets; Azure Key Vault signs and
  unwraps RSA-OAEP content keys; Google Cloud KMS signs and decrypts RSA-OAEP and agrees no keys. Each offers its own
  algorithm and key-size rows.
- **Custody is one choice for every key.** A split by use adds vocabulary for a choice no threat model needs.
- **A remote call has no place in synchronous code.** Signing and content-key unwrapping reach the service through the
  asynchronous seam of [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md).
- **A stored secret must not be trusted blindly.** A reference to a remote key is data a tenant's document carries, so
  the signature, the decryption and the load all verify what the service returns.

## Decision

**A tenant's signing and encryption keys can stay in a KMS: the service signs every token and unwraps every content key,
and one tenant setting chooses the custody of every provisioned key.**

1. **Remote key types.** `RemoteRsaSecretKey : RsaSecretKey, IRemoteSecretKey` and
   `RemoteEccSecretKey : EccSecretKey, IRemoteSecretKey` hold the stored SPKI, so algorithm checks, JWK export and
   verification apply unchanged. `IRemoteSecretKey` exposes `RemoteKeyBinding`, `SignAsync`, `DecryptAsync` and
   `DeriveSharedSecretAsync`. A type a service can hold implements `IRemoteSecretKeyTypeDescriptor` (RSA, ECC), on the
   key-type seam of [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md).
2. **Kinds.** An RSA remote key signs or decrypts and an ECC remote key signs or agrees, as its row's kind says. Each
   refuses another algorithm or kind with `InvalidOperationException`, and `ISecretKeyFactory.CreateRsaRemote` and
   `CreateEccRemote` refuse a type, size, curve or kind that differs from the row with `ArgumentException`.
3. **Stored shape.** A remote `PersistedSecret` has `SecretType` `rsa` or `ecc`, encoding `remote`, `EncodedValue` the
   `RemoteKeyReference` (`provider:keyId`, parsed at the first colon), `PublicKey` the SPKI read from the service,
   `KeySizeBits`, a required `Algorithm` and its row's `Use`. It needs no master key, because integrity comes from local
   verification.
4. **Fail-closed load.** The serializer reads a remote secret fail-closed: an unregistered provider, a missing public key
   or algorithm, a missing row or a use other than the row's throws `InvalidOperationException` naming the secret, and
   the tenant does not load.
5. **Verification.** Every remote signature is verified locally
   ([ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md)), a JWE's tag
   authenticates every remote decryption, and the JWKS never calls the service.
6. **Creation.** `ISecretGenerator.GenerateRemoteSecretAsync` resolves the lifecycle, descriptor, provider (which must
   create keys) and row before any call, creates the key, checks its public half, and hands a key it cannot keep to
   `IRemoteSecretDestroyer`
   ([ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)).
7. **The custody setting.** `tenant_key_provider` (`OpenIdSettingKeys.TenantKeyProvider`,
   `TenantKeyProviderSettingDescriptorDataSource`) chooses the custody of every provisioned key, signing and encryption
   alike: empty (local) or a provider that creates keys. It applies to tenants (platform and team documents carry it),
   is not discoverable, is environment-bound and merges by replacement.
8. **Start-up check.** The host does not start when a provider that creates keys has no row for a provisioned key of
   either use (`DefaultTenantKeyPolicy.ResolveSpecifications`), so Azure or Google deployments provision `RSA-OAEP*`
   keys rather than `ECDH-ES` keys.
9. **Specifications.** `TenantKeySpecification.RemoteKeyProvider` (null for local) is set by
   `ITenantKeyPolicy.ForTenant`, and `IsSatisfiedBy` requires the custody to match.
   `TenantKeyReconciliationHandler` compares specifications element by element.
10. **Custody change.** A custody change supersedes the managed serving keys that do not satisfy the specification. The
    successor activates at `H`: Max(now, earliest serving activation) when a serving key exists; else
    Min(now + activation delay, latest retirement of the active superseded keys); else now. Superseded keys retire with
    `custody_changed` (`TenantKeyChangeReasons.CustodyChanged`): active ones at `H`, upcoming ones at once, each expiring
    one grace later.
11. **Rollover.** `DefaultTenantKeyRollover` generates remote keys through `GenerateRemoteSecretAsync`, loads a protector
    only for local keys, sends `TenantKeyRolloverResult.RemovedSecrets` to the destroyer, and audits
    `remote_key_provider`.
12. **Successors.** A successor is a new version of the group's key (Google; Azure `{GroupName}-h` or `-s`) or, on AWS, a
    new key tagged with the group. Azure and Google keys are hardware-protected by default, and creation uses the row
    kind's key usage (AWS `SIGN_VERIFY`, `ENCRYPT_DECRYPT`, `KEY_AGREEMENT`).
13. **Rows.** AWS: RS, PS and ES 256-512, `RSA-OAEP` and `RSA-OAEP-256`, `ECDH-ES` and `ECDH-ES+A*KW`. Google: RS256,
    PS256, RS512 and PS512 (4096), ES256 and ES384, `RSA-OAEP`, `RSA-OAEP-256` and `RSA-OAEP-512`. Azure: RS, PS and ES
    256-512 and `RSA-OAEP` and `RSA-OAEP-256` through `UnwrapKey`. All three hold the defaults.
14. **Permissions.** AWS needs `kms:CreateKey`, `TagResource`, `GetPublicKey`, `Sign`, `Decrypt`, `DeriveSharedSecret`,
    `ScheduleKeyDeletion` and `DescribeKey`; Azure needs `Key Vault Crypto Officer`; Google needs create, get, view, use
    and destroy on the key ring.

## Options considered

- **A `RemoteAsymmetricSecretKey` base.** Rejected: it would duplicate the RSA and ECC key behavior the remote types
  inherit.
- **Fetching public keys per request.** Rejected: every token would depend on a second round trip, and the JWKS would
  call the service.
- **Unverified signatures.** Rejected: a wrong key or a tampered reference would issue tokens nobody can verify.
- **Protecting the reference.** Rejected: the reference is not a secret, and integrity comes from local verification.
- **A host-wide, per-algorithm or per-use setting.** Rejected: custody is one choice per tenant for every key.
- **Switching custody at once.** Rejected: tokens signed by the old keys must stay verifiable, so the rollover
  supersedes keys and lets them expire after a grace.
- **Exporting a key to agree locally.** Rejected: the private key would leave the service.
- **Azure `Decrypt`.** Rejected: Azure unwraps the content key through `UnwrapKey`.
- **Chosen:** remote subtypes of the RSA and ECC keys, one `tenant_key_provider` setting, local verification of every
  remote result and the rollover for a custody change.

## Consequences

- Private keys never leave the service, a replica sees only one message's ECDH secret, buffers are zeroed and logs carry
  ids.
- Each token costs a round trip, and an outage of the service stops issuance and decryption only for remote tenants.
- A provider without a row for a provisioned key stops the host at start, not at the first token.
- A change of custody is a rollover, so it needs no outage and no manual key handling.
- The "you chose wrong" smell is a tenant whose latency budget cannot take a round trip per token: it keeps local
  custody.

## References

- [ADR-1472](1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md): the asynchronous seam.
- [ADR-1474](1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md):
  the provider contract and its rows.
- [ADR-1478](1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md): the
  destroyer.
- [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md): the rollover a
  custody change reuses.
- [ADR-1169](1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md),
  [ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-honored-on-every-use.md),
  [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md).
- [RFC 7516](https://www.rfc-editor.org/rfc/rfc7516) section 11.5 and [RFC 7518](https://www.rfc-editor.org/rfc/rfc7518)
  sections 4.3 and 4.6.
