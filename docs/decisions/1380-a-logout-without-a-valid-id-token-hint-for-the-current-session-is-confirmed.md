# 1380. A logout without a valid `id_token_hint` for the current session is confirmed by the end-user

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group
- **Amends:** [ADR-1074](1074-session-management-and-logout.md), [ADR-1096](1096-end-session-subject-from-server-state.md)

## Context

The `end_session_endpoint` ended the end-user's session for any request that reached it, so a cross-site link or form
signed the end-user out (logout CSRF). OpenID Connect RP-Initiated Logout 1.0 section 2 says "the OP MUST ask the
End-User this question if an id_token_hint was not provided or if the supplied ID Token does not belong to the current
OP session with the RP and/or currently logged in End-User"
([#494](https://github.com/NCodeGroup/Identity/issues/494)).

## Decision

1. **The prompt rule.** The endpoint asks the end-user to confirm when an OP login is present
   (`HttpContext.AuthenticateAsync()` succeeds) and the request has no valid `id_token_hint` whose `sid` equals the
   login's `sid` (ordinal). A valid hint naming the current session logs out unprompted; with no OP login there is
   nothing in the browser to end, so there is no prompt.

2. **The login's session wins.** When a login is present, its `sid` takes precedence over the hint's `sid`, so a
   confirmed logout ends the session the end-user is signed in to; the hint's `sid` identifies the session only when no
   login is present (amends [ADR-1096](1096-end-session-subject-from-server-state.md)).

3. **The subject carries the answer.** `EndSessionSubject` gains `bool RequiresConfirmation { get; init; }`
   (non-positional, so existing constructions compile), computed by `DefaultResolveEndSessionSubjectHandler`.
   `EndSessionCommand` takes the resolved `EndSessionSubject` as a third positional parameter, so the endpoint
   validates the hint once; callers send `ResolveEndSessionSubjectCommand` first. The first-party
   `POST /account/logout` stays unprompted: its antiforgery-protected form is the end-user's own confirmation.

4. **The request is parked as a one-time continuation.** The request is parked with `IContinueService` under the new
   continue code `OpenIdConstants.ContinueCodes.EndSession` (`"continue_end_session"`) for 10 minutes, with
   `clientId: null` (the request's `client_id` is unauthenticated). The payload is
   `EndSessionContinuation(IdTokenHint, ClientId, PostLogoutRedirectUri, State, SessionId)`: the four parameters the
   pipeline reads plus the session it was asked for. Plain strings avoid message JSON polymorphism.

5. **A resumed logout is bound to the session.** Resuming (`DefaultEndSessionContinueProvider`) re-resolves the subject
   and refuses with a 400 `invalid_request` when its `SessionId` differs from the parked one, so a continuation
   obtained in one browser cannot log out another. Refusing (cancel) returns `ReadOnlyEndpointDisposition.Handled()`
   with no result: the continuation is spent and nothing is answered to the relying party.

6. **The core asks through a mediator command.** The new `EndSessionConfirmationChallengeCommand(OpenIdContext,
   IEndSessionRequest Request, string ContinueUrl)` returns `ReadOnlyEndpointDisposition`, mirroring
   `ConsentChallengeCommand`. The core default handler renders a minimal page whose form posts to the one-time continue
   URL (the URL's unguessable `state` is the CSRF secret), so every OP asks; its CSP opens `form-action`
   (`AllowsAnyFormAction = true`) because the resumed logout may redirect to the client. A handler that returns
   `UnHandled()` opts out: the endpoint then logs out unprompted.

7. **The interactive surface replaces that handler.** It redirects to `/account/logout/confirm?ReturnUrl=<continue URL>`.
   `GET` renders the no-JS form (stable ids `logout-confirm`, `logout-cancel`; the SPA renders the same contract);
   `POST` with `action=logout` redirects to the continue URL, and any other action refuses the continuation through
   `IContinueRefusalService` and redirects to the account page, leaving the session.

8. **A logout that saw no login clears nothing.** A logout whose subject names no `SubjectId` and no `SessionId` saw no
   login, so `DefaultEndSessionHandler` then skips `SignOutAsync` and `ClearBrowserState`: a cross-site POST, which the
   SameSite=Lax login cookie does not accompany, can no longer clear the login through the response's `Set-Cookie`.

## Alternatives considered

- **Antiforgery on the endpoint itself.** Rejected: relying parties send plain GET and POST requests.
- **Refusing a logout without a hint.** Rejected: it leaves stale OP sessions.
- **A "confirmed" flag posted back to the endpoint.** Rejected: it is forgeable.
- **No core page.** Rejected: an OP without the interactive surface would never ask.

## Consequences

- A logout from a relying party without a valid hint for the current session takes one extra click.
- The endpoint writes one continue grant per prompt.
- `EndSessionCommand` callers resolve the subject first.

## References

- [ADR-1074](1074-session-management-and-logout.md), [ADR-1096](1096-end-session-subject-from-server-state.md)
- Issue [#494](https://github.com/NCodeGroup/Identity/issues/494)
