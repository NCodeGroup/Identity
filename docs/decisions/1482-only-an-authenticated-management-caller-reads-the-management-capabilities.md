# 1482. Only an authenticated management caller reads the management capabilities

- **Status:** Accepted (amends [ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md), [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md))
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

`GET /api/capabilities` returns the management capabilities document
([ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md)), and every management route declares
each problem it answers
([ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md)). The forces:

- **The document describes the deployment.** It lists the deployment's secret types, algorithms, curves and registered
  key management services. A caller that reads it learns which key services the deployment runs and which algorithms
  and key sizes it accepts.
- **Its clients are management clients.** The administration console and the tools that build key forms read it after
  they sign in, so every consumer already holds a management access token.
- **OpenID discovery is public for a reason that does not apply here.** Relying parties read the discovery document
  before they hold a token, because they need it to find the token endpoint. No client needs the capabilities document
  before it holds a management access token.

## Decision

**Any principal the management authentication filter authenticated reads the capabilities, with no role, and anyone
else is answered with an authentication-required problem.**

1. **Authentication is the whole requirement.** The handler answers a request whose principal is authenticated. It asks
   for no role, because tenant administrators and team administrators drive key forms from the document too.
2. **An unauthenticated request gets the authentication-required problem.** The handler returns
   `ManagementProblems.AuthenticationRequired()`, the same `401` problem document every other management route
   answers when the caller carries no valid access token.
3. **The route declares `401` and no `403`.** The route has no authority check, so it cannot answer `403`. The host walk
   in `ManagementRouteProblemDeclarationTests` names `api/capabilities/get` as the one authenticated-caller route: it
   declares `401` and not `403`.

## Options considered

- **Leave the document public.** Rejected: an anonymous caller could survey the deployment's key management services,
  algorithms and secret types, which is reconnaissance, and no anonymous consumer needs the document.
- **Require the platform `Read` authority.** Rejected: tenant administrators and team administrators drive key forms
  from the document and do not hold a platform authority, so the requirement would lock out the callers the document
  serves.

## Consequences

- An anonymous request to `GET /api/capabilities` is answered with a `401` problem document.
- A management client with any valid management access token reads the document unchanged.
- The route's OpenAPI document lists `401` and no `403`, and the host walk keeps that declaration true.

## References

- [ADR-1067](1067-secret-type-capability-catalog-and-capabilities-discovery.md): the capabilities document.
- [ADR-1480](1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md): a route
  declares every problem it answers.
- [#707](https://github.com/NCodeGroup/Identity/issues/707)
