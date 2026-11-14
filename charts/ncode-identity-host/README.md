# ncode-identity-host

The Helm chart for `NCode.Identity.OpenId.Host`, the OpenID Connect and OAuth 2.0 server. It runs the host image as a
non-root deployment that meets the restricted Pod Security Standard, serves HTTPS only, probes `/health/live` and
`/health/ready` over HTTPS, and drains each pod before stopping it.
Setting `managementConsole.enabled` also deploys the management console host on a host name of its own
([ADR-1458](../../docs/decisions/1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)).
It requires Kubernetes 1.30 or later.

## Before the first install

The chart takes no secret values. It references Secrets you create in the release's namespace:

- `database.existingSecret` or `database.parts.existingSecret`: the Postgres database, as a Secret holding the finished
  Npgsql connection string under the `connection-string` key, or as the Secret your Postgres operator writes, with no
  Secret of your own to build; see [Database and Valkey](#database-and-valkey).
- `secretProtection.local.keySecret`: a Secret holding the local root key under the `key` key (base64url, 32 bytes).
  The root key encrypts every stored secret and the Data Protection key ring, so back it up and never change it in
  place; see [Secret protection](#secret-protection).
- `secretProtection.recovery.publicKeyConfigMap`: a ConfigMap holding the recovery public key under the
  `recovery-public.pem` key, so losing the root key does not lose every stored secret; see
  [Secret protection](#secret-protection).
- `valkey.existingSecret` or `valkey.parts.existingSecret`, for more than one replica: the Valkey server, as a Secret
  holding the finished connection string under the `connection-string` key, or as the Secret your Valkey operator
  writes, with no Secret of your own to build; see [Database and Valkey](#database-and-valkey).
- `migrations.database`, optional: a connection with the right to change the schema, used only by the migration Job,
  in either form (`existingSecret` or `parts.existingSecret`). It defaults to `database`.
- cert-manager, unless `backendTls.existingSecret` names a Secret: the chart creates the `Certificate` (and, without
  `backendTls.certManager.issuerRef.name`, a private CA) that issues the pods' serving certificate; see
  [Transport security](#transport-security). The chart also creates the ingress `Certificate` `<release>-ingress`
  with cert-manager whenever a route uses the ingress controller's Secret and no `ingressSecret` of yours names it.
- A router that reaches the pods over HTTPS: a Gateway API implementation with `BackendTLSPolicy` (Gateway API v1.4 or
  later), Traefik, or another Ingress controller you configure; see [Routing to the pods](#routing-to-the-pods).
- `setup.token.existingSecret`, optional: a Secret holding the setup token under the `token` key, and
  `setup.job.existingSecret`, for the opt-in setup Job: a Secret holding the root administrator's credentials; see
  [First use](#first-use).

### Creating the Secrets

Create each Secret in the release's namespace, and name it in the value that references it. The key names below are the
chart's defaults; each is a value you can change.

The database connection string, as `database.existingSecret` reads it. Use the Npgsql connection string of your
database; the placeholders are yours:

```bash
kubectl create secret generic identity-database \
  --from-literal=connection-string='Host=<host>;Database=<database>;Username=<user>;Password=<password>'
```

The Valkey connection string, as `valkey.existingSecret` reads it, for more than one replica. Use your server's
StackExchange.Redis connection string:

```bash
kubectl create secret generic identity-valkey \
  --from-literal=connection-string='<host>:6379,password=<password>,ssl=true'
```

The setup token, as `setup.token.existingSecret` reads it, at least 32 characters:

```bash
kubectl create secret generic identity-setup-token \
  --from-literal=token="$(openssl rand -base64 32)"
```

The root administrator's credential for the setup Job, as `setup.job.existingSecret` reads it. The password has at
least 15 characters:

```bash
kubectl create secret generic identity-setup-root \
  --from-literal=userName=<user name> \
  --from-literal=email=<email address> \
  --from-literal=password=<password>
```

The local root key, the recovery key and the management console's keys have their own instructions:
[Creating the local root key Secret](#creating-the-local-root-key-secret),
[Creating the recovery key](#creating-the-recovery-key) and [Management console](#management-console). An External
Secrets `ExternalSecret` that writes the same Secret with the same keys works the same way.

## Published artifacts

Every merge to `main` and every release tag `v*.*.*` publishes four artifacts to GHCR at one version
(`0.0.1-alpha.<n>` for a merge to `main`, the tag's version for a release tag), through the Publish artifacts workflow ([ADR-1505](../../docs/decisions/1505-every-merge-to-main-publishes-the-host-image-the-console-image-the-chart-and-the-openapi-document-at-one-prerelease-version.md)):

- `ghcr.io/ncodegroup/ncode-identity-host:<version>`, the host image.
- `ghcr.io/ncodegroup/ncode-identity-management-console:<version>`, the management console image.
- `oci://ghcr.io/ncodegroup/charts/ncode-identity-host`, this chart, at version `<version>`.
- `oci://ghcr.io/ncodegroup/openapi/ncode-identity-host` at version `<version>`, the host's OpenAPI document (`v1.json`), which `oras pull` fetches.

The chart pins its image tags to its own `appVersion`, so following the chart follows the images. A stable version also tags both images `latest`. A Flux `HelmRelease`
or `helm install` takes a semver range that includes prereleases, such as `>=0.0.1-alpha`, to follow them.

Verify the chart signature, then the host image provenance:

```bash
cosign verify ghcr.io/ncodegroup/charts/ncode-identity-host:<version> --certificate-identity-regexp '^https://github.com/NCodeGroup/identity/\.github/workflows/publish-artifacts\.yml@refs/(heads/main|tags/v.+)$' --certificate-oidc-issuer https://token.actions.githubusercontent.com
```

```bash
gh attestation verify oci://ghcr.io/ncodegroup/ncode-identity-host:<version> --repo NCodeGroup/identity
```

### Package access

Each package's access is set once, in GitHub: no API sets a package's Actions access, its repository link or its
visibility. A package the workflow creates is linked to this repository and the workflow can write to it, but it starts
private. A package first pushed by hand is neither linked nor writable by the workflow, and the workflow's push to it
fails. For each of `ncode-identity-host`, `ncode-identity-management-console`, `charts/ncode-identity-host` and
`openapi/ncode-identity-host`, open the package from the organization's **Packages** tab and:

1. In **Package settings**, under **Manage Actions access**, click **Add repository**, choose `NCodeGroup/identity`
   and give it the **Write** role.
2. When the package page shows no linked repository, click **Connect repository** and choose `NCodeGroup/identity`.
3. In **Package settings**, under **Danger Zone**, use **Change visibility** to make the package **Public**, so a
   cluster pulls it without credentials.

The `verify` job pulls every artifact anonymously, so a package that is still private fails the run with a hint that
points here.

## Install

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.parts.existingSecret=pg-app \
  --set secretProtection.rootKeyProvider=local --set secretProtection.local.keySecret=identity-root-key --set secretProtection.recovery.publicKeyConfigMap=identity-recovery-key
```

`pg-app` is a CloudNativePG cluster's Secret; a deployer without an operator names a connection-string Secret with
`database.existingSecret` instead (see [Database and Valkey](#database-and-valkey)).

`ci/high-availability-values.yaml` shows a rolling three-replica release with Valkey, an ingress, and a retired local
root key.

## What the chart refuses

Rendering fails, before anything is applied, when:

- `allowedHosts` is empty or contains `*`;
- the probes would send a wildcard `Host` header: when the first allowed host is a wildcard such as `*.example.com`,
  set `probes.host` to a concrete host it accepts;
- `backendTls.certManager.renewBefore` is not shorter than `duration`, or the key size does not fit its algorithm
  (ECDSA 256, 384 or 521; RSA 2048 or more), or `ingress.controller` is set and is not `traefik` or `other`;
- an Ingress is enabled and `ingress.controller` is empty;
- `httpRoute.enabled` is `true` and `httpRoute.parentRefs` or `httpRoute.hostnames` is empty, or there is no CA
  ConfigMap: `backendTls.caConfigMap` for the server, or `managementConsole.backendTls.caConfigMap` for the console
  (which falls back to the server's);
- `backendTls.caPublisher.enabled` is `true` and the server's certificate does not come from the chart's private CA,
  because `backendTls.certManager.issuerRef.name` or `backendTls.existingSecret` is set;
- `backendTls.caPublisher.enabled` is `true`, the management console is enabled with a certificate of your own in
  `managementConsole.backendTls.existingSecret`, and `managementConsole.backendTls.caConfigMap` is empty, because the
  console's CA ConfigMap would fall back to the one the publisher writes, which holds only the chart's private CA;
- neither `database.existingSecret` nor `database.parts.existingSecret` is set, a connection (`database` or
  `migrations.database`) sets both, a connection's `existingSecret` is set and the key of its connection string,
  `connectionStringKey`, is empty (a `migrations.database` key falls back to the one under `database`), or a parts
  Secret leaves `hostKey`, `databaseKey`, `usernameKey` or `passwordKey` empty;
- a Valkey connection (`valkey` or `managementConsole.valkey`) sets both `existingSecret` and `parts.existingSecret`,
  sets `existingSecret` and leaves `connectionStringKey` empty, or has a parts Secret that leaves `hostKey` empty. The
  other `parts` keys may be empty, because an empty key leaves that part out;
- `setup.token.existingSecret` is set and `setup.token.key` is empty, or `setup.job.enabled` is `true` and
  `setup.job.existingSecret`, `setup.job.userNameKey` or `setup.job.passwordKey` is empty;
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
- a `secretProtection.remoteKeyProviders` location, `awsKms.region`, `azureKeyVault.vaultUri` or
  `googleCloudKms.keyRing`, is set and the provider's `enabled` is `false`, because a location registers nothing on its
  own;
- an enabled `secretProtection.remoteKeyProviders` provider has its location and
  `secretProtection.remoteKeys.deployment` is empty, because the provider creates keys and the sweep finds them by the
  deployment;
- a key management service is used, as `secretProtection.rootKeyProvider`, in `secretProtection.ownerRootKeys.providers`
  or enabled under `secretProtection.remoteKeyProviders`, and its workload credentials are half set: only one of
  `secretProtection.azureKeyVault.workloadIdentity.tenantId` and `clientId` is set, or
  `secretProtection.googleCloudKms.workloadIdentityFederation.provider` is set and is not the resource name of a
  workload identity pool provider. Workload credentials themselves are optional: a provider without them signs in
  with the pod's ambient identity, such as EKS Pod Identity or Workload Identity Federation for GKE. The console's
  identities under `managementConsole.secretProtection` are checked the same way for each vendor a console remote key
  names;
- `environment` is `Production`, `secretProtection.recovery.publicKeyConfigMap` is empty, and
  `secretProtection.recovery.disabled` is not `true`, because losing the root key would then lose every stored secret;
- `replicaCount` is above 1 without `valkey.existingSecret` or `valkey.parts.existingSecret`, because replicas share
  single-use state through Valkey;
- `managementConsole.enabled` is `true` and `managementConsole.host` is empty or contains `*`;
- `managementConsole.enabled` is `true` and the console's authority is empty or does not start with `https://`;
- `managementConsole.enabled` is `true` and `setup.managementConsole.clientId` is empty, because the setup wizard hints
  the console's `client_id` and the host refuses an empty one;
- `managementConsole.enabled` is `true` and a console key ring, `managementConsole.keyRings.client` or `.dpop`, names
  neither `pemSecret` nor a remote key;
- a console key ring holds two keys with the same id, compared without case, where a `pemSecret` key's id is
  `mgmt-client` or `mgmt-dpop`;
- a console remote key's `keyId` is not the form its provider reads: an AWS KMS key or alias ARN, a Key Vault or
  Managed HSM key version identifier, or a Cloud KMS crypto key version name;
- `managementConsole.replicaCount` is above 1 without `managementConsole.valkey.existingSecret` or
  `managementConsole.valkey.parts.existingSecret`, because replicas share their sessions through Valkey;
- `apiReference.pageEnabled` is `true` and `apiReference.documentEnabled` is not, because the page renders the OpenAPI
  document the host serves;
- a value is unknown or has the wrong type.

## Migrations

The host does not migrate on boot. Before each install and upgrade, a Helm hook Job runs the migration worker from the
host's image and applies the pending EF Core migrations. Helm waits for it: a failed migration fails the release before
any pod is replaced, and the Job stays until the next release so its logs can be read. Helm's `--timeout` must cover
the migrations.

The previous version keeps serving while the Job runs and until the rollout ends, so every migration must keep it
working: add first, and drop or rename only in a later release
([ADR-1180](../../docs/decisions/1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md)). A `helm rollback` runs no migration.

To apply migrations another way, such as with a generated idempotent script, set `migrations.enabled=false`.

## Updates

With Valkey, a new replica becomes ready before an old one stops. Without it, each replica keeps single-use state in its
own process, so the chart never runs two at once: it uses the `Recreate` strategy, which starts the new replica only
after the old one has terminated, so each update has a short outage.

## First use

An uninitialized instance is set up once, and the setup creates its root global administrator. A setup token guards the
wizard. Create a Secret with a value of at least 32 characters under the key `setup.token.key` names (`token`) and
name it in `setup.token.existingSecret`; the chart passes it to the host as `Setup__Token`. With
`setup.token.existingSecret` empty, the host generates a single-use token at each process start and logs it once at
Warning level, on the one replica that issued it. The generated token expires (after 1 hour by default), and the host stores only its hash.

The wizard answers at `https://<host>/setup/#token=<token>`. It creates the root global administrator once, and every
`/setup` route answers 404 after the instance is initialized.

With `managementConsole.enabled`, the host receives `Setup__Wizard__ManagementConsole__BaseUrl` (`https://<managementConsole.host>`)
and `Setup__Wizard__ManagementConsole__ClientId` (`setup.managementConsole.clientId`), so the wizard's console step opens
with registration checked, the client ID and the console URL filled, and the key set read from
`https://<managementConsole.host>/bff/jwks`. The operator submits without typing. When the browser cannot reach the
console, the step says so and takes a pasted set.

To create the root administrator without a browser, enable `setup.job`. It renders a Job per release revision that runs
the host image's `setup` command, creates the root administrator from the Secret named in `setup.job.existingSecret`, and
exits 0 on an instance that is already initialized. The setup values:

| Value                                    | Meaning                                                                                     |
|------------------------------------------|---------------------------------------------------------------------------------------------|
| `setup.token.existingSecret`             | A Secret holding the setup token, the host's `Setup__Token`; empty makes the host generate one. |
| `setup.token.key`                        | The key of the token in that Secret (`token`).                                              |
| `setup.job.enabled`                      | Renders the Job (`false`).                                                                  |
| `setup.job.existingSecret`               | A Secret holding the root administrator's credentials. Required when the Job is enabled.    |
| `setup.job.userNameKey`                  | The key of the user name in that Secret (`userName`).                                       |
| `setup.job.emailKey`                     | The key of the email address in that Secret (`email`); the Secret may omit it.              |
| `setup.job.passwordKey`                  | The key of the password in that Secret (`password`); at least 15 characters.                |
| `setup.job.backoffLimit`                 | How often the Job retries (`2`).                                                            |
| `setup.job.activeDeadlineSeconds`        | How long the Job may run, in seconds (`300`).                                               |
| `setup.managementConsole.clientId`       | The `client_id` of the management console's client (`mgmt-console`).                        |
| `setup.managementConsole.baseUrl`        | The console's public https origin. Setting it registers the console's client; empty registers none. |
| `setup.managementConsole.jwks`           | The console's public JWK Set, required with `baseUrl`.                                      |
| `setup.automation.clientId`              | The `client_id` of the automation client. Setting it registers the client; empty registers none. |
| `setup.automation.jwks`                  | The automation client's public JWK Set, required with `automation.clientId`.                |
| `platform.grantTypesEnabled`             | Grant types the platform adds to its baseline grants, such as `client_credentials` (`[]`).      |

The Job registers the clients below in the same save as the root administrator, passing them as `Setup__ManagementConsole__*`
and `Setup__Automation__*`; the chart emits each group only when `setup.managementConsole.baseUrl` or
`setup.automation.clientId` is set. If a registration is refused, setup saves nothing, so the root administrator is not
created either. Setup imports each JWK Set by value, so neither client needs a `jwks_uri` the server can reach.

- **The management console's client** is a confidential client of the control-plane tenant, with the metadata that
  [Management console](#management-console) lists, except `jwks_uri`. Set `setup.managementConsole.baseUrl` to the origin
  of `managementConsole.host` and `setup.managementConsole.jwks` to the public JWK Set of the console's `mgmt-client.pem`
  ([how to derive it](../../docs/first-production-instance.md#the-management-consoles-client)).
- **The automation client** is a platform-owned `client_credentials` machine client that holds `GlobalAdmin` and
  authenticates with `private_key_jwt`, so a GitOps install manages the instance without the development bootstrap
  client. Setting `setup.automation.clientId` and `setup.automation.jwks` registers it. The platform's
  grant list must include `client_credentials`, or setup fails as a whole; set
  `platform.grantTypesEnabled` to `[client_credentials]`, which the chart passes to the host as the platform setting
  `grant_types_enabled` (the [first-use guide](../../docs/first-production-instance.md#6-enable-grants) describes it).

Delete both Secrets once the instance is initialized. The guide
[The first use of a Production instance](../../docs/first-production-instance.md) walks through the whole first use.

## Secret protection

The host encrypts every stored secret, and the Data Protection key ring, under a root key held outside the database, so
a database dump or backup is useless without it
([ADR-1235](../../docs/decisions/1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
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
([ADR-1407](../../docs/decisions/1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md)).
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
`secretProtection.ownerRootKeys.providers` or is enabled under `secretProtection.remoteKeyProviders`: for AWS KMS the
web identity (`secretProtection.awsKms.webIdentity.roleArn`), for Azure Key Vault the workload identity
(`secretProtection.azureKeyVault.workloadIdentity.clientId` and `tenantId`), and for Google Cloud KMS the federation
(`secretProtection.googleCloudKms.workloadIdentityFederation.provider`), each set up as in the sections above. The
provider's own key values (`awsKms.keyArn`, `azureKeyVault.keyId`, `googleCloudKms.keyName`) are rendered only when it
is the root key provider, because a tenant names its key itself. A listed provider with no workload-identity values uses
the pod's ambient identity, as the root key provider does. A half-set value, such as an Azure `clientId` without a
`tenantId`, or a Google Cloud `provider` that is not a pool provider resource name, is refused. A pod never carries the
credentials of a provider that is neither.

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
([ADR-1471](../../docs/decisions/1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)).
The operator registers the providers a tenant may choose from, and the host reaches each with the deployment's own
workload identity. The values are:

- `secretProtection.remoteKeyProviders.awsKms.enabled`, `azureKeyVault.enabled` and `googleCloudKms.enabled` (default
  `false`), which register the provider;
- `secretProtection.remoteKeyProviders.awsKms.region`, the region in which keys are created, and
  `pendingWindowInDays`, 7 to 30 (default 30), the days a deleted key waits before AWS destroys it;
- `secretProtection.remoteKeyProviders.azureKeyVault.vaultUri`, the vault in which keys are created, and
  `hardwareProtected` (default `true`);
- `secretProtection.remoteKeyProviders.googleCloudKms.keyRing`, the key ring in which keys are created, and
  `hardwareProtected` (default `true`);
- `secretProtection.remoteKeys.deployment`, which names this release's keys in a shared KMS scope (an AWS account, a
  vault or a key ring): lowercase letters, digits and hyphens, at most 40 characters. It is required when an enabled
  provider has its location. A deployment is one database of stored secrets: every replica and release that shares the
  database uses its name, and no other deployment in the same AWS account, vault or key ring uses it, because each
  deployment destroys every key of its name that its own database does not name;
- `secretProtection.remoteKeys.sweepEnabled` (default `true`), `sweepInterval` (default `1.00:00:00`) and
  `sweepGracePeriod` (default `1.00:00:00`, between one hour and 365 days), which control the background sweep that
  destroys the keys no stored secret names
  ([ADR-1478](../../docs/decisions/1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md)).

The chart renders an enabled provider as `SecretProtection__RemoteKeyProviders__AwsKms__*`, `__AzureKeyVault__*` or
`__GoogleCloudKms__*`, which registers it as the host registers a provider whose configuration section exists
([ADR-1485](../../docs/decisions/1485-the-host-chart-enables-server-kms-vendors-and-holds-console-key-rings-under-the-consoles-identity.md));
it renders `pendingWindowInDays` and `hardwareProtected` whenever the provider is enabled and its location only when
set, and `hardwareProtected` applies only to the keys the provider creates; a provider that is not enabled renders
nothing and no tenant can choose it; the deployment renders as `SecretProtection__RemoteKeys__Deployment` with
`__SweepEnabled`, `__SweepInterval` and `__SweepGracePeriod`.

An enabled provider with its location creates keys: `tenant_key_provider` can name it, the host does not start unless it
holds every signing and encryption key the database provisions through it, and the sweep destroys only keys tagged with
the deployment, older than the grace period, that no stored secret names. A release that enables such a provider
without `secretProtection.remoteKeys.deployment` is refused:
`secretProtection.remoteKeys.deployment must name this deployment when an enabled remote key provider creates keys,
because its region, vaultUri or keyRing is set: lowercase letters, digits and hyphens.` A location on a provider that is
not enabled is refused too:
`secretProtection.remoteKeyProviders.<vendor>.<location> is set and <vendor>.enabled is false: set enabled to register
the provider, or clear <location>.` `ci/remote-key-providers-values.yaml` shows a release that creates keys in all
three.

**Binding keys without creating any.** An enabled provider without its location binds keys a KMS already holds and
creates none
([ADR-1474](../../docs/decisions/1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md)):
the management API binds a key by its identifier and the server reads, signs with and decrypts with it;
`tenant_key_provider` cannot name the provider, the management API refuses to create a key in it, the sweep skips it,
and the release needs no `secretProtection.remoteKeys.deployment`; the server never destroys a key it binds.
`ci/bind-only-remote-key-providers-values.yaml` shows a release that binds keys in all three.

```yaml
secretProtection:
  remoteKeyProviders:
    awsKms:
      enabled: true
```

**Credentials.** An enabled provider reuses the credentials set up for it in the sections above, so a pod carries them:
for AWS KMS the web identity (`secretProtection.awsKms.webIdentity.roleArn`), for Azure Key Vault the workload identity
(`secretProtection.azureKeyVault.workloadIdentity.clientId` and `tenantId`), and for Google Cloud KMS the federation
(`secretProtection.googleCloudKms.workloadIdentityFederation.provider`). A provider with no workload-identity values
uses the pod's ambient identity. The chart renders such a provider on purpose, as it does a root key provider, because a
cluster can supply the identity itself; it refuses only a half-set value. A pod never carries the credentials of a
provider that is not the root key provider, listed in `secretProtection.ownerRootKeys.providers` or enabled here. One
identity serves each vendor, so the root key, a tenant's root key and the tenant's keys share it.

**What the identity needs.** The deployment's identity creates, uses, lists and destroys the keys a provider with its
location creates; the server creates sign-only or decrypt-only keys:

- AWS KMS: `kms:CreateKey`, `kms:TagResource`, `kms:GetPublicKey`, `kms:Sign`, `kms:Decrypt`,
  `kms:DeriveSharedSecret`, `kms:ScheduleKeyDeletion`, `kms:DescribeKey`, `kms:ListKeys` and `kms:ListResourceTags`.
- Azure Key Vault: the `Key Vault Crypto Officer` role on the vault.
- Google Cloud KMS: `cloudkms.cryptoKeys.create` and `cloudkms.cryptoKeys.list`, and
  `cloudkms.cryptoKeyVersions.create`, `.list`, `.get`, `.viewPublicKey`, `.useToSign`, `.useToDecrypt` and `.destroy`
  on the key ring.

A key an operator binds needs the read of its public half and its own operation, because the server reads the half and
proves the key before it stores it: AWS `kms:GetPublicKey` plus `kms:Sign`, `kms:Decrypt` or `kms:DeriveSharedSecret`
as the key's algorithm says, Azure `Key Vault Crypto User` on the key, Google `cloudkms.cryptoKeyVersions.viewPublicKey`
plus `.useToSign` or `.useToDecrypt`. A provider without its location needs only these.

Scope the identity to the deployment's keys: on AWS, condition `kms:ScheduleKeyDeletion` on
`aws:ResourceTag/ncode-identity:deployment` equal to the deployment name, and `kms:CreateKey` and `kms:TagResource` on
`aws:RequestTag/ncode-identity:deployment`, so it creates and destroys only keys of its deployment (`kms:ListKeys`
names no resource, and a bound key carries no such tag); on Azure, a role assignment cannot be conditioned on a key's
tags, so give each deployment a vault of its own and a custom role without delete and purge; on Google, a grant on a key
ring covers every crypto key in it, so give each deployment a key ring of its own.

**Choosing a provider.** A tenant, a team or the platform selects a provider with the setting
`tenant_key_provider`, whose value is empty (local keys) or an enabled provider with its location: `aws-kms`,
`azure-key-vault` or `google-cloud-kms`, and which chooses the provider of every key the tenant provisions, signing and
encryption alike. The management API creates a tenant's key in a provider with its location and binds a key that any
enabled provider already holds. See [Remote keys](../../docs/capabilities/remote-keys.md).

### A key the master key cannot decrypt

The host refuses to start while the Data Protection key ring holds a live key, one that is not revoked and has not
expired, that the platform's master key cannot decrypt, and it names each such key in the error
([ADR-1247](../../docs/decisions/1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md)). It happens when the
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
every path to the console. The console takes `managementConsole.httpRoute` the same way, with `managementConsole.host`
as its hostname. The console runs its own image, which shares the chart's `appVersion`
([ADR-1458](../../docs/decisions/1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md)).

Create the two private keys the console's BFF signs with, and a Secret for each, in the release's namespace:

```bash
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out mgmt-client.pem
openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out mgmt-dpop.pem
kubectl create secret generic identity-mgmt-client-key --from-file=mgmt-client.pem
kubectl create secret generic identity-mgmt-dpop-key --from-file=mgmt-dpop.pem
```

Name the Secrets in `managementConsole.keyRings.client.pemSecret` and `managementConsole.keyRings.dpop.pemSecret`. The
chart mounts each read-only and points `Bff__KeyRings__mgmt-client__PemFiles__0` and
`Bff__KeyRings__mgmt-dpop__PemFiles__0` at the files. To hold a ring's keys in a key management service instead or as
well, see [The console's keys in a key management service](#the-consoles-keys-in-a-key-management-service).

Register `mgmt-console` with the server as a confidential client of the control-plane tenant, with the metadata of
[ADR-1437](../../docs/decisions/1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)
decision 6: authentication method `private_key_jwt`, pushed authorization requests and DPoP-bound access tokens
required, the grant types `authorization_code` and `refresh_token`, the redirect URI
`https://<host>/bff/callback`, the post-logout redirect URI `https://<host>/`, the back-channel logout URI
`https://<host>/bff/backchannel-logout`, and `jwks_uri` `https://<host>/bff/jwks`.

The server never fetches a `jwks_uri` whose host resolves to a loopback, private or other special-use address, and the
management API refuses to store one. A console reachable only on a private network registers without `jwks_uri` and
imports the public half of its `mgmt-client` key through
`POST /api/tenants/{tenantId}/clients/mgmt-console/secrets/import`. The client then authenticates against the stored
key, so a rotation of `mgmt-client` imports the next key. A refused fetch logs `ClientJwksFetchFailed` with the reason
`blocked_address`.

The setup Job registers this client in the same save as the root administrator, with the metadata above except
`jwks_uri`, from `setup.managementConsole.baseUrl` and `setup.managementConsole.jwks`; see
[First use](#first-use). Without it, register the client through the management API.

The console calls the server's management API at `/api` on the origin of the console's authority, the URL the server
compares the console's DPoP proofs with, and the chart sets `ManagementConsole__ManagementApiUrl` to it:
`https://id.example.com/api` for the authority `https://id.example.com`. The console's host serves only the console, so
`allowedHosts` need not list it, and machine callers reach the management API at the server's own host
([ADR-1437](../../docs/decisions/1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).
The console's authority defaults to `tenantResolution.staticSingle.issuer` under the `StaticSingle` strategy; set
`managementConsole.authority` for any other strategy.

For more than one replica, set `managementConsole.valkey.existingSecret` to a Secret that holds the console's own
Valkey connection string under the `connection-string` key, or `managementConsole.valkey.parts.existingSecret` to the
Secret your Valkey operator writes for it; see [Valkey](#valkey). Never share the server's Valkey with the console.

Pass console settings such as `ManagementConsole__Branding__DisplayName` through `managementConsole.extraEnv`.

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.parts.existingSecret=pg-app \
  --set secretProtection.rootKeyProvider=local --set secretProtection.local.keySecret=identity-root-key --set secretProtection.recovery.publicKeyConfigMap=identity-recovery-key \
  --set managementConsole.enabled=true --set managementConsole.host=manage.example.com \
  --set managementConsole.keyRings.client.pemSecret=identity-mgmt-client-key \
  --set managementConsole.keyRings.dpop.pemSecret=identity-mgmt-dpop-key
```

`ci/management-console-values.yaml` shows a release with the console on its own host.

### The console's keys in a key management service

Each console key ring, `managementConsole.keyRings.client` (`Bff:KeyRings:mgmt-client`) and
`managementConsole.keyRings.dpop` (`Bff:KeyRings:mgmt-dpop`), holds a PEM key from a Secret, keys that a key management
service holds, or both. A key a key management service holds signs every request the console makes, so the private key
never reaches the pod
([ADR-1479](../../docs/decisions/1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md),
[ADR-1485](../../docs/decisions/1485-the-host-chart-enables-server-kms-vendors-and-holds-console-key-rings-under-the-consoles-identity.md)).
A ring has these values:

- `pemSecret`, the Secret holding `mgmt-client.pem` or `mgmt-dpop.pem`;
- `remoteKeys`, a map from a key id (`kid`) to the key. The id is letters and digits separated by single `.`, `_` or
  `-`, at most 64 characters, and a map cannot name one id twice. Each key has `provider` (`aws-kms`,
  `azure-key-vault` or `google-cloud-kms`), `keyId`, `algorithm`, `secretType`, `keySizeBits` and, for an elliptic
  curve key, `curve`.

The `keyId` is the form the provider reads: an AWS KMS key or alias ARN, a Key Vault or Managed HSM key version
identifier, or a Cloud KMS crypto key version name. A release whose client key is in AWS KMS and whose DPoP key is in a
Secret:

```yaml
managementConsole:
  keyRings:
    client:
      remoteKeys:
        mgmt-client:
          provider: aws-kms
          keyId: arn:aws:kms:us-west-2:111122223333:key/1234abcd-12ab-34cd-56ef-1234567890ab
          algorithm: ES256
          secretType: ecc
          keySizeBits: 256
          curve: P-256
    dpop:
      pemSecret: identity-mgmt-dpop-key
  secretProtection:
    awsKms:
      webIdentity:
        roleArn: arn:aws:iam::111122223333:role/ncode-identity-management-console
```

The chart renders each remote key as `Bff__KeyRings__<ring>__RemoteKeys__<kid>__Provider`, `__KeyId`, `__Algorithm`,
`__SecretType`, `__KeySizeBits` and `__Curve`, where `<ring>` is `mgmt-client` or `mgmt-dpop`. The console image
registers the AWS KMS, Azure Key Vault and Google Cloud KMS providers without a key-creation location, and the console
reads every key where the key's own identifier names, so the chart renders no
`SecretProtection__RemoteKeyProviders__…` setting for it
([ADR-1474](../../docs/decisions/1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md)).

**Credentials.** The console has an identity of its own under `managementConsole.secretProtection`:
`awsKms.webIdentity`, `azureKeyVault.workloadIdentity` and `googleCloudKms.workloadIdentityFederation`, with the same
fields and defaults as the server's `secretProtection.*` of the same names. The chart mounts them at the same paths and
sets the same environment variables as for the server, and renders the ConfigMap `<console full name>-google-cloud`
for a Google federation, with a checksum annotation on the pod so a change restarts it. The federated credentials trust
the console's service account, `system:serviceaccount:<namespace>:<service account>`, where the service account is
`managementConsole.serviceAccount.name`, or the console's full name, `<release full name>-management-console`, when
that is empty. A value left empty means the pod's ambient identity. A console pod carries a vendor's credentials only
when a console remote key names that vendor: the server never carries the console's identity, nor the console the
server's.

**What the identity needs.** The console only reads and uses a key:

- AWS KMS: `kms:GetPublicKey` plus `kms:Sign`, or `kms:Decrypt` and `kms:DeriveSharedSecret` for a key that decrypts.
- Azure Key Vault: the `Key Vault Crypto User` role on the key.
- Google Cloud KMS: `cloudkms.cryptoKeyVersions.viewPublicKey` plus `cloudkms.cryptoKeyVersions.useToSign`, or
  `cloudkms.cryptoKeyVersions.useToDecrypt` for a key that decrypts.

The console reads and proves each key at start. A key it cannot read or use stops it, and a rotation of the key takes
effect when the console restarts. The key is the operator's: the console never creates, tags or destroys it. Keep it
out of the `ncid-` groups and out of the tag of `secretProtection.remoteKeys.deployment`, in a scope of its own, so the
server's sweep never names it. Keep the DPoP key in a Secret unless the threat model needs it in a key management
service and the request rate fits the service's quota
([ADR-1479](../../docs/decisions/1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)).

`ci/management-console-remote-keys-values.yaml` shows a release.

## Transport security

The pods serve HTTPS only, on port 8443. The Service listens on `service.port` (443 by default) with
`appProtocol: https`, and the Ingress backend names the Service's `https` port. The host refuses to start in
Production unless Kestrel serves only HTTPS endpoints whose certificate loads, is valid now, has an RSA key of at least
2048 bits or an ECDSA key, is signed with SHA-256 or stronger, and is not the ASP.NET Core development certificate. A
TLS-terminating proxy that speaks plain HTTP to the pods is not supported
([ADR-1504](../../docs/decisions/1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)).

The probes use HTTPS, and the kubelet does not verify the certificate a probe meets. The host checks its certificate as
it starts, and Kestrel loads renewed certificate files it watches.

### The serving certificate

By default the chart creates a cert-manager `Certificate` for each component from a private CA it also creates: a
self-signed Issuer, the CA `Certificate` `<release>-ca`, and an Issuer backed by that CA. The key is ECDSA P-256, the
`duration` is 2160h and `renewBefore` is 720h. The DNS names are `<service>`, `<service>.<namespace>`,
`<service>.<namespace>.svc` and `<service>.<namespace>.svc.cluster.local`, plus `backendTls.certManager.dnsNames`.

- `backendTls.certManager.issuerRef` names your own Issuer or ClusterIssuer, so the chart creates no private CA.
- `backendTls.existingSecret` and `managementConsole.backendTls.existingSecret` name `kubernetes.io/tls` Secrets you
  create, so the chart creates no `Certificate` for the component whose Secret you name. The private CA is still
  created while another component needs a `Certificate` from it. Each Secret must also hold `ca.crt`, the CA that
  signed the certificate: the pods trust it for the ingress controller's certificate.

### Routing to the pods

The router must speak HTTPS to the Service and verify the pods' certificate. The chart renders what each of three
modes needs:

| Mode            | Values                                      | What the chart renders                                                                       |
|-----------------|---------------------------------------------|----------------------------------------------------------------------------------------------|
| Gateway API     | `httpRoute.enabled`                         | An `HTTPRoute` and a `BackendTLSPolicy` that targets the Service's `https` port, verifies the pods against the CA ConfigMap and checks the host name `<service>.<namespace>.svc`. |
| Traefik Ingress | `ingress.controller: traefik`               | A `ServersTransport` with `serverName`, `rootCAs` naming the ingress controller's Secret and, when the pods require the controller's certificate, `certificatesSecrets`, and the Service annotations `service.serversscheme` and `service.serverstransport`. |
| Other Ingress   | `ingress.controller: other`                 | Neither. You configure the controller to speak HTTPS to the Service and to verify the pods' certificate. |

The Ingress and the HTTPRoute may both be on. `ingress.annotations`, `managementConsole.ingress.annotations`,
`httpRoute.annotations` and `managementConsole.httpRoute.annotations` add to the annotations the chart renders. The
console's `managementConsole.httpRoute` takes `enabled`, `parentRefs` and `annotations`, and its hostname is
`managementConsole.host`. The Gateway API mode needs the Gateway API v1.4 or later CRDs and a CA ConfigMap;
`ci/gateway-api-values.yaml` shows a release:

```yaml
backendTls:
  caConfigMap: identity-ca
httpRoute:
  enabled: true
  parentRefs:
    - name: shared-gateway
      namespace: gateway-system
      sectionName: https
  hostnames:
    - id.example.com
```

The pods speak HTTP/2 and HTTP/1.1 over TLS and refuse an HTTP/2 request whose `:scheme` is `http`, so the Gateway
forwards `https`. A Gateway whose listener is HTTPS does. A Gateway whose listener is plain HTTP, behind a load
balancer that ends TLS, must be told to, as Envoy Gateway's `ClientTrafficPolicy` does with `scheme: MatchBackend`.

A Traefik release needs Traefik's Kubernetes CRD provider, which reads the `ServersTransport`;
`ci/high-availability-values.yaml` shows one:

```yaml
ingress:
  enabled: true
  controller: traefik
  className: traefik
  hosts:
    - host: id.example.com
      paths:
        - path: /
          pathType: Prefix
```

With any other controller the chart renders the Ingress alone. Configure the controller to speak HTTPS to the
Service's `https` port and to verify the pods against the CA, through `ingress.annotations` or its own resources:

```yaml
ingress:
  enabled: true
  controller: other
  className: my-controller
  hosts:
    - host: id.example.com
      paths:
        - path: /
          pathType: Prefix
```

### The ingress controller's Secret

The Traefik mode reads one Secret, `<release>-ingress`, which cert-manager issues from the issuer of the pods'
certificates as a client certificate for the common name `backendTls.clientCertificate.commonName`. Its `ca.crt` is
what Traefik verifies the pods against (the `rootCAs` entry of the `ServersTransport`). Its `tls.crt` and `tls.key` are
the controller's own client certificate, which authenticates it to the pods; they are never a pod's key, and the
serving Secret is never the controller's Secret, so the controller, which reads Secrets cluster-wide, never receives a
pod's private key. The chart issues the Secret when a route uses it: an Ingress with `ingress.controller: traefik`, or
any route while `backendTls.clientCertificate.required` is `true`, and no `ingressSecret` of yours names it.
`backendTls.ingressSecret` names a Secret of your own in the release namespace that holds `ca.crt` and, when
`backendTls.clientCertificate.required` is `true`, `tls.crt` and `tls.key`; the chart then names it in place of
`<release>-ingress`. `managementConsole.backendTls.ingressSecret` does the same for the console and, when empty, falls
back to `backendTls.ingressSecret` and then to `<release>-ingress`. Rendering fails when a route uses the controller's
Secret, its component sets `existingSecret`, and neither `backendTls.certManager.issuerRef.name` nor an `ingressSecret`
for it is set, because the chart cannot know the CA that signed your certificate.

### The CA ConfigMap

`backendTls.caConfigMap` names a ConfigMap in the release namespace that holds the CA under the `ca.crt` key and
nothing else, which the Gateway API mode's `BackendTLSPolicy` verifies the pods against. `BackendTLSPolicy` reads only
ConfigMaps, and cert-manager issues only Secrets, so the CA reaches the Gateway as a ConfigMap and the Gateway never
reads a Secret that holds a key. The Gateway API mode requires it; the Traefik mode does not use it.

Create it in one of two ways:

- A trust-manager `Bundle` whose target is a ConfigMap with the key `ca.crt`. This is recommended, with a cluster CA
  named in `backendTls.certManager.issuerRef`.
- `kubectl -n <namespace> create configmap <name> --from-file=ca.crt=<ca.pem>`.

With the chart's private CA, `backendTls.caPublisher.enabled` publishes the ConfigMap itself. A post-install and
post-upgrade hook Job, which runs the official `registry.k8s.io/kubectl` image pinned by digest, runs `kubectl apply -k`
to write the `ca.crt` of the `<release>-ca` Secret to a ConfigMap named `<release>-ca`, or the name
`backendTls.caConfigMap` sets, and the routes use it. The Job sees only the `ca.crt` key, never the CA's private key,
and its Role reads and patches only that ConfigMap and may create ConfigMaps, which Kubernetes cannot limit by name.
The kubelet holds its pod until cert-manager has created the Secret, and `helm uninstall` leaves the ConfigMap it
wrote. The chart refuses the publisher with an issuer of your own in `backendTls.certManager.issuerRef.name` or a
server certificate of your own in `backendTls.existingSecret`, because it publishes only the chart's private CA.
`managementConsole.backendTls.caConfigMap` names the console's ConfigMap and, when empty, falls back to the server's.
With the publisher, a console certificate of your own in `managementConsole.backendTls.existingSecret` needs its CA in
`managementConsole.backendTls.caConfigMap`, and the chart refuses the release without it.

### Requiring the ingress controller's certificate

By default the pods accept a connection from any client. `backendTls.clientCertificate.required: true` makes the server
and the console require the ingress controller's certificate: a pod answers `403` to every request whose connection
carries no certificate that chains to the CA in the serving Secret's `ca.crt`, has the `client auth` usage, is valid and
has the subject `CN=<backendTls.clientCertificate.commonName>` (`ncode-identity-ingress` by default), except
`/health/live` and `/health/ready`, which the kubelet sends without one. The chart sets
`ProxyClientCertificate__Required`, `__CertificateAuthorityPath` (`/etc/ncode-identity/tls/ca.crt`) and
`__AllowedSubjects__0`, so the pods need no further mount. For Traefik, the chart adds `certificatesSecrets` to each
`ServersTransport`, so Traefik presents the Secret's certificate.

In the Gateway API mode the Gateway, not a route, presents the backend client certificate, and the Gateway is yours:
name the ingress controller's Secret as its `spec.tls.backend.clientCertificateRef`
([GEP-3155](https://gateway-api.sigs.k8s.io/geps/gep-3155/)), with a `ReferenceGrant` in the release namespace that lets
the Gateway's namespace read it. A Gateway presents one certificate to every backend behind it, so set
`backendTls.clientCertificate.commonName` to the subject of the certificate your Gateway presents, issued by the CA that
signed the pods' certificates.

With `ingress.controller: other`, or with an `ingressSecret` of your own, you configure the controller's certificate
yourself: it must have the subject `CN=<commonName>` and be issued by the CA in the serving Secret's `ca.crt`.

A pod whose connection carries the controller's certificate never reads it as an end client's. A deployment with
protocol mutual TLS (RFC 8705) therefore sets `OpenId__MutualTls__CertificateHeaderName` through `extraEnv`, and the
controller forwards the end client's certificate in that header
([ADR-1506](../../docs/decisions/1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)).

### Trust

1. A private CA run by cert-manager or a mesh, verified by the proxy, is preferred and is the default.
2. A pinned self-signed certificate, whose `ca.crt` is the certificate itself, is acceptable.
3. A self-signed certificate with proxy verification off is unauthenticated encryption, which stops passive
   eavesdropping and nothing else, and is discouraged. The chart never renders it.

### What CI proves

The `cluster` job of the `Host chart` workflow installs the chart's server and management console in a kind cluster,
first behind Traefik and then behind an Envoy Gateway. Envoy Gateway is the Gateway API implementation because it
implements the Gateway's backend client certificate. The Gateway's listener is plain HTTP, and a `ClientTrafficPolicy`
with `scheme: MatchBackend` sends `https` as the `:scheme` the pods require.

Behind Traefik, the job proves that the pods become Ready over HTTPS, that a request through the controller succeeds
for both hosts, that Traefik verifies the pods' certificate through the `ServersTransport` and presents the ingress
client certificate, and that a controller whose verification Secret holds a foreign CA is refused (`500` from
Traefik). With `backendTls.clientCertificate.required`, a direct request without the proxy client certificate gets
`403` outside the health paths and `200` on `/health/live`, while a request with the ingress Secret's certificate gets
`200`.

Behind the Envoy Gateway, with `httpRoute` and `backendTls.caPublisher`, the job proves that the `BackendTLSPolicy`
verifies the pods' certificate and that the Gateway's `spec.tls.backend.clientCertificateRef` presents the ingress
client certificate the pods require. A Gateway without `clientCertificateRef` gets `403` outside the health paths
and `200` on them, and a `BackendTLSPolicy` that holds a foreign CA gets `503`.

## Strict-Transport-Security

Outside Development, the host sends `Strict-Transport-Security: max-age=31536000; includeSubDomains` on every response
to a request that reached it over HTTPS
([ADR-1447](../../docs/decisions/1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package-outside-development.md)). The pods
serve HTTPS, so the host sees HTTPS on every request and sends the header whether or not `forwardedHeaders` trusts the
ingress. Trusting the ingress (`forwardedHeaders.knownNetworks` or `forwardedHeaders.knownProxies`) is how the host
learns the client's address and the original host, so set it to cover the ingress controller.

When the ingress controller sends the header itself, set `StrictTransportSecurity__Enabled=false` in `extraEnv`, so
one component owns it. Change the policy the same way with `StrictTransportSecurity__MaxAge` (for example
`730.00:00:00`), `StrictTransportSecurity__IncludeSubDomains` and `StrictTransportSecurity__Preload`. Set `Preload`
only after submitting the domain to the browsers' preload list: it requires `IncludeSubDomains` and a `MaxAge` of at
least 365 days, and the host refuses to start without them.

## GitOps

A GitOps tool applies a release from Git, so what `helm install` is given by hand becomes manifests: the namespace, the
Secrets the chart references, the chart's source and the release's values. This section deploys the chart with
[Flux](https://fluxcd.io/) 2.3 or later, whose `HelmRelease` is the `helm.toolkit.fluxcd.io/v2` API. `examples/flux/`
holds the manifests, and `ci/gitops-values.yaml` holds the release's values as a values file, which the `Host chart`
workflow lints and renders ([What CI proves for the example](#what-ci-proves-for-the-example)).

The release needs cert-manager, which issues the pods' certificates, and an ingress controller; the example uses
Traefik with its Kubernetes CRD provider (see [Routing to the pods](#routing-to-the-pods)). Make the Flux
`Kustomization` or `HelmRelease` of each add-on a `dependsOn` of the release, so their CRDs exist before the chart's
`Certificate` and `ServersTransport` are applied.

The manifests live in the cluster's Git repository in two folders:

```text
identity/
  prerequisites/   the namespace, the Secrets and the ConfigMap the release references
  release/         the HelmRepository and the HelmRelease
```

Two Flux `Kustomization`s apply the folders in order. The release's `dependsOn` names the prerequisites, so Flux creates
every Secret before a pod asks for one. They live with Flux's own manifests for the cluster, and their `sourceRef`
names its `GitRepository`, `flux-system` here (`examples/flux/kustomizations.yaml`):

```yaml
# The two Flux Kustomizations that deploy the host chart: the prerequisites first, then the release.
apiVersion: kustomize.toolkit.fluxcd.io/v1
kind: Kustomization
metadata:
  name: identity-prerequisites
  namespace: flux-system
spec:
  interval: 10m
  path: ./identity/prerequisites
  prune: true
  wait: true
  sourceRef:
    kind: GitRepository
    name: flux-system
  decryption:
    provider: sops
    secretRef:
      name: sops-age
---
apiVersion: kustomize.toolkit.fluxcd.io/v1
kind: Kustomization
metadata:
  name: identity
  namespace: flux-system
spec:
  interval: 10m
  path: ./identity/release
  prune: true
  sourceRef:
    kind: GitRepository
    name: flux-system
  dependsOn:
    - name: identity-prerequisites
```

### The prerequisites

The chart takes no secret values, so every Secret and ConfigMap it names exists before the release; a pod that names a
missing Secret stays in `CreateContainerConfigError`. The example's release names these, in the `identity` namespace:

| Kind        | Name                       | Keys                            | Holds                                                                                              |
|-------------|----------------------------|---------------------------------|----------------------------------------------------------------------------------------------------|
| `Secret`    | `identity-database`        | `connection-string`             | The Npgsql connection string of the database, or use `database.parts.existingSecret`.              |
| `Secret`    | `identity-root-key`        | `key`                           | The local root key; see [Creating the local root key Secret](#creating-the-local-root-key-secret). |
| `ConfigMap` | `identity-recovery-key`    | `recovery-public.pem`           | The recovery public key; see [Creating the recovery key](#creating-the-recovery-key).              |
| `Secret`    | `identity-setup-token`     | `token`                         | The setup token, at least 32 characters; see [First use](#first-use).                              |
| `Secret`    | `identity-setup-root`      | `userName`, `email`, `password` | The root administrator's credential for the setup Job; the password has at least 15 characters.    |
| `Secret`    | `identity-mgmt-client-key` | `mgmt-client.pem`               | The console's client key; see [Management console](#management-console).                           |
| `Secret`    | `identity-mgmt-dpop-key`   | `mgmt-dpop.pem`                 | The console's DPoP key; see [Management console](#management-console).                             |

Generate each manifest with `kubectl create ... --dry-run=client -o yaml`, encrypt a Secret's `data` with
[SOPS](https://fluxcd.io/flux/guides/mozilla-sops/), and commit the result under `prerequisites/`:

```bash
kubectl -n identity create secret generic identity-setup-token \
  --from-literal=token="$(openssl rand -base64 32)" --dry-run=client -o yaml > prerequisites/identity-setup-token.yaml
sops --encrypt --encrypted-regex '^(data|stringData)$' --in-place prerequisites/identity-setup-token.yaml
```

The `identity-prerequisites` `Kustomization` decrypts them with the age key in the `sops-age` Secret of `flux-system`
(`decryption` in `examples/flux/kustomizations.yaml`). An External Secrets `ExternalSecret` or a Sealed Secret that
writes the same Secret with the same keys works the same way. The recovery public key is not secret: commit its
ConfigMap as it is, from `kubectl -n identity create configmap identity-recovery-key --from-file=recovery-public.pem
--dry-run=client -o yaml`. Never commit a Secret unencrypted, because Git keeps it in its history, and keep the root key
and the recovery private key backed up offline as [Secret protection](#secret-protection) says: the encrypted manifest
is a backup only when its age key is one too. Once the instance is initialized, set `setup.job.enabled` to `false` and
delete the setup Secrets.

### Pod Security

The release's namespace should enforce the `restricted` Pod Security level, which the chart's pods and Jobs are built
for: a namespace that enforces it rejects any pod that asks for more. The example's namespace also audits and warns on
`restricted`, and its `kustomize.toolkit.fluxcd.io/prune: disabled` annotation stops Flux from pruning the namespace,
which would delete the release, its Secrets and its pods (`examples/flux/prerequisites/namespace.yaml`):

```yaml
# The release's namespace enforces the restricted Pod Security Standard, which the chart's pods meet.
apiVersion: v1
kind: Namespace
metadata:
  name: identity
  labels:
    pod-security.kubernetes.io/enforce: restricted
    pod-security.kubernetes.io/audit: restricted
    pod-security.kubernetes.io/warn: restricted
  annotations:
    # Pruning the namespace would delete the release, its Secrets and its pods.
    kustomize.toolkit.fluxcd.io/prune: disabled
```

### The chart source and the release

The chart is an OCI artifact in GHCR ([Published artifacts](#published-artifacts)), so its source is a `HelmRepository`
of `type: oci`. The package is public, so the repository needs no `secretRef`
(`examples/flux/release/helmrepository.yaml`):

```yaml
# The chart is published as an OCI artifact; a HelmRepository of type oci names the registry path that holds it. The
# package is public, so the repository needs no secretRef.
apiVersion: source.toolkit.fluxcd.io/v1
kind: HelmRepository
metadata:
  name: ncode-identity
  namespace: identity
spec:
  type: oci
  interval: 1h
  url: oci://ghcr.io/ncodegroup/charts
```

The `HelmRelease` names the chart, a version range and the release's values
(`examples/flux/release/helmrelease.yaml`):

```yaml
apiVersion: helm.toolkit.fluxcd.io/v2
kind: HelmRelease
metadata:
  name: identity
  namespace: identity
spec:
  interval: 10m
  # Helm waits for the pre-install and pre-upgrade migration Job, so the timeout outlasts the Job's
  # migrations.activeDeadlineSeconds (600 seconds).
  timeout: 15m
  chart:
    spec:
      chart: ncode-identity-host
      # The chart's appVersion pins both images, so a range that includes prereleases rolls the images out too.
      version: ">=0.0.1-alpha"
      sourceRef:
        kind: HelmRepository
        name: ncode-identity
  install:
    remediation:
      retries: 3
  upgrade:
    cleanupOnFail: true
    remediation:
      retries: 3
      remediateLastFailure: true
  values:
    # The release's values: one replica behind Traefik, the management console on its own host, and the root
    # administrator created by the setup Job. Every Secret and the ConfigMap named here exists before the release.
    allowedHosts:
      - id.example.com
    tenantResolution:
      staticSingle:
        issuer: https://id.example.com
    forwardedHeaders:
      knownNetworks:
        - 10.42.0.0/16
    database:
      existingSecret: identity-database
    secretProtection:
      rootKeyProvider: local
      local:
        keySecret: identity-root-key
      recovery:
        publicKeyConfigMap: identity-recovery-key
    setup:
      token:
        existingSecret: identity-setup-token
      job:
        enabled: true
        existingSecret: identity-setup-root
    ingress:
      enabled: true
      controller: traefik
      className: traefik
      annotations:
        cert-manager.io/cluster-issuer: letsencrypt
        external-dns.alpha.kubernetes.io/ttl: "300"
      hosts:
        - host: id.example.com
          paths:
            - path: /
              pathType: Prefix
      tls:
        - secretName: id-example-com-tls
          hosts:
            - id.example.com
    managementConsole:
      enabled: true
      host: manage.example.com
      keyRings:
        client:
          pemSecret: identity-mgmt-client-key
        dpop:
          pemSecret: identity-mgmt-dpop-key
      ingress:
        enabled: true
        className: traefik
        annotations:
          cert-manager.io/cluster-issuer: letsencrypt
          external-dns.alpha.kubernetes.io/ttl: "300"
        tls:
          - secretName: manage-example-com-tls
            hosts:
              - manage.example.com
```

- `version: ">=0.0.1-alpha"` selects the newest chart version in the range, prereleases included, which covers every
  `0.0.1-alpha.<n>` that a merge to `main` publishes. The chart's `appVersion` pins both images, so a newer chart
  version rolls the host and the console out too. Name one exact version to stop following.
- `timeout` is Helm's wait for each operation, the migration Job included, so it outlasts the Job's
  `migrations.activeDeadlineSeconds` (600 seconds). Raise both for a long migration.
- `install.remediation` retries a failed install three times, uninstalling between attempts. `upgrade.remediation`
  retries a failed upgrade three times and rolls the last failure back to the previous revision, and
  `upgrade.cleanupOnFail` removes the resources the failed upgrade created.
- `values` is the release's values, the same text as `ci/gitops-values.yaml`. The example runs one replica without
  Valkey, so each update has a short outage ([Updates](#updates)). For a rolling update, set `replicaCount` above 1 and
  name `valkey.existingSecret` or `valkey.parts.existingSecret`.

### Migrations and failed releases

The migration Job is a Helm `pre-install,pre-upgrade` hook ([Migrations](#migrations)), so Flux waits for it as Helm
does, up to `timeout`. A failed migration fails the Helm action before any pod is replaced, and the `HelmRelease`
reports `Ready=False` with the reason `InstallFailed` or `UpgradeFailed`; the previous revision's pods keep serving.
Flux remediates a failed upgrade by rolling back to the previous revision, which runs no migration, and a failed install
by uninstalling and retrying, up to three times each. After the last retry the `HelmRelease` stays `Ready=False` and
Helm has rolled the release back or uninstalled it, but the failed migration Job and its logs remain, because the chart
deletes the Job only when it succeeds:

```bash
kubectl -n identity logs job/identity-ncode-identity-host-migrations
```

Fix the cause, then run `flux reconcile helmrelease identity -n identity --reset`, which resets the failure count and
tries the release again.

### The ingress

`ingress.className` names the cluster's `IngressClass` (`traefik`), and `ingress.controller` tells the chart which
router it renders for ([Routing to the pods](#routing-to-the-pods)). `ingress.annotations` and
`managementConsole.ingress.annotations` add to the annotations the chart renders. In the example,
`cert-manager.io/cluster-issuer` names a `ClusterIssuer` of yours, so cert-manager issues the Secret each `tls` entry
names, and `external-dns.alpha.kubernetes.io/ttl` is read by ExternalDNS, which creates the DNS records for the
Ingress's hosts. Put the annotations of the controllers your cluster runs in the same place.
`forwardedHeaders.knownNetworks` is k3s's default pod CIDR, `10.42.0.0/16`: set it to the pod CIDR of your cluster, so
the host trusts the `X-Forwarded-*` headers of the ingress controller.

### What CI proves for the example

The `Host chart` workflow lints `ci/gitops-values.yaml` strictly and renders it with every other values file, and it
checks that:

- `ci/gitops-values.yaml` is the `values` of `examples/flux/release/helmrelease.yaml`;
- this README shows each file under `examples/flux/` as it is;
- the `HelmRelease` names the chart and the `HelmRepository`, its version range includes prereleases, and its `timeout`
  outlasts the migration Job's `migrations.activeDeadlineSeconds`;
- every manifest under `examples/flux/` is valid against the CRD schemas that Flux v2.10.0 publishes, with unknown
  fields refused.

CI does not run Flux: it proves the values render, the manifests agree with the chart and this README, and every
manifest is valid against Flux's schemas.

## API reference

The host serves neither the OpenAPI document (`/openapi/v1.json`) nor the Scalar API reference page (`/scalar/v1`) by
default. `apiReference.documentEnabled` and `apiReference.pageEnabled` turn them on, and the page requires the document.
Both are anonymous once on: the document lists every management route, schema and scope, while the management API
still requires a management access token
([ADR-1481](../../docs/decisions/1481-the-composition-layer-serves-the-api-reference-only-where-a-host-enables-it-and-each-management-problem-says-when-it-occurs.md)).
To serve them on an internal host only, route `/openapi/` and `/scalar/` to the pods only from that host at the
ingress. The published `openapi/ncode-identity-host` artifact holds the same document, so a client generator reads it
without a release enabling it.

## Database and Valkey

The Postgres database takes one of two forms, and a connection never takes both:

- `database.existingSecret` names a Secret that holds the finished Npgsql connection string under
  `database.connectionStringKey` (`connection-string` by default). The pods receive it as `ConnectionStrings__openiddb`.
- `database.parts.existingSecret` names a Secret that holds the connection's parts, as a Postgres operator writes it.
  The pods receive each part from a `secretKeyRef` as an `OpenIdDatabase__*` environment variable, and the host
  assembles the connection from them.

The key that holds each part is a value, and the defaults are the keys of a CloudNativePG `<cluster>-app` Secret:

| Value                          | Default    | Environment variable        | Part                        |
|--------------------------------|------------|-----------------------------|-----------------------------|
| `database.parts.hostKey`       | `host`     | `OpenIdDatabase__Host`      | the server's host name      |
| `database.parts.portKey`       | `port`     | `OpenIdDatabase__Port`      | the server's port, optional |
| `database.parts.databaseKey`   | `dbname`   | `OpenIdDatabase__Name`      | the database's name         |
| `database.parts.usernameKey`   | `user`     | `OpenIdDatabase__Username`  | the role's name             |
| `database.parts.passwordKey`   | `password` | `OpenIdDatabase__Password`  | the role's password         |

An empty `portKey` leaves the port out, and Npgsql uses 5432. The host, database, user and password keys must name a
key.

For a CloudNativePG cluster named `pg`, its `pg-app` Secret works as it is:

```shell
--set database.parts.existingSecret=pg-app
```

To give the migration Job the Secret of a role with the right to change the schema, name it under
`migrations.database.parts`. Its keys default to the ones under `database.parts`, so a Secret of the same shape needs
only its name:

```yaml
migrations:
  database:
    parts:
      existingSecret: pg-schema
```

The host assembles the connection with the password quoted, so any password works, including one that holds `;`, `'`
or `"`. The Secrets are read when a pod starts, so a rotated password reaches the pods at their next restart. A setting
the connection needs beyond the parts, such as a CA bundle, goes through `extraEnv` and `extraVolumes` as under
[Other settings](#other-settings). The decision is recorded in
[ADR-1509](../../docs/decisions/1509-the-host-chart-passes-a-postgres-operators-secret-as-the-connections-parts-and-the-host-assembles-the-npgsql-connection.md).

### Valkey

The Valkey connection takes the same two forms, and a connection never takes both:

- `valkey.existingSecret` names a Secret that holds the finished StackExchange.Redis connection string under
  `valkey.connectionStringKey` (`connection-string` by default). The pods receive it as `ConnectionStrings__valkey`.
- `valkey.parts.existingSecret` names a Secret that holds the server's address and password, as a Valkey operator writes
  it. The pods receive each part from a `secretKeyRef` as a `ValkeyConnection__*` environment variable, and the adapter
  assembles the connection from them.

The key that holds each part is a value, and the defaults name the keys `host`, `port` and `password`:

| Value                      | Default    | Environment variable         | Part                        |
|----------------------------|------------|------------------------------|-----------------------------|
| `valkey.parts.hostKey`     | `host`     | `ValkeyConnection__Host`     | the server's host name      |
| `valkey.parts.portKey`     | `port`     | `ValkeyConnection__Port`     | the server's port, optional |
| `valkey.parts.usernameKey` | empty      | `ValkeyConnection__Username` | the user, optional          |
| `valkey.parts.passwordKey` | `password` | `ValkeyConnection__Password` | the password, optional      |
| `valkey.parts.ssl`         | `false`    | `ValkeyConnection__Ssl`      | TLS when `true`, no key     |

The host key must name a key. An empty `portKey` leaves the port at 6379, and an empty `usernameKey` or `passwordKey`
leaves that part out.

`managementConsole.valkey.parts` takes the same values for the console's own Valkey. Its keys default to empty and fall
back to the ones under `valkey.parts`, so a Secret of the same shape needs only its name; its `ssl` is its own value and
does not fall back. The console's Valkey is never the server's.

For a Valkey operator whose Secret is named `valkey-app`:

```shell
--set valkey.parts.existingSecret=valkey-app
```

The adapter builds the connection from the parts, so a password that holds `,` or `=` stays a password. The Secrets are
read when a pod starts, so a rotated password reaches the pods at their next restart. A setting the connection needs
beyond the parts goes through `extraEnv`. The decision is recorded in
[ADR-1510](../../docs/decisions/1510-the-host-chart-passes-a-valkey-servers-address-and-password-as-parts-and-the-adapter-assembles-the-connection.md).

## Other settings

The chart models the settings it validates. Pass any other host setting as an environment variable through `extraEnv`
or `extraEnvFrom`, for example `Logging__LogLevel__Default`. The migration Job receives `extraEnv`, `extraEnvFrom`,
`extraVolumes`, and `extraVolumeMounts` as well, so a setting the database connection needs, such as a CA bundle for
TLS to Postgres, reaches both the host and the migrations.

Raising a log level, for example `Logging__LogLevel__Default=Debug`, never writes a request or response body, a query
string, or the value of an `Authorization`, `Cookie`, `DPoP`, `Set-Cookie` or `Location` header: the host's HTTP
logging writes the request and response properties, the allow-listed headers and the duration, and no setting enables
more
([ADR-1503](../../docs/decisions/1503-http-logging-writes-a-body-only-for-an-endpoint-that-opts-in-and-never-for-one-that-carries-credentials.md)).

The image writes one JSON object per line to standard output, with a UTC timestamp, so a log collector keeps an entry
and its exception in one record
([ADR-1248](../../docs/decisions/1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)). Set
`Logging__Console__FormatterName=simple` in `extraEnv` to write text instead.
