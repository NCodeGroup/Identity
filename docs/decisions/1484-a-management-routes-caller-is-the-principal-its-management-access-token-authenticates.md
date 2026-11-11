# 1484. A management route's caller is the principal its management access token authenticates

- **Status:** Accepted (amends [ADR-1420](1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md), [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md), [ADR-1482](1482-only-an-authenticated-management-caller-reads-the-management-capabilities.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

`ManagementAuthenticationEndpointFilter` validates a self-issued management bearer token against the control-plane
tenant
([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md),
[ADR-1470](1470-a-callers-acting-client-is-resolved-once-per-request.md)). The forces:

- **Another scheme can set `HttpContext.User`.** A host can make an authentication scheme other than the management
  access token its default, and `UseAuthentication` then sets `HttpContext.User` to that scheme's principal before the
  filter runs. A session cookie is such a principal, and a browser also sends it on a cross-site navigation, so a page
  on another site can make a management request that carries it.
- **Authorization reads `HttpContext.User`.** `GlobalAdminHandler`, the ownership handlers, the audit recorder and the
  `401`/`403` split all read `HttpContext.User`. A foreign principal there acts on the management API.
- **The who-am-I routes describe a caller.** `GET /api/me`, `GET /api/me/teams` and `GET /api/me/tenants` report a
  caller's principal, platform authority and role assignments, and `GET /api/capabilities` answers only an
  authenticated caller
  ([ADR-1482](1482-only-an-authenticated-management-caller-reads-the-management-capabilities.md)). A principal from
  another scheme is not a management caller.
- **A protected resource answers a missing or invalid token `401`.** RFC 6750 §3 defines the response of a resource
  that receives a request without a valid access token.

## Decision

**The management authentication filter is the only source of a management route's caller: the principal a valid
management access token authenticates, or an anonymous principal.**

1. **The filter sets `HttpContext.User` on every management request.** It sets the principal a valid management access
   token authenticates, or `new ClaimsPrincipal(new ClaimsIdentity())` for any other request, whatever principal an
   authentication scheme set. Every authorizing route reads `HttpContext.User`, so none of them trusts a foreign
   principal.
2. **The filter publishes `IManagementCallerFeature` only for an authenticated caller.** The public feature in
   `NCode.Identity.OpenId.Management.Abstractions` carries the caller's `ClaimsPrincipal`, so a capability package's
   route reads it too. For every other request the feature is `null`.
3. **A route that only authenticates its caller reads the feature and fails closed.** `GET /api/capabilities`,
   `GET /api/me`, `GET /api/me/teams` and `GET /api/me/tenants` read `IManagementCallerFeature`, never
   `HttpContext.User.Identity.IsAuthenticated`, and answer `ManagementProblems.AuthenticationRequired()` when the
   feature is absent, also when the filter did not run. The who-am-I routes use the feature's `Caller` to resolve the
   principal and for the platform authorization.
4. **The four routes declare `401` and no `403`.** They have no authority check, so they cannot answer `403`.
   `ManagementRouteProblemDeclarationTests` names them as the authenticated-caller routes.
5. **An authenticated caller that resolves to no principal gets `200` with a null `principalId`.** The caller is
   authenticated, so the answer is a description of it; only an absent caller is a problem.

## Options considered

- **Answer an anonymous who-am-I request with an empty `200`.** Rejected: the answer describes nobody and hides a
  missing or expired token from a client that would otherwise sign in again.
- **Check only the feature in the authentication-only routes.** Rejected: the authorizing routes and the audit recorder
  read `HttpContext.User`, so they would trust a foreign principal.
- **Add an ASP.NET Core authentication scheme for the management API.** Rejected: a scheme's challenge and forbid can
  redirect to an interactive page, which a management API never answers.

## Consequences

- A session cookie or another host scheme's principal never acts on the management API.
- An anonymous request to `GET /api/me`, `GET /api/me/teams` or `GET /api/me/tenants` is answered with a `401` problem
  document, and the routes' OpenAPI documents list `401` and no `403`.
- A capability package's route reads the caller from `IManagementCallerFeature` and answers `401` without it.
- A management client with a valid management access token is the caller of every route it requests, and each route
  authorizes it from its role assignments.

## References

- [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md): the management API
  authenticates against the control-plane tenant.
- [ADR-1420](1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md): a caller's teams and
  tenants.
- [ADR-1470](1470-a-callers-acting-client-is-resolved-once-per-request.md): the acting client is resolved once per
  request.
- [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md): a route
  declares every problem it answers.
- [ADR-1482](1482-only-an-authenticated-management-caller-reads-the-management-capabilities.md): only an authenticated
  caller reads the capabilities.
- [#715](https://github.com/NCodeGroup/identity/issues/715)
- [#716](https://github.com/NCodeGroup/identity/issues/716)
