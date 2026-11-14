# NCode.Identity.ManagementConsole.Host

The management console's BFF host. It serves the `apps/mgmt` single-page bundle, runs the `mgmt` frontend of the
backend-for-frontend, and forwards the console's calls to the OP's management API
([ADR-1437](../../docs/decisions/1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)).

## Container image

The `Dockerfile` builds the production image from the repository root. Its base images are the host image's, pinned by
tag and digest:

```bash
docker build --file hosts/NCode.Identity.ManagementConsole.Host/Dockerfile --tag ncode-identity-management-console .
```

The image runs on a chiseled ASP.NET Core base with no shell. It runs as the non-root `app` user (UID 1654), listens on
port **8443** over HTTPS only, and serves the `apps/mgmt` bundle from `/app/mgmt`. It serves the PEM certificate and
private key it finds at `/etc/ncode-identity/tls/tls.crt` and `/etc/ncode-identity/tls/tls.key`
(`Kestrel__Certificates__Default__Path` and `__KeyPath`), so mount them there, as a Kubernetes `kubernetes.io/tls`
Secret does; `Kestrel__Endpoints__<name>__…` settings replace the default endpoint and certificate. TLS ends at the pod:
a proxy in front of it connects over HTTPS. Probe `/health/live` for liveness and `/health/ready` for readiness. The
image writes one JSON object per line to standard output, with a UTC timestamp. The `Console image` workflow builds it
for `linux/amd64` and `linux/arm64`
([ADR-1248](../../docs/decisions/1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md)).

The `ProxyClientCertificate` section makes the host require the reverse proxy's client certificate. With
`ProxyClientCertificate__Required=true`, the host answers `403` to every request whose connection carries no certificate
that chains to the authorities in `ProxyClientCertificate__CertificateAuthorityPath` (a PEM file), has the `client auth`
usage, is valid and has a subject listed in `ProxyClientCertificate__AllowedSubjects__0`, `__1` and so on, except
`/health/live` and `/health/ready`, which the kubelet sends without a certificate. It is off by default, and the keys
are refused while `Required` is false
([ADR-1506](../../docs/decisions/1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)).

The image never sets `LocalDevelopment:Enabled`, so the host runs the deployment posture, which refuses to start until
the deployment provides:

- A serving certificate: the host serves HTTPS only. It refuses to start when an endpoint is plain HTTP, no endpoint is
  bound, or the certificate it serves has no usable private key, is expired or not yet valid, has an RSA key under 2048
  bits or a signature weaker than SHA-256, or is the ASP.NET Core development certificate
  ([ADR-1504](../../docs/decisions/1504-the-hosts-serve-https-only-and-verify-their-certificate-at-startup.md)).
  A self-signed certificate is accepted; the proxy decides whether to trust it.
- `AllowedHosts`: the console's public host name. A missing value or `*` is refused.
- `Bff__Frontends__mgmt__Authority`: the OP's issuer URL.
- `Bff__Frontends__mgmt__PublicOrigin`: the console's public origin, such as `https://manage.example.com`.
- `Bff__KeyRings__mgmt-client__…`: the key ring of the console's client authentication key, a PEM file
  (`PemFiles__0`), keys that a key management service holds (`RemoteKeys__<kid>__…`), or both.
- `Bff__KeyRings__mgmt-dpop__…`: the key ring of the console's DPoP key, a PEM file (`PemFiles__0`), keys that a key
  management service holds (`RemoteKeys__<kid>__…`), or both.
- `ManagementConsole__ManagementApiUrl`: the OP's management API, `/api` on the origin of
  `Bff__Frontends__mgmt__Authority`, such as `https://id.example.com/api`: the OP
  accepts the console's DPoP proofs only for that URL.
- `ConnectionStrings__valkey`, or the parts `ValkeyConnection__Host`, `ValkeyConnection__Port`,
  `ValkeyConnection__Username`, `ValkeyConnection__Password` and `ValkeyConnection__Ssl`: the Valkey server that holds
  the sessions, as a finished connection string or as its parts, which the adapter assembles into one. It is required
  for more than one replica; setting both forms, or the parts without `ValkeyConnection__Host`, stops the host at
  start
  ([ADR-1510](../../docs/decisions/1510-the-host-chart-passes-a-valkey-servers-address-and-password-as-parts-and-the-adapter-assembles-the-connection.md)).

A key ring can come from a key management service instead of a PEM file, or beside one. Each key is an entry
`Bff__KeyRings__<ring>__RemoteKeys__<kid>__Provider` with `__KeyId`, `__Algorithm`, `__SecretType`, `__KeySizeBits` and
`__Curve`. The console registers the AWS KMS, Azure Key Vault and Google Cloud KMS providers without a
key-creation location and reads each key where the key's identifier names, so it needs no
`SecretProtection__RemoteKeyProviders__…` setting. The provider's row for the algorithm decides whether the key signs or
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
