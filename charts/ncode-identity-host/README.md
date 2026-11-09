# ncode-identity-host

The Helm chart for `NCode.Identity.OpenId.Host`, the OpenID Connect and OAuth 2.0 server. It runs the host image as a
non-root deployment that meets the restricted Pod Security Standard, probes `/health/live` and `/health/ready`, and
drains each pod before stopping it
([ADR-1179](../../docs/decisions/1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).
Setting `managementConsole.enabled` also deploys the management console host on a host name of its own
([ADR-1458](../../docs/decisions/1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)).
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
- `secretProtection.rootKeyProvider` is empty, in any environment, because the host does not start without one;
- `environment` is `Production` and `secretProtection.rootKeyProvider` is `ephemeral`, which would leave no root key
  outside the pod;
- `environment` is `Production`, `secretProtection.rootKeyProvider` is `local`, and `secretProtection.local.keySecret`
  is empty;
- `secretProtection.rootKeyProvider` is `aws-kms`, in any environment, and `secretProtection.awsKms.keyArn` is empty or
  is not the ARN of a KMS key or alias;
- `secretProtection.rootKeyProvider` is `azure-key-vault`, in any environment, and
  `secretProtection.azureKeyVault.keyId` is empty or is not the identifier of a Key Vault or Managed HSM key;
- `secretProtection.rootKeyProvider` is `google-cloud-kms`, in any environment, and
  `secretProtection.googleCloudKms.keyName` is empty or is not the resource name of a Cloud KMS crypto key;
- a `secretProtection.remoteKeyProviders` value is malformed: `awsKms.region` is not a system name such as `us-west-2`,
  `awsKms.pendingWindowInDays` is outside 7 to 30, `azureKeyVault.vaultUri` is not an `https` URI, or
  `googleCloudKms.keyRing` is not the resource name of a key ring;
- a key management service is used, as `secretProtection.rootKeyProvider`, in `secretProtection.ownerRootKeys.providers`
  or under `secretProtection.remoteKeyProviders`, and its workload credentials are half set: only one of
  `secretProtection.azureKeyVault.workloadIdentity.tenantId` and `clientId` is set, or
  `secretProtection.googleCloudKms.workloadIdentityFederation.provider` is set and is not the resource name of a
  workload identity pool provider. Workload credentials themselves are optional: a provider without them signs in
  with the pod's ambient identity, such as EKS Pod Identity or Workload Identity Federation for GKE;
- `environment` is `Production`, `secretProtection.recovery.publicKeyConfigMap` is empty, and
  `secretProtection.recovery.disabled` is not `true`, because losing the root key would then lose every stored secret;
- `replicaCount` is above 1 without `valkey.existingSecret`, because replicas share single-use state through Valkey;
- `managementConsole.enabled` is `true` and `managementConsole.host` is empty or contains `*`;
- `managementConsole.enabled` is `true` and `allowedHosts` does not contain `managementConsole.host`, because the
  management host routes `/api` to the server;
- `managementConsole.enabled` is `true` and the console's authority is empty or does not start with `https://`;
- `managementConsole.enabled` is `true` and `managementConsole.keys.clientKeySecret` or
  `managementConsole.keys.dpopKeySecret` is empty;
- `managementConsole.replicaCount` is above 1 without `managementConsole.valkey.existingSecret`, because replicas share
  their sessions through Valkey;
- a value is unknown or has the wrong type.

## Migrations

The host does not migrate on boot. Before each install and upgrade, a Helm hook Job runs the migration worker from the
host's image and applies the pending EF Core migrations. Helm waits for it: a failed migration fails the release before
any pod is replaced, and the Job stays until the next release so its logs can be read. Helm's `--timeout` must cover
the migrations.

The previous version keeps serving while the Job runs and until the rollout ends, so every migration must keep it
working: add first, and drop or rename only in a later release
([ADR-1180](../../docs/decisions/1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). A `helm rollback` runs no migration.

To apply migrations another way, such as with a generated idempotent script, set `migrations.enabled=false`.

## Updates

With Valkey, a new replica becomes ready before an old one stops. Without it, each replica keeps single-use state in its
own process, so the chart never runs two at once: it uses the `Recreate` strategy, which starts the new replica only
after the old one has terminated, so each update has a short outage.

## Secret protection

The host encrypts every stored secret, and the Data Protection key ring, under a root key held outside the database, so
a database dump or backup is useless without it
([ADR-1235](../../docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
`secretProtection.rootKeyProvider` chooses where the root key lives, is required, and Production refuses `ephemeral`:

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
3. Keep the old Secret listed until the secret protection Job has rewrapped every master key under the new root key (see
   [Rewrapping master keys](#rewrapping-master-keys)), so that no master key still depends on it.

**Never replace a root key in place.** New contents written into the same Secret replace the only key that can unwrap
the existing master keys. After the next restart the stored secrets cannot be read, and only the recovery key can
restore them.

### Rewrapping master keys

A master key is wrapped by the root key provider that was configured when it was created, and again under the recovery
public key. When the root key provider, the root key or the recovery key pair changes, the existing master keys still
name the old ones. `secretProtection.rewrapJob` runs the host image's offline `secret-protection` command once, as a Job,
to wrap every master key under the configured root key provider and recovery public key
([ADR-1408](../../docs/decisions/1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)):

| Value                                            | Meaning                                                                                              |
|--------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `secretProtection.rewrapJob.enabled`             | Renders the Job. Set it for the release that needs the run, and unset it afterwards.                 |
| `secretProtection.rewrapJob.command`             | `rewrap` (the default) or `rewrap-recovery`, which replaces only the recovery copies.                |
| `secretProtection.rewrapJob.owner`               | Limits the run to one owner such as `tenant:acme` or `platform:platform`; empty rewraps every owner.    |
| `secretProtection.rewrapJob.dryRun`              | `true` unwraps and verifies every master key and writes nothing.                                     |
| `secretProtection.rewrapJob.recoveryPrivateKeySecret` | A Secret holding the recovery private key under `recovery-private.pem`, mounted into the Job only. |
| `secretProtection.rewrapJob.backoffLimit`, `activeDeadlineSeconds` | How often the Job retries and how long it may run (`0` and `3600`).              |

The Job is named for the release revision, runs as the host does, and reads the same configuration, so it reaches the
root key the pods reach. Each master key is unwrapped by the root key provider it names, so the previous provider must
stay registered and configured for the run: keep the old Secret in `secretProtection.local.previousKeySecrets`, or the
old KMS key reachable. When the root key is lost, the Job unwraps each master key from its recovery copy instead, with
the recovery private key:

```powershell
kubectl -n identity create secret generic identity-recovery-private-key --from-file=recovery-private.pem
helm upgrade identity ./charts/ncode-identity-host --reuse-values `
  --set secretProtection.rewrapJob.enabled=true `
  --set secretProtection.rewrapJob.recoveryPrivateKeySecret=identity-recovery-private-key
kubectl -n identity wait --for=condition=complete job -l app.kubernetes.io/component=secret-protection --timeout=1h
```

Start with `secretProtection.rewrapJob.dryRun=true` to prove that every master key unwraps. The private key is read from
the mounted file only, held in memory that is zeroed when the command ends, and never reaches a serving pod. When the
Job has completed, set `secretProtection.rewrapJob.enabled=false` and delete the recovery private key Secret.

### Rotating the recovery key

To replace the recovery key pair, create the new pair as in [Creating the recovery key](#creating-the-recovery-key),
point `secretProtection.recovery.publicKeyConfigMap` at the ConfigMap holding the new public key, and run the Job with
`secretProtection.rewrapJob.command=rewrap-recovery`. Every master key's recovery copy is replaced with one under the new
key; the root wraps stay as they are. Keep the old private key until the Job has completed, then retire it.

### Rotating master keys

Master keys rotate on their own: after `SecretProtection:MasterKeys:RotationPeriod` (365 days by default) the active key
of the platform and of each tenant retires and the next version takes over, a tenant's values, and a platform value that
a registered protected-value source reports, are rewrapped under the new version, and a retired key is destroyed once
its grace has passed and nothing names it: the platform's retired version stays while a live Data Protection key
names it, because an existing Data Protection key is never re-encrypted
([ADR-1407](../../docs/decisions/1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md)).
An administrator rekeys at once with `POST /api/tenants/{tenantId}/encryption-keys/rekey` or
`POST /api/platform/encryption-keys/rekey`, and reads the keys with `GET .../encryption-keys`. Tune the
lifecycle with environment variables in `extraEnv`: `SecretProtection__MasterKeys__RotationPeriod`,
`SecretProtection__MasterKeys__RefreshInterval`, `SecretProtection__MasterKeys__RetirementGrace`,
`SecretProtection__MasterKeys__MaintenanceInterval` and `SecretProtection__MasterKeys__DestroyRetired` (`false` keeps
retired keys for good). A database backup keeps the master keys it held, so it can still be read with the root key that
wrapped them.

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

### Tenant root keys

A tenant can wrap its own master keys under a root key it controls and can revoke, with no recovery copy that the
operator holds
([ADR-1414](../../docs/decisions/1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)). The server
reaches the tenant's key with the deployment's own workload identity, so the operator opts in per provider.
`secretProtection.ownerRootKeys.providers` lists the key wrappers a tenant may choose from `aws-kms`, `azure-key-vault`
and `google-cloud-kms`, and is empty by default, which means no tenant can bring a key. The chart renders it as
`SecretProtection__OwnerRootKeys__Providers__<index>`; `ci/tenant-root-keys-values.yaml` shows a release.

The provider list does not choose the deployment's root key: `secretProtection.rootKeyProvider` still does, and the
tenant's key is independent of it. The control-plane tenant cannot bring a root key, because the management API
authenticates against it and a revoked key would lock out the administrators who restore it; its keys stay under the
deployment's root key.

**Credentials.** The pods carry a provider's workload credentials when it is the root key provider, is listed in
`secretProtection.ownerRootKeys.providers` or is configured under `secretProtection.remoteKeyProviders`: for AWS KMS the web identity (`secretProtection.awsKms.webIdentity.roleArn`),
for Azure Key Vault the workload identity (`secretProtection.azureKeyVault.workloadIdentity.clientId` and `tenantId`),
and for Google Cloud KMS the federation (`secretProtection.googleCloudKms.workloadIdentityFederation.provider`), each
set up as in the sections above. The provider's own key values (`awsKms.keyArn`, `azureKeyVault.keyId`,
`googleCloudKms.keyName`) are rendered only when it is the root key provider, because a tenant names its key itself. A
listed provider with no workload-identity values uses the pod's ambient identity, as the root key provider does. A
half-set value, such as an Azure `clientId` without a `tenantId`, or a Google Cloud `provider` that is not a pool
provider resource name, is refused. A pod never carries the credentials of a provider that is neither.

**What a tenant grants.** The tenant gives the deployment's identity the permission to wrap and unwrap with its one key,
and nothing else; the server calls no other operation:

- AWS KMS: the key policy allows the IAM role in `secretProtection.awsKms.webIdentity.roleArn` to call `kms:Encrypt` and
  `kms:Decrypt` on the key, as the policy in [AWS KMS](#aws-kms) shows, including the condition on the encryption
  context key `ncode-identity:root-context`. For a key in another AWS account, the role's own policy must also allow the
  key.
- Azure Key Vault: assign the deployment's identity the role "Key Vault Crypto Service Encryption User" on the key only,
  or, for a key in a Managed HSM, the local role "Managed HSM Crypto Service Encryption User" at the key's scope.
- Google Cloud KMS: grant `roles/cloudkms.cryptoKeyEncrypterDecrypter` on the key to the federated principal, or to the
  service account in `secretProtection.googleCloudKms.workloadIdentityFederation.serviceAccountEmail` when the host
  impersonates one.

The tenant then sets its key through the management API, `PUT /api/tenants/{tenantId}/encryption-keys/root-key` with the
key wrapper name and the key reference, or in the console's encryption keys tab. The server validates the key by wrapping
and unwrapping a probe through it, rotates the tenant onto master key material created under it, and rewraps the
tenant's retired keys under it. Material that existed earlier remains in backups under the deployment's root key and its
recovery copy; every value written or rewrapped afterwards is readable only while the tenant's key is.

**Intervals.** The host probes an `active` tenant key every `SecretProtection:OwnerRootKeys:ValidationInterval` (one
hour by default, between one minute and one day), and retries an `unavailable` or `removing` one every
`SecretProtection:OwnerRootKeys:RetryInterval` (one minute by default, between one second and the validation interval).
Replicas reuse material unwrapped under a tenant key for no longer than the validation interval. Tune them in `extraEnv`
with `SecretProtection__OwnerRootKeys__ValidationInterval` and `SecretProtection__OwnerRootKeys__RetryInterval`, for
example `01:00:00` and `00:01:00`.

**Revocation.** When the tenant revokes, disables or destroys its key, or the deployment's identity loses its grant, the
next unwrap through it fails on a replica, and that replica logs the failure and refuses the tenant for the retry
interval. The next failed probe marks the key `unavailable`: the tenant's data is unreadable on every replica, the host
writes a log entry and an audit event, the management API answers `503` with `Retry-After` for that tenant, and the
console shows a banner. The server fails closed, so nothing reads or writes the tenant's secrets until the key is
restored; restoring the grant makes the next retry succeed and the tenant readable again. Removing the key moves the
tenant's master keys back under the deployment's root key.

**No recovery copy.** A master key under a tenant key has no recovery copy: the recovery public key and the recovery
private key never touch it, so the operator cannot read the tenant's data without the tenant's key. Destroying the key,
without removing it first, loses every value written or rewrapped under it, in the database and in every backup.

### Tenant keys in a key management service

A tenant's signing and encryption keys can be held in a key management service, which signs every token and decrypts
every content key, so the private key never reaches the server
([ADR-1471](../../docs/decisions/1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md),
[ADR-1477](../../docs/decisions/1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md)).
The operator registers the providers a tenant may choose from, and the host reaches each with the deployment's own
workload identity. The values are:

- `secretProtection.remoteKeyProviders.awsKms.region`, and `pendingWindowInDays`, 7 to 30 (default 30), the days a
  deleted key waits before AWS destroys it;
- `secretProtection.remoteKeyProviders.azureKeyVault.vaultUri`, and `hardwareProtected` (default `true`);
- `secretProtection.remoteKeyProviders.googleCloudKms.keyRing`, and `hardwareProtected` (default `true`);
- `secretProtection.remoteKeys.deployment`, which names this release's keys in a shared KMS scope (an AWS account, a
  vault or a key ring): lowercase letters, digits and hyphens, at most 40 characters. It is required when a provider is
  registered. A deployment is one database of stored secrets: every replica and release that shares the database uses
  its name, and no other deployment in the same AWS account, vault or key ring uses it, because each deployment
  destroys every key of its name that its own database does not name;
- `secretProtection.remoteKeys.sweepEnabled` (default `true`), `sweepInterval` (default `1.00:00:00`) and
  `sweepGracePeriod` (default `1.00:00:00`, between one hour and 365 days), which control the background sweep that
  destroys the keys no stored secret names
  ([ADR-1478](../../docs/decisions/1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)).

The chart renders them as `SecretProtection__RemoteKeyProviders__AwsKms__*`, `__AzureKeyVault__*` and
`__GoogleCloudKms__*`, and the deployment as `SecretProtection__RemoteKeys__Deployment` together with
`SecretProtection__RemoteKeys__SweepEnabled`, `__SweepInterval` and `__SweepGracePeriod`. A provider is registered when
its first value (`region`, `vaultUri` or `keyRing`) is set, as the host registers a provider when its configuration
section exists; a provider with no value is not registered, and no tenant can choose it. A release that registers a
provider without `secretProtection.remoteKeys.deployment` is refused:
`secretProtection.remoteKeys.deployment must name this deployment when a remote key provider is set: lowercase letters,
digits and hyphens.` The sweep destroys only keys tagged with the deployment, older than the grace period, that no
stored secret names. Registering a provider selects it: the host does not start unless it holds every signing and
encryption key the database provisions through it. `ci/remote-key-providers-values.yaml` shows a release with all
three.

**Credentials.** A provider that is configured reuses the credentials set up for it in the sections above, so a pod
carries them: for AWS KMS the web identity (`secretProtection.awsKms.webIdentity.roleArn`), for Azure Key Vault the
workload identity (`secretProtection.azureKeyVault.workloadIdentity.clientId` and `tenantId`), and for Google Cloud KMS
the federation (`secretProtection.googleCloudKms.workloadIdentityFederation.provider`). A provider with no
workload-identity values uses the pod's ambient identity. The chart renders such a provider on purpose, as it does a
root key provider, because a cluster can supply the identity itself; it refuses only a half-set value. A pod never carries the credentials of a provider that is not
the root key provider, listed in `secretProtection.ownerRootKeys.providers` or configured here. One identity serves
each vendor, so the root key, a tenant's root key and the tenant's keys share it.

**What the identity needs.** The deployment's identity creates, uses, lists and destroys the keys it creates; the
server creates sign-only or decrypt-only keys:

- AWS KMS: `kms:CreateKey`, `kms:TagResource`, `kms:GetPublicKey`, `kms:Sign`, `kms:Decrypt`,
  `kms:DeriveSharedSecret`, `kms:ScheduleKeyDeletion`, `kms:DescribeKey`, `kms:ListKeys` and `kms:ListResourceTags`.
- Azure Key Vault: the `Key Vault Crypto Officer` role on the vault.
- Google Cloud KMS: `cloudkms.cryptoKeys.create` and `cloudkms.cryptoKeys.list`, and
  `cloudkms.cryptoKeyVersions.create`, `.list`, `.get`, `.viewPublicKey`, `.useToSign`, `.useToDecrypt` and `.destroy`
  on the key ring.

A key an operator binds through the management API needs only its own operation: AWS `kms:Sign`, `kms:Decrypt` or
`kms:DeriveSharedSecret`, Azure `Key Vault Crypto User` on the key, Google `cloudkms.cryptoKeyVersions.useToSign` or
`.useToDecrypt`.

Scope the identity to the deployment's keys: on AWS, condition `kms:ScheduleKeyDeletion` on
`aws:ResourceTag/ncode-identity:deployment` equal to the deployment name, and `kms:CreateKey` and `kms:TagResource` on
`aws:RequestTag/ncode-identity:deployment`, so it creates and destroys only keys of its deployment (`kms:ListKeys`
names no resource, and a bound key carries no such tag); on Azure, a role assignment cannot be conditioned on a key's
tags, so give each deployment a vault of its own and a custom role without delete and purge; on Google, a grant on a key
ring covers every crypto key in it, so give each deployment a key ring of its own.

**Choosing a provider.** A tenant, a team or the platform selects a provider with the setting
`tenant_key_provider`, whose value is empty (local keys) or one of `aws-kms`, `azure-key-vault` and
`google-cloud-kms`, and which chooses the provider of every key the tenant provisions, signing and encryption alike.
The management API creates a tenant's key in a registered provider and binds a key a provider already holds. See
[Remote keys](../../docs/capabilities/remote-keys.md).

### A key the master key cannot decrypt

The host refuses to start while the Data Protection key ring holds a live key, one that is not revoked and has not
expired, that the platform's master key cannot decrypt, and it names each such key in the error
([ADR-1247](../../docs/decisions/1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md)). It happens when the
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

## Management console

With `managementConsole.enabled=true` the chart also deploys the management console host: a Deployment, a Service, a
ServiceAccount that mounts no token, a PodDisruptionBudget, and an Ingress on `managementConsole.host` that routes
`/api` to the server and every other path to the console. The console runs its own image, which shares the chart's
`appVersion`
([ADR-1458](../../docs/decisions/1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)).

Create the two private keys the console's BFF signs with, and a Secret for each, in the release's namespace:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out mgmt-client.pem
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out mgmt-dpop.pem
kubectl create secret generic identity-mgmt-client-key --from-file=mgmt-client.pem
kubectl create secret generic identity-mgmt-dpop-key --from-file=mgmt-dpop.pem
```

Name the Secrets in `managementConsole.keys.clientKeySecret` and `managementConsole.keys.dpopKeySecret`. The chart
mounts them read-only and points `Bff__KeyRings__mgmt-client__PemFiles__0` and
`Bff__KeyRings__mgmt-dpop__PemFiles__0` at the files.

Register `mgmt-console` with the server as a confidential client of the control-plane tenant, with the metadata of
[ADR-1437](../../docs/decisions/1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
decision 4: authentication method `private_key_jwt`, pushed authorization requests and DPoP-bound access tokens
required, the grant types `authorization_code` and `refresh_token`, the redirect URI
`https://<host>/bff/callback`, the post-logout redirect URI `https://<host>/`, the back-channel logout URI
`https://<host>/bff/backchannel-logout`, and `jwks_uri` `https://<host>/bff/jwks`.

List the console host in `allowedHosts`, because `/api` on it reaches the server. The console's authority defaults to
`tenantResolution.staticSingle.issuer` under the `StaticSingle` strategy; set `managementConsole.authority` for any
other strategy. `managementConsole.managementApiUrl` defaults to `https://<managementConsole.host>/api`.

For more than one replica, set `managementConsole.valkey.existingSecret` to a Secret that holds the console's own
Valkey connection string under the `connection-string` key. Never share the server's Valkey with the console.

Pass console settings such as `ManagementConsole__Branding__DisplayName` through `managementConsole.extraEnv`.

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com,manage.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.existingSecret=identity-database \
  --set secretProtection.rootKeyProvider=local --set secretProtection.local.keySecret=identity-root-key --set secretProtection.recovery.publicKeyConfigMap=identity-recovery-key \
  --set managementConsole.enabled=true --set managementConsole.host=manage.example.com \
  --set managementConsole.keys.clientKeySecret=identity-mgmt-client-key \
  --set managementConsole.keys.dpopKeySecret=identity-mgmt-dpop-key
```

`ci/management-console-values.yaml` shows a release with the console on its own host.

## Strict-Transport-Security

Outside Development, the host sends `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every response
to a request that reached it over HTTPS
([ADR-1258](../../docs/decisions/1258-the-server-sends-strict-transport-security-outside-development.md)). Behind the
ingress the pod serves plain HTTP, so the host sees HTTPS only through the `X-Forwarded-Proto` header of a proxy that
`forwardedHeaders` trusts: set `forwardedHeaders.knownNetworks` or `forwardedHeaders.knownProxies` to cover the ingress
controller, or the header is not sent.

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
([ADR-1248](../../docs/decisions/1248-the-host-image-writes-one-line-json-logs.md)). Set
`Logging__Console__FormatterName=simple` in `extraEnv` to write text instead.
