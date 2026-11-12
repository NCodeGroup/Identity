# 1380. Logout ends the session found in server state, confirmed when unsure, and notifies relying parties

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

An `end_session_endpoint` that ends the end user's session for any request that reaches it lets a cross-site link or
form sign the end user out (logout CSRF). OpenID Connect RP-Initiated Logout 1.0 section 2 says "the OP MUST ask the
End-User this question if an id_token_hint was not provided or if the supplied ID Token does not belong to the current
OP session with the RP and/or currently logged in End-User"
([#494](https://github.com/NCodeGroup/identity/issues/494)).

A pairwise subject ([ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md))
is a one-way, stateless projection of the stable `PrincipalId`, with no persisted mapping, the same property the grant
store relies on when it keeps a sensitive key only as a one-way hash and matches a presented value forward. The
`id_token_hint` of an RP-Initiated Logout request carries a `sub`, which for a pairwise client is a per-sector
pseudonym, not the stable `PrincipalId`. Two consumers of the resolved subject need the stable value:

- Server-side revocation keys grants by the `PrincipalId`, so a pseudonym matches no grant.
- Back-channel logout projects the `PrincipalId` per participating client, so each relying party's `logout_token`
  carries the `sub` it saw in its ID token, and a pseudonym projected a second time matches no relying party.

A stored, reversible map from pseudonym to principal would fix both and is a standing de-anonymization liability: a
store compromise re-links every pairwise `sub`. The logout pipeline has to find the session, the subject and the
participating relying parties in state the server owns, ask the end user when the request does not prove its own
legitimacy, end the session once, and tell each relying party over a channel the browser cannot break.

## Decision

Logout ends the session that server-owned state names, asks the end user to confirm when the request does not prove it
belongs to the current session, and sends each participating relying party a signed back-channel `logout_token`.

### Entry points

RP-Initiated Logout is the self-advertising `end_session_endpoint` (`EndpointNames.EndSession`). It accepts `GET` and
`POST`, as [ADR-1314](1314-protocol-endpoints-keep-the-http-methods-their-specifications-require.md) records. The
account page's antiforgery-protected `POST /account/logout` runs the same `EndSessionCommand` pipeline without a hint:
its form is the end user's confirmation.

### The subject comes from server state

The hint is validated through the hint policy of `IIssuedTokenValidator`
([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)), which skips the lifetime check and
may decrypt with the tenant's keys. The hint contributes only its `sid` and its client (`azp`/`aud`). Its `sub` is never
read, so a pairwise pseudonym is never reversed and no reverse map, table or entity exists.

`DefaultResolveEndSessionSubjectHandler` resolves the session and the subject:

- The session comes from the OP login when one is present (`HttpContext.AuthenticateAsync()` succeeds), and the login's
  `sid` wins over the hint's, so a confirmed logout ends the session the end user is signed in to. The hint's `sid`
  identifies the session only when no login is present.
- The stable subject (`PrincipalId`) and the participating clients come from the persisted session grant, matched
  forward by `sid`, or from the login cookie.
- A hint with no resolvable session leaves the subject unresolved. The OP declines to reverse a pseudonym rather than
  guess or revoke the wrong subject. The case is unreachable for tokens the OP issued, which always carry a `sid`.

### The prompt rule

`EndSessionSubject` has a positional `RequiresConfirmation`. It is true when an OP login is present and the request has
no valid hint whose `sid` ordinally equals the login's. A valid hint naming the current session ends it unprompted, and
with no OP login there is nothing in the browser to end, so there is no prompt. The member is positional, so a
replacement `ResolveEndSessionSubjectCommand` handler must state the answer to compile and cannot silently log out
unprompted. `EndSessionCommand` takes the resolved `EndSessionSubject`, so the endpoint validates the hint once and
callers send `ResolveEndSessionSubjectCommand` first.

### Parking and asking

- `ParkEndSessionRequestCommand(OpenIdContext, IEndSessionRequest Request, EndSessionSubject Subject)` parks the request
  and returns the continue URL, so a deployment changes the lifetime, payload or binding by replacing one handler
  ([ADR-1086](1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md)).
  Its default handler, `DefaultParkEndSessionRequestHandler`, parks
  `EndSessionContinuation(IdTokenHint, ClientId, PostLogoutRedirectUri, State, SessionId)` with `IContinueService` under
  `continue_end_session` (`OpenIdConstants.ContinueCodes.EndSession`) for 10 minutes, with `clientId: null` because the
  request's `client_id` is unauthenticated. The payload is plain strings, which avoids message JSON polymorphism.
- `EndSessionConfirmationChallengeCommand(OpenIdContext, IEndSessionRequest Request, string ContinueUrl)` asks the end
  user and returns `ReadOnlyEndpointDisposition`. The core default handler renders a minimal form that posts to the
  one-time continue URL, whose unguessable `state` is the CSRF secret, so every OP asks. Its content security policy
  sets `AllowsAnyFormAction` because the resumed logout may redirect to the client. A handler that returns `UnHandled()`
  opts out, and the logout then proceeds unprompted.
- The interactive surface substitutes that handler with `/account/logout/confirm`. `GET` renders the no-JS form with the
  stable ids `logout-confirm` and `logout-cancel`, and the SPA renders the same contract. `POST` with `action=logout`
  redirects to the continue URL. Any other action refuses the continuation through `IContinueRefusalService`, redirects
  to the account page and keeps the session.
- On resume, `DefaultEndSessionContinueProvider` resolves the subject again and refuses with `400 invalid_request` when
  its `SessionId` differs from the parked one, so a continuation obtained in one browser cannot end another browser's
  session. It logs the warning `EndSessionContinuationSessionMismatch`, without the session or the continuation.
  Refusing returns `ReadOnlyEndpointDisposition.Handled()` with no result: the continuation is spent and nothing is
  answered to the relying party.

### Ending the session

`DefaultEndSessionHandler` terminates a named session with `ISessionService.RevokeAsync` and the reason `logout`
([ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md)), and
dispatches a subject-wide termination when the logout names no session. A logout whose subject names no `SubjectId` and
no `SessionId` saw no login, so the handler skips `SignOutAsync` and `ClearBrowserState`: a cross-site `POST`, which the
`SameSite=Lax` login cookie does not accompany, cannot clear the login through the response's `Set-Cookie`.

The handler redirects with `302` and `state` to a `post_logout_redirect_uri` that ordinally matches one of the client's
registered `post_logout_redirect_uris` and passes the kind check
([ADR-1203](1203-redirect-uris-are-matched-exactly-as-written-and-their-kinds-are-narrowed-by-settings.md)). Otherwise
it renders a confirmation.

### Notifying relying parties

`IBackChannelLogoutNotifier` builds a signed `logout_token` for each participating relying party, with `typ`
`logout+jwt` and the claims `iss`, `aud`, `iat`, `jti`, `sid` and/or `sub`, and `events`. The `sub` is projected per
client ([ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md)). The
default notifier dispatches a `BackChannelLogoutDeliveryJob` through `IJobDispatcher`
([ADR-1242](1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md))
that delivers the token to the client's `backchannel_logout_uri`, so a failed delivery is retried out of band.

The notifier follows the tenant's discovery. It sends nothing while `backchannel_logout_supported` is false and omits
`sid` while `backchannel_logout_session_supported` is false. `LogoutChannelCoherenceRule`
([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md))
keeps a client's registration within what its tenant supports.

## Options considered

- A stateless `sid`, derived from the cookie alone. Rejected: nothing remains to look up a session's participating
  relying parties from or to revoke independently of the cookie, so it cannot support server-side revocation or
  back-channel fan-out.
- Antiforgery on the `end_session_endpoint` itself. Rejected: relying parties send plain `GET` and `POST` requests.
- Refusing a logout that has no hint. Rejected: it leaves stale OP sessions.
- A "confirmed" flag posted back to the endpoint. Rejected: it is forgeable.
- No core confirmation page. Rejected: an OP without the interactive surface would never ask.
- A stored, reversible pairwise map from pseudonym to principal. Rejected: a store compromise re-links every pairwise
  `sub`, and it contradicts the one-way posture of the rest of the system.
- Resolving the session from server state, asking when the request does not prove itself, and notifying through a
  signed `logout_token`. Chosen: it ends exactly the session the login or the `sid` names, closes logout CSRF, and
  reaches relying parties without the browser.

## Consequences

- A logout from a relying party without a valid hint for the current session takes one extra click, and the endpoint
  writes one continue grant per prompt.
- `EndSessionCommand` callers resolve the subject first.
- Pairwise clients' logout works end to end: revocation matches the subject's grants, and each `logout_token` carries
  the correctly projected `sub`. The terminated session is exactly the one the login or the `sid` identifies, so a
  validly signed hint cannot steer the logout to a mismatched subject.
- The replaceable `ISubjectIdentifierService` stays the only pairwise seam.
- A deployment can change how the endpoint parks, asks and notifies by replacing one mediator handler or the notifier.
- Smells: a relying party that supports only front-channel logout, and a deployment that needs cross-relying-party
  session-change detection. Both are met by the opt-in mechanisms of
  [ADR-1088](1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md).
- Tests cover the subject resolution, the parking, both confirmation handlers, the end-session handler and the
  coherence rule.

## References

- [OpenID Connect RP-Initiated Logout 1.0](https://openid.net/specs/openid-connect-rpinitiated-1_0.html) section 2 and
  [Back-Channel Logout 1.0](https://openid.net/specs/openid-connect-backchannel-1_0.html).
- Issue [#494](https://github.com/NCodeGroup/identity/issues/494).
- [`DefaultResolveEndSessionSubjectHandler.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultResolveEndSessionSubjectHandler.cs),
  [`DefaultEndSessionHandler.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultEndSessionHandler.cs),
  [`DefaultEndSessionContinueProvider.cs`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/DefaultEndSessionContinueProvider.cs),
  [`DefaultBackChannelLogoutNotifier.cs`](../../src/NCode.Identity.OpenId.Authentication/Sessions/DefaultBackChannelLogoutNotifier.cs)
  and [`LogoutChannelCoherenceRule.cs`](../../src/NCode.Identity.OpenId.Authentication/Clients/Coherence/LogoutChannelCoherenceRule.cs).
- [ADR-1314](1314-protocol-endpoints-keep-the-http-methods-their-specifications-require.md),
  [ADR-1112](1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) and
  [ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md).
