# 1495. The hosted platform's hosts live on a platform domain of their own, and a brand domain carries the site, docs and mail

- **Status:** Accepted (amends [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md), [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md), [ADR-1135](1135-one-resource-tagging-strategy-across-every-vendor.md))
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

A tenant's issuer is the single authoritative origin of its tokens, discovery document and endpoint aliases
([ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md)), so the hosted platform's issuer hosts are
permanent: a tenant's relying parties pin the issuer string, and
[ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md) derives every issuer from the platform's
apex.

A brand domain carries a different kind of host. Its subdomains point at third-party services (the project site and
docs host, the status page), its zone holds the mail records of the people who operate the platform, and a person
manages some of its integrations by hand. Each of those is a reason to change the zone, to hand a vendor a scoped
token or to relax a header, and none of them is a reason to touch an issuer.

A country-code TLD can also be retired: the ccNSO's retirement policy removes a ccTLD from the root five years after
ISO 3166-1 deletes its code, or up to ten years with a retirement plan. A permanent issuer cannot sit under a domain
whose registry can be withdrawn.

## Decision

**Every issuer-bearing host lives under a platform domain that carries nothing else, and a brand domain carries the
site, docs, status page and operator mail.**

1. **The platform domain is `ncodeid.net`.** It keeps the namespaces of
   [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md): `auth.`, `{tenant}.auth.`, `mtls.`,
   `{tenant}.mtls.`, `manage.`, lab `….lab.` and CI `{run}.ci.`. Its apex and `www.` redirect (301) to
   `https://ncodeid.com`, served by the platform's ingress. It is the only domain that ever carries an issuer.
2. **The brand domain is `ncodeid.com`.** It carries `ncodeid.com` and `www.` (the project site and docs, served by the
   hosting provider), `status.ncodeid.com` (UptimeRobot), the inbound Email Routing addresses (`security@`,
   `platform@`, `postmaster@`, `abuse@` and the per-vendor aliases) and the `security.txt` contact
   `security@ncodeid.com`.
3. **Two redirect-only domains guard the name.** `ncodeidentity.com` and `ncodeidentity.net` redirect their apex and
   `www.` (301) to `https://ncodeid.com`. They receive and send no mail.
4. **The platform names four domains.**

   | Domain               | Role                              | Hosts                                                                  |
   | -------------------- | --------------------------------- | ---------------------------------------------------------------------- |
   | `ncodeid.net`        | Platform domain                   | Every issuer, mTLS alias, console, lab and CI host; apex redirect       |
   | `ncodeid.com`        | Brand domain                      | Project site, docs, status page; operator mail                          |
   | `ncodeidentity.com`  | Redirect-only domain              | Apex and `www.` redirect to `https://ncodeid.com`                       |
   | `ncodeidentity.net`  | Redirect-only domain              | Apex and `www.` redirect to `https://ncodeid.com`                       |

5. **`ncodegroup.com` is not the platform's.** It is the NCode Group's own domain, and the platform's stacks and
   records never manage it.
6. **Each domain is its own Cloudflare zone.** All four are registered at Porkbun, and the bootstrap stack creates
   each zone with DNSSEC and its own scoped CI token. The redirect-only domains' redirects are Cloudflare Single
   Redirect rules on proxied apex and `www` records, and Universal SSL is on in those two zones, because a proxied
   host needs an edge certificate, so their CAA records list Cloudflare's certificate authorities; they carry no
   issuer. In every other zone Universal SSL is off and CAA restricts issuance to Let's Encrypt. The apex and `www.`
   of `ncodeid.net` are DNS-only records to the platform's ingress, which serves the 301 to `https://ncodeid.com` and
   the HSTS header with a cert-manager Let's Encrypt certificate for those two hosts (HTTP-01). Identity hosts stay
   DNS-only, never proxied.
7. **The platform zone holds only records the platform's OpenTofu roots write.** No hosting-provider or vendor
   integration is given a token for it, and lab and CI clusters hold no token that can edit any zone.
8. **Registration is long on the two domains that matter.** `ncodeid.net` and `ncodeid.com` are renewed to the
   registry's 10-year maximum and auto-renew. The redirect domains auto-renew yearly.
9. **Every domain is under a generic TLD.** The platform names no country-code TLD, because a registry can retire one.
10. **Mail is declared before any domain sends it.** No domain sends mail yet, so every domain publishes DMARC
    `v=DMARC1; p=reject`; every domain except `ncodeid.com` publishes SPF `v=spf1 -all` and a null MX (`0 .`), and
    `ncodeid.com` publishes the Email Routing records, whose single SPF record is the one Email Routing publishes.
    System mail (verification, password reset, email MFA) sends only from `mail.ncodeid.net`, with that provider's SPF
    include and DKIM keys. Operator addresses live on `ncodeid.com`.
11. **HSTS follows who serves the subdomains.** The platform domain is submitted to the HSTS preload list with
    `includeSubDomains` once every host serves HTTPS, and the ingress's apex redirect serves the preload header. The
    brand domain sends `includeSubDomains` and is not preloaded, because third-party hosts (the status page and the
    site host) serve its subdomains.
12. **The Public Suffix List does not list `ncodeid.net`.** Host-only `__Host-` cookies are the cookie control.
    Listing stays available and requires at least two years of registration remaining and a `_psl` TXT record.
13. **Lab and CI stay under the platform domain.** Token isolation between environments
    ([ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)) covers them, and a separate
    non-production domain is not needed.
14. **No component or stack carries a default for any of the platform's domains**
    ([ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)): each environment supplies them as
    configuration.
15. **The Kubernetes label prefix is `ncodeid.net/`** (`ncodeid.net/environment`, `ncodeid.net/role`), and the owner
    mailbox is `platform@ncodeid.com`
    ([ADR-1135](1135-one-resource-tagging-strategy-across-every-vendor.md)).

## Options considered

- **One shared apex for the issuers, the site, docs and mail.** Rejected: it couples permanent issuers to the brand
  zone's third-party hosts, scoped tokens and HSTS posture, so a change for the site becomes a risk to every tenant's
  issuer.
- **A `.io` apex.** Rejected: `.io` is a country-code TLD, and a ccTLD can be retired.
- **A third, non-production domain for lab and CI.** Rejected: the platform domain already separates the brand, and
  token isolation covers lab and CI.
- **A Public Suffix List entry for `ncodeid.net`.** Not taken: host-only `__Host-` cookies cover the risk, and listing
  needs two years of registration remaining and a `_psl` record. It stays available.

## Consequences

- An issuer template reads `https://{tenantId}.auth.ncodeid.net`, and the root tenant's issuer is
  `https://auth.ncodeid.net`.
- The bootstrap stack creates four zones, each with DNSSEC, a scoped token and its own records.
- `platform@ncodeid.com` must exist as an inbound Email Routing route, because the owner tag of every resource names
  it.
- A change to the site, the docs or the mail records touches only the brand zone, and never an issuer.
- A tenant's custom domain is a separate matter and is unaffected.

## References

- [ADR-1129](1129-the-tenant-issuer-is-the-single-authoritative-origin.md)
- [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)
- [ADR-1133](1133-we-ship-and-run-the-same-deployment-artifacts.md)
- [ADR-1135](1135-one-resource-tagging-strategy-across-every-vendor.md)
- [ADR-1258](1258-the-server-sends-strict-transport-security-outside-development.md)
- [`docs/hosting-analysis.md`](../hosting-analysis.md), [`docs/platform-bootstrap.md`](../platform-bootstrap.md)
- [ccNSO retirement of ccTLDs policy](https://icannwiki.org/CcNSO_Policy_Development_Process_-_Retirement)
- [Public Suffix List guidelines](https://github.com/publicsuffix/list/wiki/Guidelines)
