# ncode-identity-host

The Helm chart for `NCode.Identity.OpenId.Host`, the OpenID Connect and OAuth 2.0 server. It runs the host image as a
non-root deployment that meets the restricted Pod Security Standard, probes `/health/live` and `/health/ready`, and
drains each pod before stopping it
([ADR-0179](../../docs/adr/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).
It requires Kubernetes 1.30 or later.

## Before the first install

The chart takes no secret values. It references Secrets you create in the release's namespace:

- `database.existingSecret`: the Postgres connection string, under the `connection-string` key.
- `keyProtection.certificateSecret`: a `kubernetes.io/tls` Secret with an RSA certificate and its unencrypted key. The
  certificate encrypts the Data Protection key ring at rest. It is long-lived and must not renew in place; see
  [Rotating the key-protection certificate](#rotating-the-key-protection-certificate).
- `valkey.existingSecret`, for more than one replica: the Valkey connection string, under the `connection-string` key.
- `migrations.database.existingSecret`, optional: a connection string with the right to change the schema, used only
  by the migration Job. It defaults to `database.existingSecret`.

## Install

```bash
helm install identity charts/ncode-identity-host \
  --set 'allowedHosts={id.example.com}' \
  --set tenantResolution.staticSingle.issuer=https://id.example.com \
  --set database.existingSecret=identity-database \
  --set keyProtection.certificateSecret=identity-key-protection
```

`ci/high-availability-values.yaml` shows a rolling three-replica release with Valkey, an ingress, and a retired
key-protection certificate.

## What the chart refuses

Rendering fails, before anything is applied, when:

- `allowedHosts` is empty or contains `*`;
- the probes would send a wildcard `Host` header: when the first allowed host is a wildcard such as `*.example.com`,
  set `probes.host` to a concrete host it accepts;
- `database.existingSecret` is empty;
- the `StaticSingle` strategy has no `https` issuer;
- `environment` is `Production` and `keyProtection.certificateSecret` is empty;
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

## Rotating the key-protection certificate

Each Data Protection key is wrapped by the current certificate when the key is created, and only that certificate (by
thumbprint) can unwrap it again. Treat the certificate as long-lived, for example several years, and rotate it by hand:

1. Create the new certificate in a **new** Secret.
2. Add the old Secret to `keyProtection.previousCertificateSecrets` and name the new one in
   `keyProtection.certificateSecret`, then upgrade the release. New keys use the new certificate; keys the old one
   wrapped keep decrypting.
3. Keep the old Secret listed until no stored data depends on a key it wrapped: until the secrets those keys protect are
   re-protected or retired, not merely until the keys expire
   ([ADR-0177](../../docs/adr/0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)).

**Never renew the certificate in place.** A renewal written into the same Secret, which is what cert-manager does by
default, replaces the only certificate that can unwrap the existing keys. After the next restart the key ring cannot be
read, so sign-in sessions, antiforgery tokens, and protected stored secrets stop working. If cert-manager issues the
certificate, give it a `duration` longer than your rotation period and issue each replacement into a new Secret
through a new `Certificate` resource.

The host's readiness probe fails while the pod cannot unwrap a live key that was in the ring when it started. A pod
started with a renewed or wrong certificate, or without a previous certificate whose keys are still live, never
receives traffic, and with Valkey the rolling update stops before it replaces the pods that are serving. Without
Valkey the `Recreate` strategy has already stopped the old pod, so the release is unavailable until the certificate is
fixed.

## Other settings

The chart models the settings it validates. Pass any other host setting as an environment variable through `extraEnv`
or `extraEnvFrom`, for example `Logging__LogLevel__Default`. The migration Job receives `extraEnv`, `extraEnvFrom`,
`extraVolumes`, and `extraVolumeMounts` as well, so a setting the database connection needs, such as a CA bundle for
TLS to Postgres, reaches both the host and the migrations.
