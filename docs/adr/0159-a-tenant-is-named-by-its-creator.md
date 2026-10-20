# 159. A tenant is named by its creator

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

Resource identifiers are server-generated and opaque ([ADR-0014](0014-server-generated-opaque-resource-ids.md)): an
identifier is a meaningless anchor for foreign keys and audit records, and a caller never chooses one. For most resources
that is right. A tenant's identifier is different, because it is not internal:

- **It is part of the tenant's issuer.** Under path-based tenancy the identifier is the tenant route's segment, and a
  configured issuer is the tenant's base address as-is
  ([ADR-0138](0138-protocol-endpoints-are-served-under-the-tenant-route.md)), so tenant `acme` is served beneath, and must
  publish, an issuer ending in `/acme`. The issuer is asserted in every token the tenant mints and configured by every
  relying party that trusts it, so it is effectively immutable once clients integrate
  ([ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md)).
- **It is a name people use.** The management API addresses a tenant by it (`/api/tenants/{tenantId}/…`,
  [ADR-0042](0042-management-api-route-driven-tenant-scope.md)), and the hosted platform names a tenant by the leftmost
  label of its host (`acme-dev.auth.<apex>`, [ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)).
  The configured root and default tenants already carry readable identifiers (`root`, `default`).

A server-minted identifier exists only inside the create call, so a caller cannot know the issuer a path tenant must have
until the tenant exists. Validating a tenant's issuer against its route
([ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)) would then make it impossible to
create a path tenant with an issuer in one request.

Auth0 names a tenant by its creator: lowercase letters, digits, and hyphens, unique, and impossible to rename. The name
addresses the tenant in the management dashboard (`manage.auth0.com/dashboard/{region}/{tenant}/`) and forms its canonical
domain (`{tenant}.{region}.auth0.com`); a separate friendly name is for display only.

## Decision

**A tenant's identifier is a name its creator chooses, and it never changes.**

1. **The creator chooses it.** `POST /api/tenants` carries the `tenantId`; the server mints none.
2. **It is a lowercase host label.** 1 to 63 lowercase letters, digits, or hyphens that starts and ends with a letter or
   digit (RFC 1123). That one form is valid both as a URL path segment and as the leftmost label of a hostname, so a
   tenant's name works under every built-in strategy, and the lowercase form is the only spelling of an identity the
   store compares without regard to case.
3. **It is immutable.** No update path changes it: the management API addresses the tenant by it, and
   `UpdateTenantRequest` does not carry it. The editable, human-friendly label is the tenant's `DisplayName`.
4. **It is validated wherever a tenant identifier enters.** The management API judges it through
   `ITenantResolver.ValidateTenantAsync` ([ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)),
   and the configured identifiers (`TenantResolution:RootTenantId`, `TenantResolution:StaticSingle:TenantId`) are
   validated when the host starts ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)).
5. **Only tenants.** Servers, clients, resource servers, secrets, and principals keep ADR-0014's server-generated, opaque
   identifiers.

## Options considered

- **Keep opaque identifiers and derive a path tenant's issuer from a configured base address** (the Microsoft Entra
  `/{tenant-guid}/v2.0` shape). Rejected as the model: issuers become unreadable, operators cannot address a tenant by a
  name they know, and the hosted platform's tenant hostnames are names anyway. Deriving an issuer from a template remains
  worthwhile on top of names (backlog `F71`).
- **Create without an issuer, then set it once the identifier is known.** Rejected: the tenant exists for a while with no
  address, and every client of the API needs two calls for one intent.
- **An opaque identifier plus a separate, unique, caller-chosen slug that path tenants are routed by** (the Keycloak realm
  shape). Rejected: a second identity to keep unique and in step with the first, a store lookup on every path-routed
  request (losing the store-free cache probe of [ADR-0037](0037-tenant-cache-probe-before-store-load.md)), and the opaque
  identifier buys a tenant nothing — its issuer is already an immutable public identity.
- **A wider character set or mixed case.** Rejected: not valid as a hostname label, and with case-insensitive store
  identity two spellings would name one tenant.
- **Reserve names such as `api` under the path strategy.** Not needed: routing matches whole paths, so a tenant named
  `api` collides only with an endpoint registered at exactly one of its protocol paths, not with the management API's
  `/api/tenants/…`. A closed reserved-word list would also have to be maintained forever.

## Consequences

- The issuer of a path tenant is known before the tenant exists (`https://op.example/acme`), so a tenant is created with
  its address in one request and validated as a whole.
- Tenant identifiers read the same in the management API, in the issuer, and in a hostname; `Location` headers and audit
  records name tenants by the name their operators chose.
- A tenant cannot be renamed. A tenant that needs a different name is recreated, just as its issuer — which carries the
  name under path tenancy — could not change without breaking its relying parties.
- A deleted tenant's name, and its issuer, can be given to a new tenant, which relying parties that trusted the old one
  would then trust. Never reissuing a retired name or issuer is tracked as backlog `F70`.
- The admin console asks for the identifier when a tenant is created and locks it when one is edited.

## References

- [ADR-0014](0014-server-generated-opaque-resource-ids.md) — opaque identifiers, amended by this ADR for tenants.
- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer as the tenant's identity.
- [ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md) — tenant hostnames.
- [ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) — where the name is validated.
- Code: `NCode.Identity.OpenId.Management.Contracts.Tenants.CreateTenantRequest.TenantId`,
  `NCode.Identity.OpenId.Tenants.TenantAddressRules.IsValidTenantId`.
