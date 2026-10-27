# 138. The protocol endpoints are served under the tenant route, and an issuer is its tenant's base address

- **Status:** Accepted (extended by [ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md):
  a tenant written through the management API is held to the same tenant route; decision 3's request-derived base
  address is removed by [ADR-0165](0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md):
  every served tenant has an issuer, which is its base address)
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

Every tenant-resolution strategy declares a **tenant route**: empty for a single tenant served at the root, a literal
path such as `/identity`, or a pattern carrying the tenant's identity such as `/{tenantId}`. The route has two jobs. It
is where the tenant's protocol endpoints are served, and it is part of the tenant's base address — the URL every
published endpoint, and the issuer itself when none is configured, is built from.

Only the second job was being done. When the protocol endpoints moved to declarative route groups
([ADR-0042](0042-management-api-route-driven-tenant-scope.md)), their group was mapped with an empty prefix, so the
endpoints sat at absolute routes (`/oauth2/token`) whatever the configuration said, while the base address still
appended the tenant route. A configured path therefore produced an issuer (`https://op.example/identity`) beneath which
nothing was served, and OpenID Connect Discovery §4.3 requires the discovery document to be fetched from beneath the
issuer. The path strategy could not work at all: with no `{tenantId}` in the matched route there was nothing to resolve
the tenant from.

Three smaller defects sat on the same path:

- A tenant with a configured issuer had the tenant route appended to it again, so a path tenant whose issuer is
  `https://op.example/acme` was given the base address `https://op.example/acme/acme`.
- Binding the tenant route handed the request's live route values to the template binder, which consumes the values it
  binds and writes the rest as a query string. It stripped the tenant parameter from the request — so links generated
  later in the request could no longer carry it — and could leak an endpoint's own parameters into the base address.
- A configured issuer's default port was made explicit, so endpoints were published as `https://op.example:443/…`
  beside an issuer of `https://op.example`.

## Decision

**The tenant route is the prefix of the OpenID protocol endpoint group, and a tenant's issuer, where it has one, is its
base address exactly.**

1. **Endpoint groups resolve their prefix when routes are mapped.** `IEndpointGroup` exposes
   `GetPrefix(IServiceProvider)` rather than a fixed string, because a group is registered before the container exists
   while its prefix may be configuration. The OpenID protocol group returns `ITenantResolver.GetTenantRoute()`, so its
   endpoints are served at, for example, `/{tenantId}/oauth2/token` and `/{tenantId}/.well-known/openid-configuration`.
2. **A configured issuer is the base address as-is.** The issuer is the tenant's identity and names where the tenant is
   served, tenant route included, so nothing is appended to it. A static tenant's issuer must therefore end with the
   tenant path, which is validated when the host starts
   ([ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)); a path base the host is mounted under
   may precede it.
3. **The request-derived fallback binds only the tenant route's parameters, from a copy.** The base address of a tenant
   without an issuer is the request's origin, its path base, and the tenant route bound from the request's values for
   that route's own parameters. The request's route values are left untouched.
4. **Published links carry the tenant route's values.** Absolute URLs are generated with the request's route values as
   ambient values, so `{tenantId}` carries over into every endpoint URL. Ambient values only fill parameters of the
   target route; they never become query parameters.
5. **The authority omits the default port**, so published URLs keep the issuer's own form.

## Options considered

- **Drop the tenant route and keep absolute endpoint routes.** Rejected: the path strategy cannot identify a tenant
  without it, and a configured static path would still publish an issuer nothing is served under.
- **Keep `Prefix` as a string and add a second, service-aware member.** Rejected: two members describing one prefix
  invite them to disagree; the prefix is resolved at map time for every group.
- **Append the tenant route to a configured issuer.** Rejected: the issuer is the complete identity. Appending makes a
  path tenant's issuer and base address disagree, and makes the issuer depend on the request.
- **Supply the tenant's route values explicitly at each link site.** Rejected: every site would need to know the
  tenant route; ambient values are the routing system's own mechanism for carrying a route prefix's values.

## Consequences

- A configured tenant path is honored: the endpoints, the discovery document, and the issuer all agree on it, and a
  static issuer that does not end with the path stops the host instead of publishing an unusable discovery document.
- The path strategy serves its protocol endpoints under `/{tenantId}`. The interactive login and consent surface is
  served under the same route, and its session cookie is scoped to the tenant's path
  ([ADR-0193](0193-the-interactive-surface-is-served-under-the-tenant-route.md)). A tenant written through the
  management API is held to the same rule: its issuer must end with its tenant route
  ([ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)).
- Management endpoints keep their fixed `/api` prefix; they carry their own `{tenantId}` segment and do not resolve a
  protocol tenant ([ADR-0042](0042-management-api-route-driven-tenant-scope.md)).

## References

- [ADR-0017](0017-tenant-resolution-shared-abstraction-and-management-boundary.md) — tenant selection versus
  materialization.
- [ADR-0129](0129-the-tenant-issuer-is-the-single-authoritative-origin.md) — the issuer as the single authoritative
  origin.
- Code: `NCode.Registration.AspNetCore.IEndpointGroup.GetPrefix`,
  `NCode.Identity.OpenId.Authentication.Endpoints.OpenIdEndpointGroup`,
  `NCode.Identity.OpenId.Authentication.Endpoints.OpenIdLinkGeneratorExtensions`,
  `NCode.Identity.OpenId.Tenants.DefaultOpenIdTenantFactory.GetTenantBaseAddressAsync`.
