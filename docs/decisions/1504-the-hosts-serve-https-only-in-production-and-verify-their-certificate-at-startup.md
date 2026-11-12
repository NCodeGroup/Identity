# 1504. The hosts serve HTTPS only in Production and verify their certificate at startup

- **Status:** Accepted
- **Date:** 2026-11-11
- **Deciders:** NCode Group

## Context

A host behind a TLS-terminating ingress that speaks plain HTTP to the pod carries an HTTPS redirect it cannot use: the
request reaches the pod as HTTP, so the redirect middleware has no HTTPS port to redirect to and warns about it (issue
[#774](https://github.com/NCodeGroup/identity/issues/774)). The same shape leaves the hop from the proxy to the pod
unencrypted. Transport security should hold by construction, not by a setting that an operator has to remember.

Three facts shape the check:

- **Endpoints and certificates exist only after Kestrel binds them.** Endpoints come from configuration, from
  `ASPNETCORE_URLS`, from the base image's `ASPNETCORE_HTTP_PORTS` and from code, and the certificate of an HTTPS
  endpoint comes from `Kestrel:Certificates:Default`, `Kestrel:Endpoints:<name>:Certificate`, code or a selector. Only
  `IServerAddressesFeature.Addresses`, after binding, lists each endpoint with its real scheme.
- **A completed handshake proves more than a configuration scan.** A TLS connection to the endpoint reads the
  certificate the endpoint serves and proves that its private key is present and usable.
- **The kubelet cannot present a client certificate and does not verify a probe's server certificate.** What the pod's
  certificate protects is the hop from whatever routes traffic to the pod, which verifies it
  ([ADR-1507](1507-the-host-chart-routes-to-its-https-only-pods-through-gateway-api-a-traefik-ingress-or-the-operators-own-ingress.md)).

## Decision

**In the Production environment each host refuses to start unless every endpoint Kestrel bound is HTTPS and the
certificate it serves is usable. There is no opt-out, and a TLS-terminating proxy that speaks plain HTTP to the pod is
not a deployment shape.** Development, Staging, Conformance and every other environment are untouched.

1. **The check runs after Kestrel has bound.** `TransportSecurityStartupCheck` is an `IHostedLifecycleService` and
   checks at `StartedAsync`, as `BffEndpointStartupCheck` does
   ([ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md)). It verifies, in
   order, and throws one `InvalidOperationException` that lists every failure:
   1. **The server is Kestrel.** The server's type belongs to the assembly that holds `KestrelServer`. Any other server
      is refused, because the check cannot prove TLS on it.
   2. **At least one endpoint is bound, and every endpoint is `https` on a TCP port.** A plain-HTTP endpoint, a Unix
      socket and a named pipe are refused.
   3. **A loopback TLS handshake completes for each HTTPS endpoint.** The probe accepts any certificate only to read
      it. It sends nothing and trusts nothing. A handshake that completes no TLS is a failure.
   4. **The certificate the endpoint serves obeys the certificate rules.** It is inside its validity period; it has an
      RSA key of at least 2048 bits or an ECDSA key of at least 256 bits; it is signed with RSA PKCS#1 or PSS or with
      ECDSA, using SHA-256, SHA-384 or SHA-512; and it does not carry the ASP.NET Core development certificate's
      extension (`1.3.6.1.4.1.311.84.1.1`). A self-signed certificate passes, because trusting the pod's certificate
      is the proxy's and the platform's job.
2. **No opt-out.** The check has no setting that turns it off and no plain-HTTP deployment shape. Kestrel is bound for
   the interval between binding and the check, and the host exits before a readiness probe passes.
3. **One implementation.** `AddTransportSecurity(IHostEnvironment)` in `NCode.Identity.AspNetCore` registers the check
   only in Production. `AddProductionSafety` composes it for the OpenID host, and the management console host calls it
   directly, so the Production gate stays the one Production check and both hosts share one implementation.
4. **No redirect in Production.** `UseHttpsRedirectionOutsideProduction()` in `NCode.Identity.AspNetCore` adds the
   framework's HTTPS redirection in every environment except Production. Production has no plain-HTTP endpoint, so it
   has nothing to redirect. Development keeps the framework's redirect, which it relies on when it binds HTTPS and
   HTTP together.
5. **The image's TLS contract.** Both images set `ASPNETCORE_URLS=https://+:8443`, which takes precedence over the base
   image's `ASPNETCORE_HTTP_PORTS`, read the certificate from `/etc/ncode-identity/tls/tls.crt` and the key from
   `/etc/ncode-identity/tls/tls.key` (`Kestrel__Certificates__Default__Path` and `__KeyPath`), and expose port 8443.
   The mount point is the contract.
6. **The chart serves HTTPS.** The server's and the console's Deployments carry the TLS environment, the mount and the
   volume. The migration Job runs before cert-manager has created the Secret, and the secret-protection Job runs an
   offline command that binds no endpoint; neither carries a TLS environment, mount or volume. The container port is
   8443, the three probes use `scheme: HTTPS`, and the Service is `https` on port 443 with `appProtocol: https`, which
   the chart's Ingress backend names. Without an `existingSecret`, the chart creates a cert-manager `Certificate` per component, with
   an ECDSA P-256 key, from a private CA that it also creates (a self-signed Issuer, a CA Certificate and a CA Issuer).
   `backendTls.certManager.issuerRef` names an Issuer or ClusterIssuer of the operator's instead of the private CA, and
   `backendTls.existingSecret` (`managementConsole.backendTls.existingSecret` for the console) names a
   `kubernetes.io/tls` Secret of the operator's that also holds `ca.crt`. What routes traffic to the pods, and how it
   verifies their certificate, is decided in
   [ADR-1507](1507-the-host-chart-routes-to-its-https-only-pods-through-gateway-api-a-traefik-ingress-or-the-operators-own-ingress.md);
   the client certificate the pods can require of it is decided in
   [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md).
7. **Three trust tiers.** A private CA that cert-manager or a mesh runs, verified by the proxy, is preferred and is the
   default. A pinned self-signed certificate, whose `ca.crt` is the certificate itself, is acceptable. A self-signed
   certificate with proxy verification off is unauthenticated encryption, which stops passive eavesdropping and
   nothing else, and is discouraged; the chart never renders it.

## Options considered

- **Scan the configuration before Kestrel binds.** Rejected: it misses endpoints that code defines, the base image's
  `ASPNETCORE_HTTP_PORTS` and certificates that a selector or code supplies, and it duplicates Kestrel's binding rules.
- **An opt-out setting.** Rejected: a plain-HTTP hop between the proxy and the pod is the defect, so a setting that
  allows it leaves the defect to every operator who sets it.
- **Check only at the first handshake.** Rejected: a host with an unusable certificate then starts, passes its probes
  and fails its first client.
- **Parse Kestrel's certificate configuration.** Rejected: it duplicates Kestrel's schema and sees none of the
  certificates that code or a selector supplies. The handshake reads what the endpoint serves.
- **Require a client certificate from the proxy.** Decided in
  [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md).

## Consequences

- A host with a plain-HTTP endpoint, a non-Kestrel server or an unusable certificate fails at start, and the exception
  message lists every failure at once.
- Kestrel is bound for the interval between binding and the check, and the host exits before readiness passes.
- A certificate that Kestrel reloads after start is not verified again. Kestrel loads renewed certificate files it
  watches, and the check runs at start only.
- A TLS-terminating proxy has to speak HTTPS to the pod and, to meet the preferred trust tier, verify the pod's
  certificate.
- The HTTPS redirect of the pipeline applies outside Production only.
- The unit tests of the inspectors and the probe, the check against real Kestrel, the Production gate and the chart's CI
  gate prove the wiring.

## References

- Issue [#774](https://github.com/NCodeGroup/identity/issues/774)
- [ADR-1433](1433-a-bff-host-checks-every-endpoints-bff-metadata-at-start-and-again-whenever-the-endpoints-change.md),
  [ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package-outside-development.md),
  [ADR-1499](1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md),
  [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md),
  [ADR-1507](1507-the-host-chart-routes-to-its-https-only-pods-through-gateway-api-a-traefik-ingress-or-the-operators-own-ingress.md)
- `src/NCode.Identity.AspNetCore/TransportSecurity/`
- `src/NCode.Identity.AspNetCore/TransportSecurityRegistration.cs`
- `charts/ncode-identity-host`
