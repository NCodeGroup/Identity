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
  certificate encrypts the Data Protection key ring at rest; cert-manager can issue it.
- `valkey.existingSecret`, for more than one replica: the Valkey connection string, under the `connection-string` key.

Apply the database migrations before each new version starts; the host does not migrate on boot.

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

## Updates

With Valkey, a new replica becomes ready before an old one stops. Without it, each replica keeps single-use state in its
own process, so the chart never runs two at once: a single replica is replaced with a short outage on each update.

## Other settings

The chart models the settings it validates. Pass any other host setting as an environment variable through `extraEnv`
or `extraEnvFrom`, for example `Logging__LogLevel__Default`. To retire a key-protection certificate, add its Secret to
`keyProtection.previousCertificateSecrets` and name the new one in `keyProtection.certificateSecret`. Keys the old
certificate protected keep decrypting.
