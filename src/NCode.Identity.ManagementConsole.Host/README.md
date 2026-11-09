# NCode.Identity.ManagementConsole.Host

The management console's BFF host. It serves the `apps/mgmt` single-page bundle, runs the `mgmt` frontend of the
backend-for-frontend, and forwards the console's calls to the OP's management API
([ADR-1437](../../docs/decisions/1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)).

## Container image

The `Dockerfile` builds the production image from the repository root. Its base images are the host image's, pinned by
tag and digest:

```bash
docker build --file src/NCode.Identity.ManagementConsole.Host/Dockerfile --tag ncode-identity-management-console .
```

The image runs on a chiseled ASP.NET Core base with no shell. It runs as the non-root `app` user (UID 1654), listens on
port **8080** over plain HTTP, and serves the `apps/mgmt` bundle from `/app/mgmt`. TLS terminates at the ingress. Probe
`/health/live` for liveness and `/health/ready` for readiness. The image writes one JSON object per line to standard
output, with a UTC timestamp. The `Console image` workflow builds it for `linux/amd64` and `linux/arm64`
([ADR-1458](../../docs/decisions/1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)).

The image starts in the `Production` environment, which refuses to start until the deployment provides:

- `AllowedHosts`: the console's public host name. A missing value or `*` is refused.
- `Bff__Frontends__mgmt__Authority`: the OP's issuer URL.
- `Bff__Frontends__mgmt__PublicOrigin`: the console's public origin, such as `https://manage.example.com`.
- `Bff__KeyRings__mgmt-client__…`: the key ring of the console's client authentication key, a PEM file
  (`PemFiles__0`), keys that a key management service holds (`RemoteKeys__<kid>__…`), or both.
- `Bff__KeyRings__mgmt-dpop__…`: the key ring of the console's DPoP key, a PEM file (`PemFiles__0`), keys that a key
  management service holds (`RemoteKeys__<kid>__…`), or both.
- `ManagementConsole__ManagementApiUrl`: the base URL of the OP's management API.
- `ConnectionStrings__valkey`: the Valkey connection string that holds the sessions, required for more than one
  replica.

A key ring can come from a key management service instead of a PEM file, or beside one. Each key is an entry
`Bff__KeyRings__<ring>__RemoteKeys__<kid>__Provider` with `__KeyId`, `__Algorithm`, `__SecretType`, `__KeySizeBits` and
`__Curve`, and the matching `SecretProtection__RemoteKeyProviders__<Vendor>__…` section configures the vendor: AWS
`Region`, Azure `VaultUri` or Google `KeyRing`. The console reads each key where the key's identifier names, so the
section only registers the provider. The provider's row for the algorithm decides whether the key signs or
decrypts. The console reads each key's public half and proves the key when it starts, so a key that its identity cannot
read or use stops the host. The deployment's workload identity needs, for each key, `kms:GetPublicKey` plus `kms:Sign`
or `kms:Decrypt` on AWS, `Key Vault Crypto User` on the key on Azure, or `cloudkms.cryptoKeyVersions.viewPublicKey`
plus `.useToSign` or `.useToDecrypt` on Google Cloud. The key belongs to the operator, and the console never creates,
tags or destroys it.

The Helm chart in [`charts/ncode-identity-host`](../../charts/ncode-identity-host/README.md#management-console) deploys
the image as its `managementConsole` component with these settings. It holds each key ring's keys in a Secret
(`managementConsole.keyRings.<ring>.pemSecret`), in a key management service (`remoteKeys`) or both, reached through
the console's own workload identity (`managementConsole.secretProtection`); see
[The console's keys in a key management service](../../charts/ncode-identity-host/README.md#the-consoles-keys-in-a-key-management-service).
