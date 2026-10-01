# 30. UserInfo is backed by a reusable subject-authentication seam; claims come from enrichers

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

<see href="https://openid.net/specs/openid-connect-core-1_0.html#UserInfo">OpenID Connect Core 5.3</see> defines a
UserInfo endpoint: a protected resource that returns claims about the end-user identified by an access token the caller
presents. Two questions fall out of adding it to this library:

1. **How is the caller authenticated?** UserInfo is the first _resource_ endpoint — unlike the token/authorization/
   revocation/introspection endpoints, it is not authenticated by client credentials but by the subject's own
   presented credentials (typically a bearer access token). The library already authenticates the _interactive_
   end-user at the authorization endpoint via `httpContext.AuthenticateAsync(scheme)` (an ASP.NET Core authentication
   scheme), and the management API likewise relies on the host's configured scheme plus `httpContext.User`.
2. **Where do the returned claims come from?** This library has no persistent per-user claims store — subject claims
   live only on the host-provided login ticket (`SubjectAuthentication`) captured at authentication time. So the set of
   claims a UserInfo response can carry is a host/application concern, not a library one.

## Decision

**The UserInfo endpoint (`/oauth2/userinfo`, GET and POST) is a thin shell over two mediator seams: a reusable
_subject-authentication_ command that resolves the caller, and a _claims-enricher_ command that the host populates.**

- **Authentication — a reusable seam, not UserInfo-specific.** A general `AuthenticateSubjectCommand`
  (`ICommand<AuthenticateSubjectDisposition>`) in the root `Subject` capability authenticates the subject of the
  _current request_. Its default handler calls `httpContext.AuthenticateAsync()` against the host's configured
  authentication scheme — **the same mechanism the management API uses** — and maps the ticket to the existing
  `SubjectAuthentication` type. Applications replace the handler (or configure the scheme — e.g. JWT bearer) to change
  how credentials are validated, exactly as off-the-shelf password-grant support requires overriding
  `DefaultAuthenticatePasswordGrantHandler` ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)). The
  endpoint maps the disposition to HTTP: no subject → `401 invalid_token`; a failed authentication → its error; a
  resolved subject → the claims response.
- **Claims — an enricher pipeline.** A `GetUserInfoClaimsCommand` carries the resolved `SubjectAuthentication` and the
  mutable claims dictionary. The default handler contributes **only the required `sub`** claim; applications register
  additional `ICommandHandler<GetUserInfoClaimsCommand>` handlers to add profile, email, and other claims from their own
  user store (typically gated by the granted scopes) — mirroring the token-claims enrichers
  (`GetAccessTokenSubjectClaimsCommand` / `GetIdTokenSubjectClaimsCommand`). The response is an extensible
  `[JsonExtensionData]` dictionary (`UserInfoResult`), the same shape the discovery and introspection endpoints use. The
  endpoint auto-advertises as `userinfo_endpoint` in discovery.

Two naming consequences of introducing a second subject-authentication path were folded into this change:

- The **Authorization slice drops the redundant `Subject` noun** where it would collide with the general seam: its
  pipeline commands are `AuthenticateCommand` / `AuthorizeCommand` / `ChallengeCommand` (the namespace already supplies
  the "subject" context). The root `Subject` capability keeps the noun because its types are referenced cross-slice
  (`SubjectAuthentication`, `AuthenticateSubjectCommand`, `AuthenticateSubjectDisposition`).
- `ValidateSubjectCommand` became **`ValidateSubjectAuthenticationCommand`**: it is _not_ part of the generic
  authenticate pipeline — it validates an existing login-session `SubjectAuthentication` against an OpenID request's
  requirements (tenant, `max_age`, `acr`/IdP), and is shared only by the authorization and token-issuance flows. A
  bearer-authenticated resource request (UserInfo) does not use it.

## Options considered

- **Validate the bearer JWT inside the endpoint (self-contained).** Rejected: it reinvents what ASP.NET Core
  authentication schemes already provide, duplicates the `SubjectAuthentication` model, and diverges from how the
  management API and authorization endpoint authenticate. The mediator seam lets the host plug in bearer, DPoP, mTLS, or
  anything else without touching the endpoint.
- **A bespoke `IAccessTokenAuthenticationService` + handler strategy collection.** Rejected: it duplicated the existing
  mediator "authenticate → disposition" pattern and introduced a parallel `AuthenticatedAccessToken` model that
  overlapped `SubjectAuthentication`. Reusing the mediator seam keeps one way to authenticate a subject.
- **Return a fixed, strongly-typed UserInfo DTO.** Rejected in favor of the `[JsonExtensionData]` dictionary: the claim
  set is host-defined and scope-gated, so a dictionary keeps the response open for enrichment without a public-API
  change per claim.
- **Source claims from the access token the caller presents.** Deferred: once the host authenticates the subject, the
  resulting `SubjectAuthentication` principal is the claims source; projecting additional claims from the access token
  or a user store is an additive enricher, not a change to this contract.

## Consequences

- A host that configures an authentication scheme (for example, JWT bearer validating this server's access tokens) gets
  a working UserInfo endpoint that returns at least `sub`; richer claims arrive by registering enricher handlers. With no
  scheme configured, every request is anonymous and the endpoint returns `401` (and `httpContext.AuthenticateAsync()`
  requires _some_ default scheme to be registered — the reference `Playground` host does not yet configure one).
- The subject-authentication seam is reusable: any future resource endpoint authenticates the caller by dispatching the
  same `AuthenticateSubjectCommand`, rather than re-implementing credential handling.
- The UserInfo response is extensible and consistent with discovery/introspection; new claims are additive handler
  registrations, not endpoint or response-type changes.
- The authorization-slice rename is a breaking change to those public command/handler type names, acceptable under the
  pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the mediator command/handler + overridable-default
  shape this endpoint follows.
- [ADR-0028](0028-token-revocation-refresh-tokens-only.md) / [ADR-0029](0029-token-introspection-endpoint.md) — the
  sibling client-facing endpoints whose thin-shell + extensible-result conventions UserInfo reuses.
