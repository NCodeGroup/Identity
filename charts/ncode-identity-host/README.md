# ncode-identity-host

The Helm chart for `NCode.Identity.OpenId.Host`, the OpenID Connect and OAuth 2.0 server. It runs the host image as a
non-root deployment that meets the restricted Pod Security Standard, probes `/health/live` and `/health/ready`, and
drains each pod before stopping it
([ADR-0179](../../docs/adr/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).
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
([ADR-0180](../../docs/adr/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)). A `helm rollback` runs no migration.

To apply migrations another way, such as with a generated idempotent script, set `migrations.enabled=false`.

## Updates

With Valkey, a new replica becomes ready before an old one stops. Without it, each replica keeps single-use state in its
own process, so the chart never runs two at once: it uses the `Recreate` strategy, which starts the new replica only
after the old one has terminated, so each update has a short outage.

## Secret protection

The host encrypts every stored secret, and the Data Protection key ring, under a root key held outside the database, so
a database dump or backup is useless without it
([ADR-0235](../../docs/adr/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
`secretProtection.rootKeyProvider` chooses where the root key lives, and Production refuses an empty or `ephemeral`
choice:

- `local`: an AES key in a Secret the chart mounts into the pod. It suits a homelab: the key is outside the database,
  but anyone who can read the Secret can read the key.
- `aws-kms`: a key in AWS KMS, which never leaves it. The host reaches it with web identity federation, so no AWS
  credential is stored in the cluster; see [AWS KMS](#aws-kms).

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

## Other settings

The chart models the settings it validates. Pass any other host setting as an environment variable through `extraEnv`
or `extraEnvFrom`, for example `Logging__LogLevel__Default`. The migration Job receives `extraEnv`, `extraEnvFrom`,
`extraVolumes`, and `extraVolumeMounts` as well, so a setting the database connection needs, such as a CA bundle for
TLS to Postgres, reaches both the host and the migrations.
