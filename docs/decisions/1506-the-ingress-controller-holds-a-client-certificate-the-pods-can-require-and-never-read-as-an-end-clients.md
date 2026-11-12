# 1506. The ingress controller holds a client certificate the pods can require and never read as an end client's

- **Status:** Accepted (amends
  [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md) and
  [ADR-1172](1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md); amended by
  [ADR-1507](1507-the-host-chart-routes-to-its-https-only-pods-through-gateway-api-a-traefik-ingress-or-the-operators-own-ingress.md):
  the chart issues the ingress Secret when a route uses it, and has no ingress-nginx mode)
- **Date:** 2026-11-11
- **Deciders:** NCode Group

## Context

The chart has the ingress controller verify the pods' certificate
([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)). Three gaps
remain on that hop:

- **The controller needs a Secret that holds the CA, and cert-manager issues none that holds only the CA.** The Secret
  that holds the pods' serving certificate also holds their private key, so a controller that reads it, which both
  ingress-nginx and Traefik do cluster-wide, receives the key of every pod it fronts (issue
  [#788](https://github.com/NCodeGroup/identity/issues/788)).
- **The pods accept a connection from anyone who can reach them.** The pod's certificate authenticates the pod to the
  proxy; nothing authenticates the proxy to the pod (issue [#789](https://github.com/NCodeGroup/identity/issues/789)).
- **The connection has one client-certificate slot.** RFC 8705 mutual-TLS client authentication and certificate-bound
  access tokens read an end client's certificate from it when TLS terminates at the pod
  ([ADR-1107](1107-mutual-tls-client-authentication.md),
  [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md)). A proxy that presents its own certificate there
  must never have it read as an end client's.

What the chart renders cannot prove that cert-manager issues the Secret, that the controller reads it, or that the pods
become Ready and the controller verifies and authenticates to them (issues
[#790](https://github.com/NCodeGroup/identity/issues/790) and
[#791](https://github.com/NCodeGroup/identity/issues/791)).

## Decision

**The ingress controller has a client certificate and a verification Secret of its own, the pods can require that
certificate on the proxy-to-pod hop, and a certificate the host verified as the proxy's is never read as an end
client's.**

1. **The chart issues the ingress Secret `<release>-ingress`.** A cert-manager `Certificate` of the same name has the
   `client auth` usage, the common name `backendTls.clientCertificate.commonName` and the same issuer as the pods'
   certificates. Its Secret holds `ca.crt`, `tls.crt` and `tls.key`. The `tls.key` is the controller's own identity,
   never a pod's, and the controller reads the one Secret for both jobs (ingress-nginx's `proxy-ssl-secret` and
   Traefik's `ServersTransport` read the CA and the client certificate from it). The pods' serving Secret is never the
   controller's Secret and is never a fallback. The Secret holds the controller's own key and certificate, which a
   Secret that holds only `ca.crt` cannot provide, and the property #788 asks for holds: no pod's key ever reaches the
   controller.
   - `backendTls.ingressSecret` and `managementConsole.backendTls.ingressSecret` name a Secret the operator created.
     That Secret is then the only Secret the controller verifies against. The console's value falls back to the
     server's, then to `<release>-ingress`.
   - The chart renders the Certificate when the ingress controller is not `none`, an Ingress that needs it is enabled
     and no `ingressSecret` applies to that Ingress. It renders the release's private CA whenever the Certificate needs
     it.
   - When `backendTls.certManager.issuerRef.name` is empty and a component's `existingSecret` is set without an
     `ingressSecret` for it, rendering fails: the chart cannot know the CA that signed the operator's certificate.
2. **The `ProxyClientCertificate` configuration section of `NCode.Identity.AspNetCore` requires the proxy's
   certificate.** `Required` (default `false`), `CertificateAuthorityPath` and `AllowedSubjects` are bound strictly and
   shared by the OpenID host and the management console host. The options validator refuses `CertificateAuthorityPath`
   or `AllowedSubjects` while `Required` is false, and refuses a `Required` section without an existing PEM file of
   certificates or without a distinguished name in `AllowedSubjects`.
   - **The handshake asks and accepts.** While `Required` is true, the hosts' Kestrel HTTPS defaults use
     `ClientCertificateMode.AllowCertificate` and accept any certificate, so the kubelet's probes and the startup
     check's loopback handshake ([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md))
     connect without one.
   - **A middleware, first in the pipeline, decides.** It lets exactly `HealthProbeDefaults.LivenessPath` and
     `ReadinessPath` through without a certificate and answers `403` to every other request whose connection
     certificate is missing or not the proxy's.
   - **A certificate is the proxy's** when it chains to the anchors in `CertificateAuthorityPath`, has the `client auth`
     extended key usage, is inside its validity period and has a subject that is one of `AllowedSubjects`. Revocation is
     not checked: the chart's certificates are short-lived and issued per release. The anchors are read again when the
     file's last-write time changes, so a renewed `ca.crt` takes effect without a restart.
3. **A verified proxy certificate is never an end client's.** A request that passes the middleware carries
   `IProxyClientCertificateFeature`, whose certificate is the proxy's. `DefaultClientCertificateResolver` never reads
   the connection's certificate as an end client's on such a connection and reports no certificate.
   - With `OpenId:MutualTls:CertificateHeaderName` configured, the header stays the only source
     ([ADR-1172](1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md)). A proxy that terminates
     the end client's TLS hands the end client's certificate to the pod in that header, and RFC 8705 `tls_client_auth`
     and `self_signed_tls_client_auth` work behind the proxy through it.
   - A direct-TLS deployment that serves end clients' certificates on the connection does not enable
     `ProxyClientCertificate`.
4. **The chart enables it with `backendTls.clientCertificate`.** `required` (default `false`) and `commonName` (default
   `ncode-identity-ingress`) apply to the server and the console. The chart sets `ProxyClientCertificate__Required`,
   `__CertificateAuthorityPath` to the mounted serving Secret's `ca.crt`, so no further mount is needed, and
   `__AllowedSubjects__0` to `CN=<commonName>`. For Traefik it adds `certificatesSecrets`, the ingress Secret, to each
   `ServersTransport`. ingress-nginx presents the client certificate that its `proxy-ssl-secret` holds. With
   `ingressController: none`, or with an `ingressSecret` the operator created, the operator's certificate must have the
   subject `CN=<commonName>` and be issued by the CA in the serving Secret's `ca.crt`.
5. **The chart workflow gates the Secret and installs the chart in kind.** The `check` job renders the chart and
   fails unless each controller verifies against the ingress Secret by default, against an operator's `ingressSecret`
   when set, and never against a serving Secret. The `cluster` job installs the chart's server in a kind cluster behind
   ingress-nginx and behind Traefik, one cluster each, with cert-manager, the controller and a Postgres stub, from the
   host image built from the `host-image` build cache. It proves that:
   - the pods become Ready over HTTPS, and a request through the controller succeeds;
   - a controller whose verification Secret holds a foreign CA refuses the request, with `502` from ingress-nginx and `500` from Traefik;
   - with `clientCertificate.required`, a request through the controller still succeeds, a direct request without a
     certificate answers `403` on `/` and `200` on `/health/live`, and a direct request with the ingress Secret's
     certificate is not `403`.

## Options considered

- **A trust-manager `Bundle`.** Rejected: trust-manager reads its sources only from its own trust namespace, and it is
  an extra cluster-wide dependency for one file.
- **A Helm-generated CA read back with `lookup`.** Rejected: the CA's key lives in the release, and `lookup` returns
  nothing under `helm template`, so a GitOps tool diffs a different CA on every render.
- **A post-install Job that copies `ca.crt` into a Secret.** Rejected: it needs RBAC and an image to produce one file
  that cert-manager already writes.
- **`ClientCertificateMode.RequireCertificate`.** Rejected: it refuses the kubelet's probes and the startup check's
  loopback handshake, which cannot present a certificate.
- **A path-based Kestrel endpoint split.** Rejected: a second port for the probes means a second endpoint to bind,
  expose and keep out of the Ingress, for the two paths the middleware exempts.
- **Read the proxy's certificate as the end client's and let a header override it.** Rejected: a request would carry
  two certificates with no rule that says which one is the client's, and an end client's certificate would depend on
  whether a header happened to arrive.

## Consequences

- A pod reached directly without the proxy's certificate answers `403` outside the liveness and readiness probes, so
  the host is reachable only through the proxy.
- `ProxyClientCertificate` and end clients' certificates on the connection do not mix: a deployment that serves
  end clients' certificates directly leaves the section off.
- The controller's Secret holds a private key that belongs to the proxy, never one that belongs to a pod. The key is
  useless except to authenticate the proxy to the pods.
- The chart refuses a release whose operator-supplied serving certificate has no known CA and no `ingressSecret`.
- The kind job covers the server behind ingress-nginx and Traefik. The management console, the end-to-end suites and
  the image workflows are outside it.
- Revocation of the proxy's certificate is not checked; renewal through cert-manager's short validity is the control.

## References

- Issues [#788](https://github.com/NCodeGroup/identity/issues/788),
  [#789](https://github.com/NCodeGroup/identity/issues/789),
  [#790](https://github.com/NCodeGroup/identity/issues/790) and
  [#791](https://github.com/NCodeGroup/identity/issues/791)
- [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md),
  [ADR-1172](1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md),
  [ADR-1107](1107-mutual-tls-client-authentication.md),
  [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md)
- `src/NCode.Identity.AspNetCore/ProxyClientCertificate/`
- `src/NCode.Identity.AspNetCore/ProxyClientCertificateRegistration.cs`
- `src/NCode.Identity.OpenId.Authentication/ClientCertificates/DefaultClientCertificateResolver.cs`
- `charts/ncode-identity-host`
- `.github/workflows/host-chart.yml`
