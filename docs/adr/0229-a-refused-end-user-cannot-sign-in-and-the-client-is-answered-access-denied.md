# 229. A refused end user cannot sign in, and the client is answered access_denied

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A blocked principal ([ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md))
and a disabled local account ([ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md)) were refused only
by handlers of `ValidateSubjectAuthenticationCommand`. The interactive sign-in applied neither, so a blocked principal
got a session. The authorization request challenged a refused subject again, which is a login loop for a host whose
sign-in is another scheme. The self-service pages served a cookie whose session had ended, a disabled account looked
like a wrong password, and a consent denial left the client unanswered (backlog `R98`).

## Decision

**The end user's standing is a command of its own, which every subject validation includes, which the interactive
sign-in sends before it creates a session, and whose refusal answers the client with `access_denied`.**

1. **The standing is its own command.** `ValidateSubjectStandingCommand(OpenIdContext, SubjectAuthentication,
   OperationDisposition<IOpenIdError>)` asks whether the end user may sign in or be issued tokens at all, whatever the
   client or the request. `DefaultValidateBlockedSubjectHandler` and `DefaultValidateLocalAccountSubjectHandler` handle
   it.
2. **Every subject validation includes the standing.** `DefaultValidateStandingOnSubjectAuthenticationHandler`
   forwards each `ValidateSubjectAuthenticationCommand` to `ValidateSubjectStandingCommand` with the same disposition
   when no error is recorded, so every grant and every CIBA approval keeps refusing, also for a grant that another
   package adds ([ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md)).
3. **The interactive sign-in validates the standing before it creates a session.** It sends the command after it
   resolves the principal, and a refusal creates neither a session nor a cookie.
4. **A refusal answers the parked authorization request.** When the `ReturnUrl` names a parked authorization request,
   the request is answered at the redirect URI of the client with the refusal, `access_denied` and the `state` of the
   request, through `IContinueRefusalService.RefuseAsync` and `IContinueProvider.RefuseAsync`, which spend the
   continuation named by the `state` of the URL. Otherwise the login page shows the error code `access_denied`: "This
   account cannot sign in. Contact your administrator." A consent denial answers the parked request with `access_denied`
   the same way, and it redirects to `/` only when the `ReturnUrl` names no parked request (OpenID Connect Core 1.0
   §3.1.2.6). The refusal seam is `IContinueRefusalService`, not a member of `IContinueService`, because
   `DefaultContinueService` is part of the constructor graph of the authorization provider.
5. **The authorization request classifies a refusal by the standing.** For a refused subject, `DefaultAuthorizeHandler`
   sends `ValidateSubjectStandingCommand`. A refused standing is answered with its error to the client for every
   `prompt`, and it is audited as an authorization denial. Any other refusal challenges, or answers `login_required` for
   `prompt=none`. A fresh authentication cannot cure a blocked or disabled end user (RFC 6749 §4.1.2.1, OpenID Connect
   Core 1.0 §3.1.2.6).
6. **A disabled account's credential is verified.** `LocalAccountStore.VerifyCredentialAsync` returns the account of a
   valid credential whether or not the account is enabled, so the standing check refuses a disabled account with its
   reason after a correct password. A store whose disabled state is a lockout against guessing, such as the ASP.NET Core
   Identity adapter, refuses a locked-out user without verifying the password. The password grant refuses a disabled
   account itself with `invalid_grant`.
7. **The self-service surface serves a cookie only while its session is active.** Every interactive endpoint
   authenticates through `AuthenticateSessionAsync`, which requires a `sid` claim and calls
   `ISessionService.ValidateAndTouchAsync`, and signs the cookie out otherwise.

## Options considered

- **Classify a refusal by its error code.** Rejected: every subject validation answers `access_denied`, the ones that a
  fresh authentication cures included.
- **Refuse only at the sign-in.** Rejected: a host whose sign-in is another scheme loops at the authorization request.
- **Validate the session in a cookie event.** Rejected: the OpenID context exists only inside the endpoint filter, and
  the cookie handler caches the first authentication of a request.
- **A member of `IContinueService`.** Rejected: `DefaultContinueService` is part of the constructor graph of the
  authorization provider, so the dependency is a cycle.
- **Answer a disabled account as invalid credentials.** Rejected: the end user receives no reason after a correct
  password, while a lockout keeps refusing without verifying.

## Consequences

- A blocked or disabled end user has no session, and the client receives `access_denied`; Auth0 answers a blocked user
  at the callback the same way.
- A consent denial answers the authorization request with `access_denied`.
- Each self-service request validates and touches its session.
- **Breaking:** `ValidateSubjectStandingCommand`, `IContinueProvider.RefuseAsync` and `IContinueRefusalService` are
  added, the two handlers handle the standing command, and `ILocalAccountStore.VerifyCredentialAsync` returns the
  disabled account of a valid credential.
- This implements backlog `R98`.

## References

- [ADR-0070](0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md) — the interactive surface.
- [ADR-0076](0076-consent-persistence-and-enforcement.md) — the consent surface, whose denial answers the authorization
  request.
- [ADR-0224](0224-deleting-or-disabling-an-account-ends-its-sessions.md) — the account check.
- [ADR-0225](0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) — every subject validation.
- [ADR-0226](0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md) — the
  blocked principal.
- [RFC 6749 §4.1.2.1](https://www.rfc-editor.org/rfc/rfc6749#section-4.1.2.1) — the error response of the authorization
  request.
- [OpenID Connect Core 1.0 §3.1.2.6](https://openid.net/specs/openid-connect-core-1_0.html#AuthError) — the error
  response of the authentication request, which an end user's denial of consent included.
- [Auth0: Block and Unblock Users](https://auth0.com/docs/manage-users/user-accounts/block-and-unblock-users) — a
  blocked user is answered at the callback.
- Code: `NCode.Identity.OpenId.Authentication.Subject.ValidateSubjectStandingCommand`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers.DefaultAuthorizeHandler`,
  `NCode.Identity.OpenId.Authentication.Interactive.InteractiveAuthenticationEndpointHandler`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Continue.DefaultContinueRefusalService`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores.LocalAccountStore`.
