# 1506. The host chart routes to its HTTPS-only pods through Gateway API, a Traefik Ingress or the operator's own ingress

- **Status:** Accepted (amends [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md))
- **Date:** 2026-11-12
- **Deciders:** NCode Group

## Context

The pods serve HTTPS only
([ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)), so whatever
routes traffic to them must speak HTTPS to the Service and verify the pods' certificate. Four facts shape how the chart
does that (issue [#792](https://github.com/NCodeGroup/identity/issues/792)):

- **ingress-nginx is retired.** Its maintenance ended in March 2026 and its repository is archived, so it receives no
  fixes. A chart that renders its annotations points operators at software that no longer gets security updates.
- **Gateway API has a standard way to verify a backend.** `BackendTLSPolicy` is Standard since Gateway API v1.4. It
  targets a Service port, names the host name to check and takes the CA from a ConfigMap.
- **The routers read the CA from different objects.** `BackendTLSPolicy` reads only ConfigMaps, and Traefik's `rootCAs`
  on a `ServersTransport` accepts a ConfigMap or a Secret.
- **cert-manager writes only Secrets.** The Secret that holds the pods' certificate holds their private key too, so a
  router that verifies against it, and reads Secrets cluster-wide, receives that key.

## Decision

**The chart routes to the pods in one of three modes, for the server and the console alike, and every mode verifies the
pods against a CA ConfigMap that holds only `ca.crt`.** This amends decision 6 of ADR-1504 and its consequence about the
verification Secret.

1. **Gateway API.** `httpRoute.enabled` (`managementConsole.httpRoute.enabled` for the console) renders an `HTTPRoute`
   attached to the operator's Gateway through `parentRefs` that sends every path to the Service's `https` port, and a
   `BackendTLSPolicy` that targets that port and verifies the pods' certificate against the CA ConfigMap under the host
   name `<service>.<namespace>.svc`, a name the pods' certificate carries. The server's route names its `hostnames`; the
   console's hostname is `managementConsole.host`.
2. **Traefik Ingress.** `ingress.controller: traefik` renders a `ServersTransport` with `serverName` and `rootCAs`, and
   the Service annotations `service.serversscheme` and `service.serverstransport`. `rootCAs` names the CA ConfigMap
   when `backendTls.caConfigMap` is set and otherwise the component's serving Secret, which holds the key.
3. **Other Ingress.** `ingress.controller: other` renders neither. The operator wires HTTPS to the Service and the trust
   in the CA.
4. **No default mode.** Routes stay off unless enabled, and an enabled Ingress must set `ingress.controller`, because a
   guessed controller renders the wrong backend TLS without a sign. The Ingress and the HTTPRoute may both be enabled.
   `ingress.controller` serves the console's Ingress too.
5. **The CA is a ConfigMap the operator names.** `backendTls.caConfigMap` names a ConfigMap in the release namespace that
   holds only `ca.crt`; `managementConsole.backendTls.caConfigMap` names the console's and falls back to the server's.
   The Gateway API mode requires it. trust-manager, with a `Bundle` that targets a ConfigMap, is the documented source,
   and `kubectl create configmap <name> --from-file=ca.crt=<ca.pem>` the manual one.
6. **No ingress-nginx mode.** The chart renders nothing for ingress-nginx, and `rootCAsSecrets`, which Traefik
   deprecates, is not rendered.

## Options considered

- **Keep the ingress-nginx mode.** Rejected: the project is retired and archived, so the mode would configure software
  that receives no fixes.
- **A default controller.** Rejected: a guess renders the wrong backend TLS, and the release then serves a failing or
  unverified hop without any sign.
- **Build the CA ConfigMap with `lookup`.** Rejected: `lookup` returns nothing under `helm template` and Argo CD, so the
  chart would render differently from how it installs.
- **A trust-manager `Bundle` that the chart renders.** Rejected: trust-manager reads its sources from its own trust
  namespace, so the cluster's operator owns the `Bundle`, and a chart that renders one writes into a namespace it does
  not own.
- **Verify against the serving Secret in every mode.** Rejected: `BackendTLSPolicy` cannot read a Secret, and the Secret
  holds the pods' private key, which no router needs.

## Consequences

- An operator names the CA ConfigMap for the Gateway API mode and, to keep the pods' private key from Traefik, for the
  Traefik mode too. Without it Traefik verifies against the serving Secret.
- The chart's private CA yields the PEM only once cert-manager has issued `<release>-ca`, so the operator creates the
  ConfigMap after that, or takes the CA from a cluster issuer and trust-manager.
- The Gateway API mode requires Gateway API v1.4 or later, for `BackendTLSPolicy` at `gateway.networking.k8s.io/v1`.
- The chart refuses an enabled Ingress without `ingress.controller`, and an enabled HTTPRoute without `parentRefs`,
  without `hostnames` (the server's) or without a CA ConfigMap.
- Each mode stays correct for one controller family, and a controller the chart does not configure uses
  `ingress.controller: other`.

## References

- Issue [#792](https://github.com/NCodeGroup/identity/issues/792)
- [ADR-1504](1504-the-hosts-serve-https-only-in-production-and-verify-their-certificate-at-startup.md)
- `charts/ncode-identity-host`
