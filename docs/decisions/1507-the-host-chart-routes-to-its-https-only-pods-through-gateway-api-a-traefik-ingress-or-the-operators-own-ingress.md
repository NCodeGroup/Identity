# 1507. The host chart routes to its HTTPS-only pods through Gateway API, a Traefik Ingress or the operator's own ingress

- **Status:** Accepted (amends
  [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md) and
  [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md))
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

The pods serve HTTPS only
([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)), so whatever
routes traffic to them must speak HTTPS to the Service and verify the pods' certificate, and, when the pods require it,
present the ingress controller's client certificate
([ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)).
Five facts shape how the chart does that (issue [#792](https://github.com/NCodeGroup/identity/issues/792)):

- **ingress-nginx is retired.** Its maintenance ended in March 2026 and its repository is archived, so it receives no
  fixes. A chart that renders its annotations points operators at software that no longer gets security updates.
- **Gateway API has a standard way to verify a backend.** `BackendTLSPolicy` is Standard since Gateway API v1.4. It
  targets a Service port, names the host name to check and takes the CA from a ConfigMap.
- **A Gateway, not a route, presents a backend client certificate.** Gateway API places the client certificate a
  Gateway presents to its backends on the Gateway (`spec.tls.backend.clientCertificateRef`, GEP-3155), which the
  cluster's operator owns, and it applies to every backend of that Gateway.
- **The ingress controller has a Secret of its own.** The chart issues `<release>-ingress`, which holds the CA that
  signed the pods' certificates and the controller's client certificate, never a pod's key (ADR-1506). Traefik's
  `ServersTransport` reads its CA (`rootCAs`) and its client certificate (`certificatesSecrets`) from a Secret.
- **cert-manager writes only Secrets.** `BackendTLSPolicy` reads only ConfigMaps, so the CA a Gateway verifies against
  is a ConfigMap that something other than cert-manager writes.

## Decision

**The chart routes to the pods in one of three modes, for the server and the console alike, and no mode gives a router
the pods' private key: the Gateway API mode verifies the pods against a CA ConfigMap that holds only `ca.crt`, and the
Traefik mode against the ingress controller's own Secret.** This amends decision 6 of ADR-1504 and decision 1 of
ADR-1506.

1. **Gateway API.** `httpRoute.enabled` (`managementConsole.httpRoute.enabled` for the console) renders an `HTTPRoute`
   attached to the operator's Gateway through `parentRefs` that sends every path to the Service's `https` port, and a
   `BackendTLSPolicy` that targets that port and verifies the pods' certificate against the CA ConfigMap under the host
   name `<service>.<namespace>.svc`, a name the pods' certificate carries. The server's route names its `hostnames`; the
   console's hostname is `managementConsole.host`. When `backendTls.clientCertificate.required` is true, the operator
   names the ingress controller's Secret as the Gateway's backend client certificate, because the chart does not own
   the Gateway.
2. **Traefik Ingress.** `ingress.controller: traefik` renders a `ServersTransport` with `serverName`, `rootCAs` naming
   the ingress controller's Secret and, when `backendTls.clientCertificate.required` is true, `certificatesSecrets`
   naming the same Secret, and the Service annotations `service.serversscheme` and `service.serverstransport`.
3. **Other Ingress.** `ingress.controller: other` renders neither. The operator wires HTTPS to the Service, the trust in
   the CA and, when the pods require it, the controller's client certificate.
4. **No default mode.** Routes stay off unless enabled, and an enabled Ingress must set `ingress.controller`, because a
   guessed controller renders the wrong backend TLS without a sign. The Ingress and the HTTPRoute may both be enabled.
   `ingress.controller` serves the console's Ingress too.
5. **The Gateway API mode's CA is a ConfigMap.** `backendTls.caConfigMap` names a ConfigMap in the release namespace
   that holds only `ca.crt`; `managementConsole.backendTls.caConfigMap` names the console's and falls back to the
   server's.
   The Gateway API mode requires it. trust-manager, with a `Bundle` that targets a ConfigMap, is the documented source,
   and `kubectl create configmap <name> --from-file=ca.crt=<ca.pem>` the manual one. `backendTls.caPublisher` publishes
   the chart's private CA itself: a post-install and post-upgrade hook Job runs the official `kubectl` image with
   `kubectl apply -k` over a projected volume that holds a static `kustomization.yaml` and only the `ca.crt` key of the
   `<release>-ca` Secret, and writes the ConfigMap `backendTls.caConfigMap` names, or `<release>-ca`.
6. **The chart issues the ingress controller's Secret when a route uses it.** A component's route uses it when the
   component's Ingress is enabled with `ingress.controller: traefik`, or any of its routes is enabled and
   `backendTls.clientCertificate.required` is true. The chart then renders the `<release>-ingress` Certificate of
   ADR-1506 unless an `ingressSecret` applies, and refuses a component's `existingSecret` without an `ingressSecret`
   when no `backendTls.certManager.issuerRef` names the CA that signed it.
7. **No ingress-nginx mode.** The chart renders nothing for ingress-nginx, and `rootCAsSecrets`, which Traefik
   deprecates, is not rendered.

## Options considered

- **Keep the ingress-nginx mode.** Rejected: the project is retired and archived, so the mode would configure software
  that receives no fixes.
- **A default controller.** Rejected: a guess renders the wrong backend TLS, and the release then serves a failing or
  unverified hop without any sign.
- **Verify Traefik against the CA ConfigMap.** Rejected: the ingress controller's Secret already holds the CA and the
  client certificate Traefik presents, so one object serves both and no pod's key reaches Traefik.
- **Render the Gateway's backend client certificate.** Rejected: the Gateway belongs to the cluster's operator and
  serves every backend behind it, so a chart that set it would write into an object it does not own.
- **Build the CA ConfigMap with `lookup`.** Rejected: `lookup` returns nothing under `helm template` and Argo CD, so the
  chart would render differently from how it installs.
- **A trust-manager `Bundle` that the chart renders.** Rejected: trust-manager reads its sources from its own trust
  namespace, so the cluster's operator owns the `Bundle`, and a chart that renders one writes into a namespace it does
  not own.
- **A publisher Job with a shell image.** Rejected: the official `kubectl` image has no shell, and a third-party image
  widens the supply chain; `kubectl apply -k` over a projected volume needs no shell.
- **Delete and recreate the ConfigMap.** Rejected: every upgrade would leave the routes without a CA for a moment, where
  `kubectl apply -k` creates or updates it in one call.
- **Verify against the serving Secret in any mode.** Rejected: `BackendTLSPolicy` cannot read a Secret, and the Secret
  holds the pods' private key, which no router needs.

## Consequences

- An operator names the CA ConfigMap for the Gateway API mode; the Traefik mode needs none.
- With the chart's private CA, `backendTls.caPublisher` publishes the CA; without it, the operator creates the ConfigMap
  once cert-manager has issued `<release>-ca`, or takes the CA from a cluster issuer and trust-manager.
- The publisher's `kubectl` image is pinned by digest and kept current by hand, and `helm uninstall` leaves the
  ConfigMap it wrote.
- In the Gateway API mode a Gateway presents one backend client certificate to every backend, so pods that require it
  accept the subject of the Gateway's certificate in `backendTls.clientCertificate.commonName`, issued by the CA that
  signed the pods' certificates.
- The Gateway API mode requires Gateway API v1.4 or later, for `BackendTLSPolicy` at `gateway.networking.k8s.io/v1`.
- The chart refuses an enabled Ingress without `ingress.controller`, and an enabled HTTPRoute without `parentRefs`,
  without `hostnames` (the server's) or without a CA ConfigMap. It refuses `backendTls.caPublisher` with a server
  certificate from another CA, and with a console certificate in `managementConsole.backendTls.existingSecret` when
  `managementConsole.backendTls.caConfigMap` is empty, because the publisher writes only the chart's private CA.
- The `cluster` job of the `Host chart` workflow proves the Traefik mode in kind; what the chart renders proves the
  Gateway API mode.
- Each mode stays correct for one controller family, and a controller the chart does not configure uses
  `ingress.controller: other`.

## References

- Issue [#792](https://github.com/NCodeGroup/identity/issues/792)
- [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)
- [ADR-1506](1506-the-ingress-controller-holds-a-client-certificate-the-pods-can-require-and-never-read-as-an-end-clients.md)
- [GEP-3155: Complete Backend mutual TLS Configuration](https://gateway-api.sigs.k8s.io/geps/gep-3155/)
- `charts/ncode-identity-host`
