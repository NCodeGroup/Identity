# 1401. UserInfo authenticates an access token, returns enricher-built claims, and audits disclosures on opt-in

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

The UserInfo endpoint (OpenID Connect Core §5.3) returns an end user's claims, their personal data, to the client an
access token was issued to. Three questions follow from that.

- **Who is calling?** UserInfo is called with an access token the server itself issued, so the tenant's keys, issuer and
  audience are known only once the request's environment has resolved. The interactive endpoints authenticate a session
  cookie instead. Both are one question, "which subject is this request for", and applications need one seam to change
  how credentials are validated.
- **Which claims?** The claims a deployment discloses depend on the scopes and the account store behind it, and sibling
  packages add their own, so the set cannot be fixed in the endpoint.
- **Who read what?** A deployment under a data-protection regime, such as one that must answer which parties read a data
  subject's personal data and when (GDPR Art. 15 and 30, HIPAA §164.312(b)), needs a record of each disclosure. The audit
  trail ([ADR-1047](1047-auditing-and-events-observation-seam.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)) records the issuance of the
  token, with its subject and client, but not each read of the claims it grants.

Three forces pull against auditing every read:

- UserInfo is a hot read path: a client can call it on every page it renders, and each audit event is a record in the
  indexed store and a log entry.
- Most deployments need only the issuance, which already names the subject and the client.
- An audit record is itself retained data, so it must carry what identifies the disclosure, not the data disclosed.

Which tenants, and which of a tenant's clients, handle data that needs the trail is the operator's knowledge, so the
choice belongs in the settings vocabulary, which scopes and merges a value per server, tenant and client
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)).

## Decision

**UserInfo authenticates its caller through a subject-authentication command, builds its claims through an enricher
pipeline, and audits each disclosure only when the `audit_userinfo_access` setting is on for the access token's
client.**

1. **The endpoint.** `/oauth2/userinfo` answers `GET` and `POST` and is advertised as `userinfo_endpoint`.
2. **Authentication.** The endpoint sends `AuthenticateResourceRequestCommand` with the identity resource server's
   audience. `DefaultAuthenticateSubjectHandler` accepts only an access token presented with the `Bearer` or `DPoP`
   scheme; a request without one is unauthenticated. The token is validated through `AuthenticateAccessTokenCommand`
   (`DefaultAuthenticateAccessTokenHandler`), which uses `IIssuedTokenValidator` in the request's environment: the
   tenant's keys, the issuer, the `at+jwt` type, the required audience and session revocation
   ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)). Its proof of possession is
   re-checked by `IProofOfPossessionService.ValidateResourceAccessAsync`
   ([ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)).
3. **The seam.** Interactive endpoints authenticate the session cookie through `AuthenticateSubjectCommand` instead.
   With no subject the answer is `401 invalid_token`, and a failed authentication is audited as
   `subject.authentication`. An application replaces the handlers to change how credentials are validated.
4. **Claims.** They come from the `GetUserInfoClaimsCommand` enricher pipeline. The default handler contributes only
   `sub`; packages and hosts add scope-gated claims, such as `DefaultGetLocalAccountUserInfoClaimsHandler`, and
   `DefaultGetRequestedUserInfoClaimsHandler` adds the claims that `claims.userinfo` requested
   ([ADR-1039](1039-requested-claims-acr-and-auth-time-follow-the-request-and-the-authentication-event.md)).
5. **The response.** It is a `UserInfoResult` (`[JsonExtensionData]`), or a signed and optionally encrypted JWT per the
   client's `userinfo_*` metadata
   ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). Its `sub` is the access token's
   projected `sub`, equal to the ID token's
   ([ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md)).
6. **The setting.** `audit_userinfo_access` (`OpenIdSettingNames.AuditUserInfoAccess`, a `bool`) applies to tenants and
   clients, defaults to `false`, and is not discoverable. It merges with `Or`: once a tenant turns it on, none of its
   clients can turn it off, so a client cannot leave its tenant's trail; a tenant that audits only some clients turns
   it on for those clients.
7. **The event.** When the access token's client has the setting on, `IAuditEventRecorder.RecordUserInfoAccessedAsync`
   publishes `UserInfoAccessedAuditEvent` (`OpenIdAuditEventNames.UserInfoAccessed`, `userinfo.accessed`). Its category
   is `data_access` (`AuditEventCategories.DataAccess`), a category for the disclosure of an end user's personal data
   to a caller, distinct from `authentication` and `token`, so a retention or export policy can select the disclosures
   alone. Its severity is `info` and its outcome `success`. It names the subject as `subject_id`, the client as
   `client_id` and as the `client` actor, the request, and `details.claim_names`: the distinct names of the claims in
   the response, in ordinal order.
8. **Recorded once the response is built.** The endpoint records the event after it builds the JSON or the signed or
   encrypted JWT, so a response that cannot be protected is not recorded as a disclosure. No claim value and no token
   is written: the names say what kind of data was disclosed, and the values are already in the account store.
9. **A refusal after the token authenticated is `denied`.** An access token that authenticates but names no client, or
   a client the tenant does not serve or has disabled, discloses nothing and is answered `invalid_token`. When the
   tenant's effective value is on, `RecordUserInfoAccessDeniedAsync` publishes the event with the outcome `denied`, the
   error code `invalid_token`, the `client_id` the token names, if any (the actor is `anonymous` without one), and no
   details. The tenant's value decides because no client's settings can be read. A request whose token does not
   authenticate is already audited as `subject.authentication`, so it is not recorded again.

## Options considered

- **Validate the bearer token inside the endpoint, or in a bespoke access-token authentication service.** Rejected: the
  endpoint would own credential handling that interactive endpoints share, and an application could not replace it
  through the same command seam.
- **A pre-routing authentication scheme.** Rejected: the tenant's keys are known only after the environment resolves,
  so a scheme that runs before routing cannot validate the token.
- **A fixed claims DTO.** Rejected: sibling packages and hosts add claims by registering enrichers, which a fixed shape
  forbids.
- **Audit every UserInfo response.** Rejected: a record per page render, for a disclosure that most deployments need
  only at issuance.
- **Rely on the `token.issued` event.** Rejected as the only record: it says that a client may read the claims, not that
  it read them or which ones it read.
- **A tenant-only setting.** Rejected: a tenant whose clients handle data of different sensitivity would have to audit
  every client to audit one.
- **Merge with `Replace`.** Rejected: a client could opt out of the trail its tenant requires.
- **Record the claim values, or a hash of them.** Rejected: the audit store would become a second copy of the personal
  data, with its own retention and erasure obligations, and a hash of a low-entropy value such as a birth date is
  reversible.
- **Audit in the `GetUserInfoClaimsCommand` pipeline.** Rejected: an enricher sees only its own claims, and the response
  can still fail after the pipeline runs.
- **Record the event under the `token` or `protocol` category.** Rejected: a disclosure of personal data is what a
  compliance export selects, and a category of its own lets it select them without enumerating event names.
- **Authenticate through the command seam, build claims with enrichers and audit on opt-in.** Chosen: the token is
  validated in the environment that issued it, the claims stay open to registration, and the trail costs nothing where
  it is not needed.

## Consequences

- One command seam authenticates both the access token UserInfo receives and the session cookie interactive endpoints
  receive, and an application changes either by replacing a handler.
- A package adds UserInfo claims by registering an enricher, without editing the endpoint.
- An operator turns on a PII-access trail for a tenant or for chosen clients, and can answer which client read which
  kinds of a subject's data and when.
- With the setting on, each UserInfo response costs one audit event: a record in the indexed store, a log entry and a
  metric.
- The record names claim types, not values, so it shows that a client read an email address, not which one.
- A response that fails after the audit, for example while it is written to the connection, is still recorded.
- This implements issue [#21](https://github.com/NCodeGroup/identity/issues/21).

## References

- [OpenID Connect Core 1.0 §5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), the UserInfo
  endpoint.
- [ADR-1039](1039-requested-claims-acr-and-auth-time-follow-the-request-and-the-authentication-event.md),
  [ADR-1047](1047-auditing-and-events-observation-seam.md),
  [ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md),
  [ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md),
  [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md).
- [Audit records](../audit-records.md).
- Code: `NCode.Identity.OpenId.Authentication.Auditing.UserInfoAccessedAuditEvent`,
  `NCode.Identity.OpenId.Authentication.Auditing.IAuditEventRecorder`,
  `NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.DefaultUserInfoEndpointProvider`,
  `NCode.Identity.Events.Audit.AuditEventCategories`.
