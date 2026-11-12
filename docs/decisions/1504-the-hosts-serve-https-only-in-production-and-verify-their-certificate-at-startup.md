# 1504. The hosts serve HTTPS only in Production and verify their certificate at startup

- **Status:** Accepted (amends
  [ADR-1178](1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md),
  [ADR-1179](1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md),
  [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md) and
  [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md);
  amended by
  [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md):
  the controller verifies the pods against the chart's ingress Secret and authenticates to them with its client
  certificate)
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
  certificate protects is the hop from the ingress controller, which verifies it.

## Decision

**In the Production environment each host refuses to start unless every endpoint Kestrel bound is HTTPS and the
certificate it serves is usable. There is no opt-out, and a TLS-terminating proxy that speaks plain HTTP to the pod is
not a deployment shape.** Development, Staging, Conformance and every other environment are untouched.

1. **The check runs after Kestrel has bound.** `TransportSecurityStartupCheck` is an `IHostedLifecycleService` and
   checks at `StartedAsync`, as `BffEndpointStartupCheck` does
   ([ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md)). It verifies, in
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
6. **The chart serves HTTPS and has the ingress controller verify the pods.** The values `backendTls` and
   `managementConsole.backendTls` select the ingress controller (`ingress-nginx` by default, `traefik` or `none`), an
   `existingSecret` (a `kubernetes.io/tls` Secret that also holds `ca.crt`), the cert-manager `issuerRef`, and an
   optional `ingressSecret` (a Secret the operator created, which is then the only Secret the controller verifies the
   pods against; the console's own value falls back to the server's, and without either the controller reads the
   chart's ingress Secret, never a serving Secret, see [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)). By
   default the chart creates a cert-manager `Certificate` per component from a private CA that it also creates (a
   self-signed Issuer, a CA Certificate and a CA Issuer), with an ECDSA P-256 key. The container port is 8443, the
   probes use `scheme: HTTPS`, the Service is `https` on port 443 with `appProtocol: https`, and the Ingress backend
   names that port. For ingress-nginx the chart renders the Ingress annotations that turn on HTTPS to the backend and
   verification of the pod's certificate (`proxy-ssl-verify: "on"` with the Secret and the server name). For Traefik
   it renders a `ServersTransport` and two Service annotations. For `none` it renders neither, and the operator
   configures the controller. The Deployment alone carries the TLS environment, mount and volume: the migration and
   secret-protection Jobs run before cert-manager has created the Secret.
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
- The controller's verification Secret is the chart's ingress Secret by default, never a serving Secret;
  `backendTls.ingressSecret` names an operator's Secret instead
  ([1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)).
- The HTTPS redirect of the pipeline applies outside Production only.
- The unit tests of the inspectors and the probe, the check against real Kestrel, the Production gate and the chart's CI
  gate prove the wiring.

## References

- Issue [#774](https://github.com/NCodeGroup/identity/issues/774)
- [ADR-1433](1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md),
  [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md),
  [ADR-1499](1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md)
- `src/NCode.Identity.AspNetCore/TransportSecurity/`
- `src/NCode.Identity.AspNetCore/TransportSecurityRegistration.cs`
- `charts/ncode-identity-host`
