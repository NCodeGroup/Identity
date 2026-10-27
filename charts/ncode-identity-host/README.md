# ncode-identity-host

The Helm chart for `NCode.Identity.OpenId.Host`, the OpenID Connect and OAuth 2.0 server. It runs the host image as a
non-root deployment that meets the restricted Pod Security Standard, probes `/health/live` and `/health/ready`, and
drains each pod before stopping it
([ADR-0179](../../docs/decisions/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).
It requires Kubernetes 1.30 or later.

## Before the first install

The chart takes no secret values. It references Secrets you create in the release's namespace:

- `database.existingSecret`: the Postgres connection string, under the `connection-string` key.
- `secretProtection.local.keySecret`: a Secret holding the local root key under the `key` key (base64url, 32 bytes).
  The root key encrypts every stored secret and the Data Protection key ring, so back it up and never change it in
  place; see [Secret protection](#secret-protection).
- `secretProtection.recovery.publicKeyConfigMap`: a ConfigMap holding the recovery public key under the
  `recovery-public.pem` key, so losing the root key does not lose every stored secret; see
  [Secret protection](#secret-protection).
- `valkey.existingSecret`, for more than one replica: the Valkey connection string, under the `connection-string` key.
- `migrations.database.existingSecret`, optional: a connection string with the right to change the schema, used only
  by the migration Job. It defaults to `database.existingSecret`.

## Install

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.existingSecret=identity-database \
  --set secretProtection.rootKeyProvider=local --set secretProtection.local.keySecret=identity-root-key --set secretProtection.recovery.publicKeyConfigMap=identity-recovery-key
```

`ci/high-availability-values.yaml` shows a rolling three-replica release with Valkey, an ingress, and a retired local
root key.

## What the chart refuses

Rendering fails, before anything is applied, when:

- `allowedHosts` is empty or contains `*`;
- the probes would send a wildcard `Host` header: when the first allowed host is a wildcard such as `*.example.com`,
  set `probes.host` to a concrete host it accepts;
- `database.existingSecret` is empty;
- the `StaticSingle` strategy has no `https` issuer;
- `environment` is `Production` and `secretProtection.rootKeyProvider` is empty or `ephemeral`, which would leave no
  root key outside the pod;
- `environment` is `Production`, `secretProtection.rootKeyProvider` is `local`, and `secretProtection.local.keySecret`
  is empty;
- `secretProtection.rootKeyProvider` is `aws-kms`, in any environment, and `secretProtection.awsKms.keyArn` is empty or
  is not the ARN of a KMS key or alias;
- `secretProtection.rootKeyProvider` is `azure-key-vault`, in any environment, and
  `secretProtection.azureKeyVault.keyId` is empty or is not the identifier of a Key Vault or Managed HSM key, or only
  one of `secretProtection.azureKeyVault.workloadIdentity.tenantId` and `clientId` is set;
- `secretProtection.rootKeyProvider` is `google-cloud-kms`, in any environment, and
  `secretProtection.googleCloudKms.keyName` is empty or is not the resource name of a Cloud KMS crypto key, or
  `secretProtection.googleCloudKms.workloadIdentityFederation.provider` is set and is not the resource name of a
  workload identity pool provider;
- `environment` is `Production`, `secretProtection.recovery.publicKeyConfigMap` is empty, and
  `secretProtection.recovery.disabled` is not `true`, because losing the root key would then lose every stored secret;
- `replicaCount` is above 1 without `valkey.existingSecret`, because replicas share single-use state through Valkey;
- a value is unknown or has the wrong type.

## Migrations

The host does not migrate on boot. Before each install and upgrade, a Helm hook Job runs the migration worker from the
host's image and applies the pending EF Core migrations. Helm waits for it: a failed migration fails the release before
any pod is replaced, and the Job stays until the next release so its logs can be read. Helm's `--timeout` must cover
the migrations.

The previous version keeps serving while the Job runs and until the rollout ends, so every migration must keep it
working: add first, and drop or rename only in a later release
([ADR-0180](../../docs/decisions/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). A `helm rollback` runs no migration.

To apply migrations another way, such as with a generated idempotent script, set `migrations.enabled=false`.

## Updates

With Valkey, a new replica becomes ready before an old one stops. Without it, each replica keeps single-use state in its
own process, so the chart never runs two at once: it uses the `Recreate` strategy, which starts the new replica only
after the old one has terminated, so each update has a short outage.

## Secret protection

The host encrypts every stored secret, and the Data Protection key ring, under a root key held outside the database, so
a database dump or backup is useless without it
([ADR-0235](../../docs/decisions/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
`secretProtection.rootKeyProvider` chooses where the root key lives, and Production refuses an empty or `ephemeral`
choice:

- `local`: an AES key in a Secret the chart mounts into the pod. It suits a homelab: the key is outside the database,
  but anyone who can read the Secret can read the key.
- `aws-kms`: a key in AWS KMS, which never leaves it. The host reaches it with web identity federation, so no AWS
  credential is stored in the cluster; see [AWS KMS](#aws-kms).
- `azure-key-vault`: an RSA key in Azure Key Vault or Managed HSM, which never leaves it. The host reaches it with
  workload identity federation, so no Azure credential is stored in the cluster; see
  [Azure Key Vault](#azure-key-vault).
- `google-cloud-kms`: a key in Google Cloud KMS, which never leaves it. The host reaches it with workload identity
  federation, so no Google Cloud credential is stored in the cluster; see [Google Cloud KMS](#google-cloud-kms).

Whichever provider you choose, also create the recovery key below, or set `secretProtection.recovery.disabled=true` to
accept that losing the root key loses every stored secret.

### Creating the local root key Secret

Generate 32 random bytes, encode them as base64url, and store them under the `key` key of a Secret in the release's
namespace:

```powershell
$b = [byte[]]::new(32); [Security.Cryptography.RandomNumberGenerator]::Fill($b)
$key = [Convert]::ToBase64String($b).TrimEnd('=').Replace('+','-').Replace('/','_')
kubectl -n identity create secret generic identity-root-key --from-literal=key=$key
```

Set `secretProtection.rootKeyProvider=local` and `secretProtection.local.keySecret=identity-root-key`. Back the key up
offline: losing it without a recovery key loses every secret.

### Creating the recovery key

Do this once per deployment:

```powershell
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:4096 -out recovery-private.pem
openssl pkey -in recovery-private.pem -pubout -out recovery-public.pem
kubectl -n identity create configmap identity-recovery-key --from-file=recovery-public.pem
```

Set `secretProtection.recovery.publicKeyConfigMap=identity-recovery-key`. Store `recovery-private.pem` offline only, for
example split with a Shamir tool (such as `ssss-split -t 2 -n 3`) among three people, or encrypted in two separate
offline locations, then delete it from the workstation. Record who holds which share.

### Rotating a local root key

Each master key is wrapped by the current root key when it is created, and only that key can unwrap it again. Rotate
the root key by hand:

1. Create the new root key in a **new** Secret.
2. Add the old Secret to `secretProtection.local.previousKeySecrets` and name the new one in
   `secretProtection.local.keySecret`, then upgrade the release. New master keys use the new root key; master keys the
   old one wrapped keep unwrapping.
3. Keep the old Secret listed until the rewrap tool has run, so that no master key still depends on it.

**Never replace a root key in place.** New contents written into the same Secret replace the only key that can unwrap
the existing master keys. After the next restart the stored secrets cannot be read, and only the recovery key can
restore them.

### AWS KMS

The root key is a customer-managed AWS KMS key. The host reaches it with web identity federation: the chart projects a
service-account token into the pod, and the AWS SDK exchanges it with AWS STS for short-lived credentials of an IAM
role, so nothing is stored. The host calls only `kms:Encrypt` and `kms:Decrypt`, and CloudTrail records every call
with its encryption context, which holds a key id and an owner, never secret material. Set it up once per deployment:

1. **Create the key.** A symmetric key (`SYMMETRIC_DEFAULT`) with yearly automatic rotation and an alias such as
   `alias/ncode-identity-root`. The platform's `identity-platform` OpenTofu `aws-kms-key` module creates one; the key
   is one more instance of it.
2. **Check that the cluster's issuer is publicly discoverable.** AWS fetches the service-account issuer's discovery
   document and signing keys itself, so both must answer unauthenticated requests from the internet. Run this from
   outside the cluster:

   ```bash
   ISSUER=$(kubectl get --raw /.well-known/openid-configuration | jq -r .issuer)
   curl -fsS "$ISSUER/.well-known/openid-configuration" | jq -r .jwks_uri | xargs curl -fsS
   ```

   If it fails, for example because the issuer is an in-cluster name, web identity cannot work: use the `local`
   provider until IAM Roles Anywhere is supported.
3. **Register the issuer as an IAM OIDC identity provider**, with the issuer URL and the client ID
   `sts.amazonaws.com`.
4. **Create the role the host assumes.** Its trust policy allows `sts:AssumeRoleWithWebIdentity` for that provider only
   when the token's `sub` is the host's service account, `system:serviceaccount:<namespace>:<service account>`, and its
   `aud` is `sts.amazonaws.com`. The service account is `serviceAccount.name`, or the release's full name when that is
   empty.
5. **Allow the role to use the one key**, and only with the encryption context key the host sends:

   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": ["kms:Encrypt", "kms:Decrypt"],
         "Resource": "<key ARN>",
         "Condition": {
           "ForAllValues:StringEquals": { "kms:EncryptionContextKeys": ["ncode-identity:root-context"] },
           "Null": { "kms:EncryptionContextKeys": "false" }
         }
       }
     ]
   }
   ```

Then set `secretProtection.rootKeyProvider=aws-kms`,
`secretProtection.awsKms.keyArn=arn:aws:kms:<region>:<account>:alias/ncode-identity-root`, and
`secretProtection.awsKms.webIdentity.roleArn=arn:aws:iam::<account>:role/ncode-identity-root-key`;
`ci/aws-kms-values.yaml` shows a release. The chart sets `AWS_REGION` from the key's ARN. When `roleArn` is set, it also
mounts a projected service-account token (audience `secretProtection.awsKms.webIdentity.audience`, default
`sts.amazonaws.com`, valid for `tokenExpirationSeconds`, at least 600) read-only at
`/var/run/secrets/tokens/aws/token`, and sets `AWS_ROLE_ARN`, `AWS_WEB_IDENTITY_TOKEN_FILE`, and `AWS_ROLE_SESSION_NAME`
(the release's full name). It does this only for the `aws-kms` provider, so a pod that uses another provider never
carries AWS credentials. The pod still does not mount the default service-account token.

### Azure Key Vault

The root key is an RSA key in Azure Key Vault or Managed HSM. The host reaches it with workload identity federation: the
chart projects a service-account token into the pod, and Azure.Identity exchanges it with Microsoft Entra ID for a
short-lived token of a managed identity or app registration, so nothing is stored. The host calls only the key's wrap
and unwrap operations, with `RSA-OAEP-256`. Key Vault's RSA-OAEP binds no context, so unlike AWS KMS the call carries no
encryption context; each master key's key-check value binds it to its owner instead. Set it up once per deployment:

1. **Create the key.** An RSA key of 3072 bits or more, in a vault or Managed HSM with purge protection on. An
   `RSA-HSM` key in a Premium vault, or a key in a Managed HSM, is recommended. A key identifier without a version wraps
   with the key's current version, and every wrapped key records its version, so rotating the key in Key Vault keeps
   older master keys unwrappable as long as their versions stay enabled.
2. **Check that the cluster's issuer is publicly discoverable**, with the `curl` check in step 2 of
   [AWS KMS](#aws-kms). Microsoft Entra ID fetches the issuer's discovery document and signing keys itself.
3. **Create the identity the host uses**: a user-assigned managed identity or an app registration. Give it a federated
   identity credential with the cluster's issuer, the subject `system:serviceaccount:<namespace>:<service account>`,
   and the audience `api://AzureADTokenExchange`. The service account is `serviceAccount.name`, or the release's full
   name when that is empty.
4. **Allow the identity to use the one key.** Assign it the role "Key Vault Crypto Service Encryption User" on the key
   only, not on the vault. For a key in a Managed HSM, assign instead the Managed HSM local role "Managed HSM Crypto
   Service Encryption User" at the key's scope.

Then set `secretProtection.rootKeyProvider=azure-key-vault`,
`secretProtection.azureKeyVault.keyId=https://<vault>.vault.azure.net/keys/ncode-identity-root`,
`secretProtection.azureKeyVault.workloadIdentity.tenantId=<tenant ID>`, and
`secretProtection.azureKeyVault.workloadIdentity.clientId=<client ID>`; `ci/azure-key-vault-values.yaml` shows a
release. The chart sets `SecretProtection__RootKeyProviders__AzureKeyVault__KeyId`. When `clientId` is set, it also
mounts a projected service-account token (audience `secretProtection.azureKeyVault.workloadIdentity.audience`, default
`api://AzureADTokenExchange`, valid for `tokenExpirationSeconds`, at least 600) read-only at
`/var/run/secrets/tokens/azure/token`, and sets `AZURE_TENANT_ID`, `AZURE_CLIENT_ID`, `AZURE_FEDERATED_TOKEN_FILE`, and
`AZURE_TOKEN_CREDENTIALS=WorkloadIdentityCredential`, so Azure.Identity tries exactly that one credential. It does this
only for the `azure-key-vault` provider, so a pod that uses another provider never carries Azure credentials. When the
cluster supplies Azure credentials itself, leave `workloadIdentity` empty: the chart then sets only the key identifier.

### Google Cloud KMS

The root key is a symmetric key in Google Cloud KMS. The host reaches it with workload identity federation: the chart
projects a service-account token into the pod, and Application Default Credentials exchange it with Google STS for a
short-lived access token, so nothing is stored. The host calls only `Encrypt` and `Decrypt`, binds each wrap to its
owner through additional authenticated data, and checks CRC32C checksums on every request and response. Set it up once
per deployment:

1. **Create the key.** A symmetric key (purpose `ENCRYPT_DECRYPT`) with the `HSM` protection level and yearly automatic
   rotation.
2. **Create a workload identity pool and an OIDC provider that trusts the cluster.** Give the provider the cluster's
   issuer and its signing keys, uploaded as a JWKS file, so Google never fetches them and the issuer need not be
   public. Map `google.subject` to the token's `sub`, and keep the provider's default allowed audience,
   `https://iam.googleapis.com/` followed by the provider's resource name, which is the audience of the token the chart
   projects:

   ```bash
   ISSUER=$(kubectl get --raw /.well-known/openid-configuration | jq -r .issuer)
   kubectl get --raw /openid/v1/jwks > jwks.json
   gcloud iam workload-identity-pools create ncode-identity --location=global
   gcloud iam workload-identity-pools providers create-oidc cluster --location=global \
     --workload-identity-pool=ncode-identity --issuer-uri="$ISSUER" --jwk-json-path=jwks.json \
     --attribute-mapping=google.subject=assertion.sub
   ```

   The provider trusts only the keys uploaded to it, so monitor the cluster's JWKS for changes and upload it again when
   it changes.
3. **Allow the host's service account to use the one key.** Its federated principal is

   ```text
   principal://iam.googleapis.com/projects/<project number>/locations/global/workloadIdentityPools/<pool>/subject/system:serviceaccount:<namespace>:<service account>
   ```

   where the service account is `serviceAccount.name`, or the release's full name when that is empty. Then either:

   - grant that principal `roles/cloudkms.cryptoKeyEncrypterDecrypter` on the key, and leave `serviceAccountEmail`
     empty; or
   - grant that principal `roles/iam.workloadIdentityUser` on a Google service account, grant
     `roles/cloudkms.cryptoKeyEncrypterDecrypter` on the key to `serviceAccount:<email>`, and set `serviceAccountEmail`
     to that email. The host then impersonates the service account.

Then set `secretProtection.rootKeyProvider=google-cloud-kms`,
`secretProtection.googleCloudKms.keyName=projects/<project>/locations/<location>/keyRings/<ring>/cryptoKeys/<key>`,
and `secretProtection.googleCloudKms.workloadIdentityFederation.provider` to the provider's resource name,
`projects/<project number>/locations/global/workloadIdentityPools/<pool>/providers/<provider>`;
`ci/google-cloud-kms-values.yaml` shows a release that impersonates a service account. The chart sets
`SecretProtection__RootKeyProviders__GoogleCloudKms__KeyName`. When `provider` is set, it also renders a ConfigMap,
`<release full name>-google-cloud`, holding an `external_account` credential configuration, and projects it together
with a service-account token (audience `https://iam.googleapis.com/<provider>`, valid for `tokenExpirationSeconds`, at
least 600) read-only at `/var/run/secrets/tokens/google`. It sets `GOOGLE_APPLICATION_CREDENTIALS` to
`/var/run/secrets/tokens/google/credential-configuration.json`, and annotates the pod with the configuration's
checksum, so a change restarts it. It does this only for the `google-cloud-kms` provider, so a pod that uses another
provider never carries Google Cloud credentials. On GKE with Workload Identity Federation for GKE, which supplies
credentials itself, leave `provider` empty: the chart then sets only the key name.

### A key the master key cannot decrypt

The host refuses to start while the Data Protection key ring holds a live key, one that is not revoked and has not
expired, that the server's master key cannot decrypt, and it names each such key in the error
([ADR-0247](../../docs/decisions/0247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md)). It happens when the
master key rows are lost or recreated while the key ring survives, such as after restoring part of a backup.

Restore the master keys if you can. Otherwise list each ID in `extraEnv` as `DataProtectionKeyRing__RevokedKeyIds__0`,
`DataProtectionKeyRing__RevokedKeyIds__1`, and so on, and upgrade the release:

```yaml
extraEnv:
  - name: DataProtectionKeyRing__RevokedKeyIds__0
    value: 5b0f5a54-2c1e-4a2f-9a3c-0d6f1c8e7b21
```

The host revokes the listed keys as it starts, and Data Protection creates a key in their place. The sign-in cookies and
anti-forgery tokens that the revoked keys protected stop working. The revocation is stored in the key ring, so the
entries may be removed afterwards.

## Strict-Transport-Security

Outside Development, the host sends `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every response
to a request that reached it over HTTPS
([ADR-0258](../../docs/decisions/0258-the-server-sends-strict-transport-security-outside-development.md)). Behind the
ingress the pod serves plain HTTP, so the host sees HTTPS only through the `X-Forwarded-Proto` header of a proxy that
`reverseProxy` trusts: set `reverseProxy.knownNetworks` or `reverseProxy.knownProxies` to cover the ingress controller,
or the header is not sent.

When the ingress controller sends the header itself, set `StrictTransportSecurity__Enabled=false` in `extraEnv`, so
one component owns it. Change the policy the same way with `StrictTransportSecurity__MaxAge` (for example
`730.00:00:00`), `StrictTransportSecurity__IncludeSubDomains` and `StrictTransportSecurity__Preload`. Set `Preload`
only after submitting the domain to the browsers' preload list: it requires `IncludeSubDomains` and a `MaxAge` of at
least 365 days, and the host refuses to start without them.

## Other settings

The chart models the settings it validates. Pass any other host setting as an environment variable through `extraEnv`
or `extraEnvFrom`, for example `Logging__LogLevel__Default`. The migration Job receives `extraEnv`, `extraEnvFrom`,
`extraVolumes`, and `extraVolumeMounts` as well, so a setting the database connection needs, such as a CA bundle for
TLS to Postgres, reaches both the host and the migrations.

The image writes one JSON object per line to standard output, with a UTC timestamp, so a log collector keeps an entry
and its exception in one record
([ADR-0248](../../docs/decisions/0248-the-host-image-writes-one-line-json-logs.md)). Set
`Logging__Console__FormatterName=simple` in `extraEnv` to write text instead.
