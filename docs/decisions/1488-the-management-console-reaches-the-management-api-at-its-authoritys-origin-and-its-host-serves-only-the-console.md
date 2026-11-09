# 1488. The management console reaches the management API at its authority's origin, and its host serves only the console

- **Status:** Accepted (amends [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md), [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md), [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md))
- **Refines:** [ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md)
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The management console calls the management API with a DPoP-bound access token. The BFF signs the proof's `htu` over
the URL it sends the request to, which is `ManagementConsole:ManagementApiUrl` plus the path.

The OP compares a proof's `htu` only with its tenant's published URLs
([ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md)).
`TenantRequestUri` builds the URL it compares from the control-plane tenant's issuer scheme and host plus the request's
path, and the management API's route prefix is `/api` at the server's root (`ManagementRootGroup.RoutePrefix`). So the
only URL the OP accepts from the console is `<origin of the authority>/api/...`.

A console host that routes `/api` to the OP makes every DPoP-bound call fail: the console signs `htu` over the console
host, and the tenant does not publish that host. The browser also sends the console's `__Host-Http-bff.mgmt` cookie,
whose path is `/`, with every request to that host, so a path routed to the OP hands the console's session handle to
another process and shares the console's origin with an OP surface, which
[ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md) isolates.

## Decision

**The console calls the management API at `/api` on the origin of its authority, and the console's host routes every
path to the console.**

1. **The chart derives the API URL.** The host chart sets `ManagementConsole__ManagementApiUrl` to the origin of the
   console's authority (`https://` and its host, and its port when one is given) followed by `/api`. The chart has no
   `managementConsole.managementApiUrl` value, because the only URL the OP accepts is the derived one.
2. **The console's host serves only the console.** The chart's Ingress for `managementConsole.host` routes every path to
   the console's Service, and `allowedHosts` need not list the console's host.
3. **Machine callers use the server's own host.** A caller that is not the console reaches the management API at
   `/api` on the origin of the control-plane tenant's issuer, which the server's Ingress serves.
4. **The hosted platform follows.** The management API is at `auth.<apex>/api`, the root tenant's issuer host, and
   `manage.<apex>` serves only the console.

## Options considered

- **Keep `/api` on the console's host.** Rejected for the reasons in Context: every DPoP-bound call fails there, a
  route only unbound bearer tokens can use invites the weaker token, and the console's session cookie reaches another
  process.
- **A second published origin in the OP.** Rejected:
  [ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md) rule 3
  compares `htu` only with the tenant's own published URLs.
- **A `managementConsole.managementApiUrl` value.** Rejected: the only URL the OP accepts is derived, so an override can
  only make every DPoP-bound call fail.

## Consequences

- A default install's console calls succeed.
- The console's pods reach the server at its public origin, as they already do for discovery, pushed authorization
  requests and the token endpoint.
- `allowedHosts` lists only the server's own hosts.
- The console keeps an explicit `ManagementConsole:ManagementApiUrl`; only the chart derives it.

## References

- [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)
- [ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md)
- [ADR-1437](1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md)
- [ADR-1458](1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md)
- `charts/ncode-identity-host`, `.github/workflows/host-chart.yml`
- Issue [#720](https://github.com/NCodeGroup/Identity/issues/720)
