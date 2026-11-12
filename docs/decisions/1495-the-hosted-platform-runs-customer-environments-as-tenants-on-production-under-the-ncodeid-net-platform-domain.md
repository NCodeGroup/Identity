# 1495. The hosted platform runs customer environments as tenants on production under the ncodeid.net platform domain

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

The OpenID Provider runs as a hosted, multi-tenant service. Two choices about that service are permanent once the first
customer integrates:

- **Where customers' non-production environments run.** Every customer needs dev and test environments beside
  production. They can be separate platform environments (a customer-facing staging cluster) or tenants on the one
  production platform, as in Auth0's model
  ([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).
- **The hostnames.** A tenant's issuer is the single authoritative origin of its tokens, discovery document and
  endpoint aliases ([ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)). A tenant's relying
  parties pin the issuer string, and it is also the host the tenant is routed on, so the issuer hosts are permanent.

Further forces:

- A wildcard certificate covers a single DNS label.
- RFC 8705 section 5 mTLS endpoint aliases need a host where the proxy requests a client certificate without affecting
  browsers on the main host.
- The management API is a separate control-plane surface.
- Tenants on one registrable domain must not share cookies.
- The platform budget cannot fund a second customer-grade cluster.
- A brand domain carries a different kind of host. Its subdomains point at third-party services (the project site and
  docs host, the status page), its zone holds the mail records of the people who operate the platform, and a person
  manages some of its integrations by hand. Each of those is a reason to change the zone, to hand a vendor a scoped
  token or to relax a header, and none of them is a reason to touch an issuer.
- A country-code TLD can be retired: the ccNSO's retirement policy removes a ccTLD from the root five years after
  ISO 3166-1 deletes its code, or up to ten years with a retirement plan. A permanent issuer cannot sit under a domain
  whose registry can be withdrawn.

## Decision

**Customer environments are tenants on production, and every issuer-bearing host lives under a platform domain that
carries nothing else; a brand domain carries the site, docs, status page and operator mail.**

1. **Environment model.**
   - A customer's dev, test and prod are separate tenants on the production platform (`acme-dev`, `acme-test`,
     `acme`). Promotion between them is tenant configuration, not infrastructure.
   - The platform's environments are exactly `production`, `lab` and `ci`. Production hosts the customers' tenants and
     the internal `canary` (post-deploy verification) and `conformance` (OpenID certification) tenants. The platform
     owner's private lab, where platform changes are proven before production, and the ephemeral ci environments never
     host a customer tenant.
   - Platform environments and tenant environments never share a word: `dev`, `test` and `staging` name only
     customers' tenants, never a platform environment, host, tag or root.
   - In a hostname a tenant is always the leftmost label (`acme-dev.auth.ncodeid.net`) and a platform environment
     follows the function (`auth.lab.ncodeid.net`). A tenant's custom domain becomes its issuer, so it is chosen
     before clients integrate.
2. **The platform domain is `ncodeid.net`, and it is the only domain that carries an issuer.**

   | Host                                       | Serves                                                                      |
   | ------------------------------------------ | --------------------------------------------------------------------------- |
   | `auth.ncodeid.net`                         | Root tenant issuer; the management API is `/api` on it                      |
   | `{tenant}.auth.ncodeid.net`                | Workload tenant issuers, resolved by `DynamicByHost`                        |
   | `mtls.` and `{tenant}.mtls.ncodeid.net`    | RFC 8705 mTLS aliases, the only hosts that request client certificates      |
   | `manage.ncodeid.net`                       | The management console only ([ADR-1437](1437-the-management-console-is-a-confidential-client-whose-own-bff-host-serves-only-the-console.md)) |
   | `….lab.ncodeid.net`                        | Lab hosts, with `lab` after the function                                    |
   | `{run}.ci.ncodeid.net`                     | Ephemeral CI runs                                                           |

   The apex and `www.` of `ncodeid.net` are DNS-only records to the platform's ingress, which serves a 301 to
   `https://ncodeid.com` with a cert-manager Let's Encrypt certificate (HTTP-01) for those two hosts.
3. **The brand domain is `ncodeid.com`.** It carries the site and docs (apex and `www.`), `status.ncodeid.com`
   (UptimeRobot), the Email Routing addresses (`security@`, `platform@`, `postmaster@`, `abuse@` and per-vendor
   aliases) and the `security.txt` contact.
4. **Two redirect-only domains guard the name.** `ncodeidentity.com` and `ncodeidentity.net` redirect their apex and
   `www.` (301) to `https://ncodeid.com`, and handle no mail. `ncodegroup.com` is not the platform's: the platform's
   stacks and records never manage it.
5. **Each domain is its own Cloudflare zone.** All four are registered at Porkbun, and the bootstrap stack creates each
   zone with DNSSEC and a scoped CI token. Identity hosts are DNS-only, never proxied, so TLS reaches the platform's
   proxy untouched. cert-manager issues Let's Encrypt certificates, with DNS-01 for wildcards.
6. **CAA limits issuance to Let's Encrypt and Universal SSL is off**, except in the two redirect-only zones. Their
   Cloudflare Single Redirect rules run on proxied records, which need Cloudflare's edge certificates, so Universal SSL
   is on there and their CAA records list Cloudflare's certificate authorities. They carry no issuer.
7. **Only the platform's OpenTofu roots write the platform zone.** No hosting-provider or vendor integration gets a
   token for it, and lab and CI clusters hold no token that edits any zone.
8. **Every domain is under a generic TLD**, because a registry can retire a ccTLD. `ncodeid.net` and `ncodeid.com` are
   renewed to the registry's 10-year maximum and auto-renew; the redirect domains auto-renew yearly.
9. **Mail.** Every domain publishes DMARC `p=reject`. Every domain but `ncodeid.com` publishes SPF `-all` and a null MX
   (`0 .`); `ncodeid.com` publishes the Email Routing records. System mail sends only from `mail.ncodeid.net`.
10. **HSTS follows who serves the subdomains.** `ncodeid.net` is submitted to the HSTS preload list with
    `includeSubDomains`, and the apex redirect serves the header
    ([ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md)). `ncodeid.com` sends
    `includeSubDomains` without preload, because third parties serve its subdomains.
11. **`ncodeid.net` is not on the Public Suffix List.** Host-only `__Host-` cookies (no `Domain` attribute) are the
    cookie control. Listing stays available; it needs at least two years of registration remaining and a `_psl` TXT
    record.

## Options considered

- **Customer-facing staging or dev platform environments.** Customers' non-production would run on different
  hardware, versions and configuration than their production, so incidents get blamed on drift, and it multiplies the
  platform's cost. Auth0 does not do this. Rejected.
- **Calling the lab `dev`, `staging`, `sandbox` or `playground`.** `dev` collides with customers' `-dev` tenants;
  `staging` promises production parity the lab lacks; `sandbox` is what other vendors call their customer-facing test
  environments and would invite the integrations this decision forbids; `playground` names the flow explorer
  ([ADR-1084](1084-the-playground-explores-the-recommended-flow-and-the-conformance-harness-measures-the-published-spec-surface.md)). Rejected for `lab`.
- **Tenants directly under the apex.** A tenant name can collide with a service host (`www`, `manage`, `mtls`,
  `status`, `docs`), which needs a reserved-name list forever. Rejected.
- **mTLS aliases nested under the tenant** (`mtls.{tenant}.auth`). A wildcard covers one label, so every tenant would
  need its own certificate. Rejected for the sibling `{tenant}.mtls`.
- **Path-based tenants on one host** (`DynamicByPath`). Tenants then share an origin and its cookies, the proxy cannot
  apply a per-tenant TLS policy, and custom domains do not fit. Rejected for the hosted platform; it stays a supported
  strategy for other deployments.
- **`id.` or `login.` instead of `auth.`.** The hosts serve token, discovery and JWKS endpoints as well as the login
  UI, and `auth.` is the label developers expect. Rejected.
- **Traefik's built-in ACME client.** It keeps state per instance and does not share it across replicas. Rejected for
  cert-manager.
- **One apex for the issuers, the site, docs and mail.** It couples permanent issuers to the brand zone's third-party
  hosts, scoped tokens and HSTS posture. Rejected.
- **A `.io` apex.** `.io` is a ccTLD, and a registry can retire one. Rejected.
- **A third, non-production domain.** The platform domain already separates the brand, and token isolation covers lab
  and CI. Rejected.
- **A Public Suffix List entry for `ncodeid.net`.** Host-only `__Host-` cookies cover the risk. Not taken.
- **Chosen:** customer environments as tenants on production, every issuer under `ncodeid.net`, and the brand on
  `ncodeid.com`.

## Consequences

- An issuer template reads `https://{tenantId}.auth.ncodeid.net`, and the root tenant's issuer is
  `https://auth.ncodeid.net`.
- Customers' load tests share production hardware, so per-tenant rate limits and quotas apply.
- The lab differs from production (OKE Basic, arm64, in-cluster CloudNativePG), so each release is verified on the
  `canary` tenant before customers see it.
- Each function gets one wildcard certificate.
- A tenant can move to a dedicated deployment by DNS change without changing its issuer
  ([ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)).
- The bootstrap stack creates four zones, each with DNSSEC, a scoped token and its own records.
- A change to the site, the docs or the mail records touches only the brand zone, never an issuer.
- `platform@ncodeid.com` must exist as an Email Routing route, because the owner tag of every resource names it
  ([ADR-1135](1135-platform-resources-carry-one-stable-tag-set-and-provenance-comes-from-attestations-and-audit-logs.md)).
- No component or stack carries a default for the platform's domains
  ([ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md)); each environment
  supplies them as configuration, and the Kubernetes label prefix is `ncodeid.net/`.

## References

- [ADR-1165](1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md)
- [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)
- [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)
- [`docs/hosting-analysis.md`](../hosting-analysis.md), [`docs/platform-bootstrap.md`](../platform-bootstrap.md)
- [RFC 8705 section 5](https://www.rfc-editor.org/rfc/rfc8705#section-5), mutual-TLS endpoint aliases
- [ccNSO retirement of ccTLDs policy](https://icannwiki.org/CcNSO_Policy_Development_Process_-_Retirement)
- [Public Suffix List guidelines](https://github.com/publicsuffix/list/wiki/Guidelines)
