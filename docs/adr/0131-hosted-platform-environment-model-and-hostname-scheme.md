# 131. The hosted platform's environment model and hostname scheme

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The OpenID Provider will be run as a hosted, multi-tenant service. Two choices about that service are effectively
permanent once the first customer integrates:

- **Where customers' non-production environments run.** Every customer needs dev and test environments alongside
  production. They can be separate platform environments (a customer-facing staging cluster) or tenants on the one
  production platform, as in Auth0's model ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- **The hostnames.** A tenant's issuer is baked into every client's configuration and into the `iss` of every token it
  mints, and it is also the host the tenant is routed on
  ([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md)). Changing an issuer later breaks every
  integration with that tenant.

Further forces: a wildcard certificate covers a single DNS label; RFC 8705 §5 mTLS endpoint aliases need a host where
the proxy requests a client certificate without affecting browsers on the main host; the management API is a separate
control-plane surface ([ADR-0031](0031-control-plane-management-resource-server-and-root-tenant-seeding.md),
[ADR-0042](0042-management-api-route-driven-tenant-scope.md)); tenants on one registrable domain must not share
cookies; and the platform budget (~$100/month) cannot fund a second customer-grade cluster.

## Decision

**Customer environments are tenants on production, and every platform host lives in a functional namespace under one
apex domain.**

1. **Environment model.**
   - A customer's dev, test, and prod are separate tenants on the production platform (`acme-dev`, `acme-test`,
     `acme`). Promotion between them is tenant configuration, not infrastructure.
   - The platform runs exactly one customer-facing environment, **production**, which also hosts internal `canary`
     (post-deploy verification) and `conformance` (OpenID certification) tenants.
   - The platform owner's private **lab** (development, sandbox, conformance experiments) and ephemeral CI environments
     never host a customer tenant.

2. **Hostname scheme** (`<apex>` is `nidentity.io`):

   | Hostname                                    | Serves                                                                |
   | ------------------------------------------- | --------------------------------------------------------------------- |
   | `auth.<apex>`                               | Root (control-plane) tenant issuer                                    |
   | `{tenant}.auth.<apex>`                      | Workload tenant issuers, resolved by `DynamicByHost`                  |
   | `mtls.<apex>`, `{tenant}.mtls.<apex>`       | RFC 8705 mTLS endpoint aliases — the only hosts that request client certificates |
   | `manage.<apex>`                             | Management API (`/api`, routed only here) and admin console           |
   | `<apex>`, `www.<apex>`                      | Project site and documentation                                        |

   The lab inserts a `lab` label after the function (`auth.lab.<apex>`, `{tenant}.auth.lab.<apex>`); ephemeral CI runs
   use `{run}.ci.<apex>`. A tenant's custom domain *becomes* its issuer (ADR-0129), so it is chosen before clients
   integrate.

3. **DNS and certificates.** The registrar holds only the nameserver and DNSSEC delegation (a one-time manual
   bootstrap); the zone is hosted on Cloudflare DNS (DNS-only, never proxied, so TLS reaches the platform's proxy
   untouched) and managed by OpenTofu. cert-manager issues Let's Encrypt certificates (DNS-01 for wildcards); CAA
   restricts issuance to Let's Encrypt. Lab and CI clusters never hold a token that can edit the production zone. Session
   cookies are host-only (`__Host-` prefix, no `Domain` attribute).

If `nidentity.com` is acquired before the first customer integrates, it replaces `nidentity.io` as the apex and the
scheme is otherwise unchanged; after that point the apex is permanent.

## Options considered

- **Customer-facing staging or dev platform environments.** Gives customers a "safe" place to test, but their
  non-production then runs on different hardware, versions, and configuration than their production — incidents get
  blamed on drift — and it multiplies the platform's cost. Auth0 does not do this. Rejected.
- **Tenants directly under the apex** (`acme.<apex>`). Shorter, but a tenant name can collide with a service host
  (`www`, `manage`, `mtls`, `status`, `docs`, …), which needs a reserved-name list forever. Rejected.
- **mTLS aliases nested under the tenant** (`mtls.{tenant}.auth.<apex>`). Reads naturally, but a wildcard covers one
  label, so every tenant would need its own certificate. Rejected in favor of the sibling `{tenant}.mtls.<apex>`.
- **Path-based tenants on one host** (`DynamicByPath`). One certificate, but tenants then share an origin (and its
  cookies), the proxy cannot apply a per-tenant TLS policy, and custom domains don't fit. Rejected for the hosted
  platform; it remains a supported strategy for other deployments.
- **`id.` or `login.` instead of `auth.`** The hosts serve machine-to-machine token, discovery, and JWKS endpoints as
  well as the login UI; `auth.` is the label developers expect.
- **Traefik's built-in ACME client.** Keeps state per instance and does not share it across replicas. Rejected in favor
  of cert-manager.

## Consequences

- Customers' load tests share hardware with every tenant's production, so per-tenant rate limits and quotas (`F45`)
  are a launch prerequisite.
- Per-tenant mTLS alias hosts need host-strategy routing for the alias authority (`F47`), in-app chain validation
  (`F43`), and Traefik's forwarded-chain format (`F44`).
- The lab is cheaper and differs from production (k3s, arm64, in-cluster Postgres vs. managed Kubernetes, x86, managed
  Postgres); every release is verified on the production `canary` tenant before customers see it.
- Each function gets one wildcard certificate, and the management plane can carry its own CORS, rate-limit, and
  IP-allow-list policy at the proxy.
- The `.io` ccTLD's long-term status is an accepted risk; owning a fallback domain mitigates it.

## References

- [Hosting analysis](../hosting-analysis.md) — vendor comparison, environment model, and DNS plan in full.
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — a tenant can later move to a dedicated deployment by DNS
  change without changing its issuer.
- [ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md) — mTLS sender-constraining and the forwarded
  client certificate.
- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mutual-TLS endpoint aliases.
