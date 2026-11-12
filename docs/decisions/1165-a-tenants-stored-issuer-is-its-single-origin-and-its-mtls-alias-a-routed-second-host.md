# 1165. A tenant's stored issuer is its single origin, and its mTLS alias a routed second host

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md)

## Context

An OpenID Provider publishes absolute URLs: the discovery endpoints, `jwks_uri`, `registration_client_uri`,
`verification_uri`, `check_session_iframe` and the continue URL. A client trusts them as the provider's own, and the
`iss` claim of every token must equal the issuer that discovery publishes. An origin that follows the request's `Host`
header lets a caller steer those URLs, and two tenants that share an issuer issue tokens that cannot be told apart.

A tenant is reached by a path (`/{tenantId}/oauth2/token`), by a host, or as the only tenant of a deployment, and its
strategy decides which. Mutual-TLS clients ([RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §5) reach the token,
revocation and userinfo endpoints on a second host, because a deployment often terminates client certificates on a
separate listener. That host is routed like the first, so it needs the same care: a stored value, validated, held by one
tenant.

The forces:

- The issuer must come from stored, validated data, as
  [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) requires.
- Every tenant a strategy serves needs an issuer, and a tenant written without one needs a default its strategy can
  derive.
- A template change must not move a tenant that already exists.
- The route of the protocol endpoints must follow the tenant without leaking route values into query strings.
- Vocabularies stay open ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a sibling package adds a
  strategy by registering it.

## Decision

A tenant's issuer is its single authoritative origin, and its mTLS endpoint alias is a second routed host. Both are
stored on the tenant.

### The issuer

1. `Issuer` is a first-class property of the tenant, not a setting. Every absolute URL published for the tenant is built
   from the base address parsed from it. The request contributes nothing.
2. Every tenant a strategy serves has a stored issuer. The tenant factory takes the issuer and the base address from it
   alone on every path and fails closed for a stored tenant that has none. The static strategy requires
   `TenantResolution:StaticSingle:Issuer`; the root tenant it does not serve needs none.
3. An issuer is the base address exactly: nothing is appended. A static issuer must end with the tenant path, which a
   startup check enforces, and a mounting path base may precede it. The authority omits the default port.
4. The tenant route is the protocol group's prefix. `IEndpointGroup.GetPrefix(IServiceProvider)` resolves at map time,
   and `OpenIdEndpointGroup` returns `ITenantResolver.GetTenantRoute()`, for example `/{tenantId}/oauth2/token`. Links
   take the request's route values as ambient values, which never become query parameters.
5. `ITenantStrategy.GetDefaultIssuerAsync`, exposed by `ITenantResolver`, gives a tenant written without an issuer its
   default. The answer is `TenantResolution:Root:Issuer` for the root tenant, `StaticSingle:Issuer` for the static
   strategy, and otherwise `DynamicByHost:IssuerTemplate` or `DynamicByPath:IssuerTemplate`. A template is an RFC 6570
   level-1 template with one variable, `{tenantId}`, substituted without escaping.
6. Every write path, management create and update and `EnsureCreatedAsync`, applies `issuer ??= default` and then
   `ValidateTenantAsync`. Clearing the issuer restores the derived one. The derived issuer is stored, so a change to a
   template never moves a tenant.
7. Templates are validated at startup: `{tenantId}` appears exactly once, no other expression appears, and a probe
   expansion is a tenant the strategy serves. A host label is at most 63 characters and the host at most 253. The
   dynamic strategies require a root-tenant address that no other tenant may take.
8. The normalized issuer has a filtered UNIQUE index, because a shared issuer makes tokens indistinguishable. The host
   strategy routes on the hosts derived on write into `NormalizedIssuerHost` and `NormalizedMtlsEndpointAliasHost`.
   Those two columns are deliberately not unique, because path tenants share a host; under the host strategy their
   uniqueness is a validation rule.

### The mTLS endpoint alias

9. `PersistedTenant.MtlsEndpointAliasAuthority` is an optional `https` origin with no path. `OpenIdTenant` exposes it,
   and discovery publishes it as `mtls_endpoint_aliases`. The published `issuer` never changes with the request host.
10. `ITenantStore.GetOrDefaultByHostAsync` returns the one tenant whose issuer host or alias host matches, and nothing
    when several match, so an ambiguous host routes nowhere. Validation uses `ListTenantIdsByHostAsync`, which returns
    every holder.
11. The address is judged as two hosts. The alias must be an `https` origin, the host pattern must map its host back to
    the tenant, no other tenant may hold it, and it must differ from the tenant's own issuer host. An update judges it
    again when either value changes.
12. `ITenantStrategy.GetDefaultMtlsEndpointAliasAuthorityAsync` gives the default alias:
    `Root:MtlsEndpointAliasAuthority`, then `StaticSingle:MtlsEndpointAliasAuthority`, else
    `DynamicByHost:MtlsEndpointAliasTemplate`. A startup check requires `{tenantId}` in the template's host, that the
    host maps to itself, and that it differs from the issuer template's host. Writes apply `alias ??= default` and store
    it, and clearing restores it.

### The hosted platform

Changing a tenant's issuer changes the host the tenant answers on, as one operation. The hosted platform's templates are
`https://{tenantId}.auth.<apex>`, `https://{tenantId}.mtls.<apex>` and, for the root tenant, `https://auth.<apex>`,
which give the hostnames of
[ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md).
A custom domain is an explicit issuer. The frontend bootstrap `issuer` is the serving tenant's
([ADR-1182](1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md)).

## Options considered

- **A request-derived origin, even behind a development opt-in.** It lets a caller steer every published URL and the
  `iss` of every token, and an opt-in leaves the same path open wherever it is switched on. Rejected.
- **A writable domain column.** It adds a second field that must agree with the issuer. Rejected.
- **Deriving the issuer at request time.** A change to a template or a setting would move existing tenants, and the
  issuer a token carries would depend on the request. Rejected.
- **Appending the route to an issuer.** The issuer would not be the base address, and a static issuer could not be
  checked against the tenant path. Rejected.
- **A per-strategy base address.** Each strategy would parse its own, and the factory could not take one origin from the
  tenant alone. Rejected.
- **An mTLS alias setting.** Routing by host would scan the merged settings of every tenant. Rejected in favor of a
  stored tenant property with a normalized host column.
- **A shared alias host.** Two tenants on one host cannot be told apart by it. Rejected.
- **Throwing on an ambiguous host.** One misconfigured pair would fail requests for tenants that did nothing wrong.
  Rejected in favor of routing nowhere and reporting the conflict on the next write to either tenant.
- **A stored issuer and a stored alias, each defaulted by the strategy on write.** Chosen. The origin is data, the
  strategy supplies the default, and nothing is derived from the request.

## Consequences

- The tenant factory is the one place that builds a base address, and it cannot be given a request. A stored tenant
  without an issuer fails closed instead of serving URLs from a guess.
- A strategy's default is applied once, on write. Changing a template affects tenants created afterwards, and an
  existing tenant keeps the issuer it was stored with.
- The unique issuer index rejects two tenants with one issuer, and the host columns let the host strategy route with an
  index lookup. Two tenants that come to hold the same host serve no request on it until an operator fixes one of them,
  and the next write to either tenant reports the conflict.
- Moving a tenant to another host is a deliberate change of its issuer, which changes the `iss` of the tokens it issues
  ([ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md)).
- A strategy implements two defaults, the issuer and the alias, and the startup checks fail a template that a strategy
  cannot serve before the first request.

## References

- [RFC 8705 §5](https://www.rfc-editor.org/rfc/rfc8705#section-5) — mutual-TLS endpoint aliases.
- [RFC 6570](https://www.rfc-editor.org/rfc/rfc6570) — URI templates, level 1.
- [project-template ADR-0126](project-template/0126-published-urls-come-from-a-configured-origin.md) — published URLs
  come from a configured origin.
- [ADR-1097](1097-sender-constrained-access-tokens-dpop-and-mtls.md) — sender-constrained access tokens.
- [ADR-1160](1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md) — a tenant's
  address is validated by the strategy that serves it.
- [ADR-1166](1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) — the root tenant and the
  provisioning pipeline.
- [ADR-1182](1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md) — the frontend bootstrap
  issuer.
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md)
  — the hosted platform's domains.
