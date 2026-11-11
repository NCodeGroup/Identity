# 1401. UserInfo access is audited when a tenant or client opts in

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group

## Context

The UserInfo endpoint returns an end user's claims, their personal data, to the client an access token was issued to
([ADR-1030](1030-userinfo-endpoint-and-subject-authentication-seam.md)). A deployment under a data-protection regime,
such as one that must answer which parties read a data subject's personal data and when (GDPR Art. 15 and 30, HIPAA
§164.312(b)), needs a record of each disclosure. The audit trail
([ADR-1047](1047-auditing-and-events-observation-seam.md),
[ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md)) records the issuance of the
token, with its subject and client, but not each read of the claims it grants.

Three forces pull against auditing every read:

- UserInfo is a hot read path: a client can call it on every page it renders, and each audit event is a record in the
  indexed store and a log entry.
- Most deployments need only the issuance, which already names the subject and the client.
- An audit record is itself retained data, so it must carry what identifies the disclosure, not the data disclosed.

Which tenants, and which of a tenant's clients, handle data that needs the trail is the operator's knowledge, so the
choice belongs in the settings vocabulary, which scopes and merges a value per server, tenant and client
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md)).

## Decision

**The `audit_userinfo_access` setting, off by default, applies to tenants and clients and merges with `Or`. When the
effective value for the access token's client is on, each UserInfo response is audited as a `userinfo.accessed` event
that names the subject, the client and the names of the claims disclosed, never a claim value or the token.**

1. **The setting.** `audit_userinfo_access` (`OpenIdSettingNames.AuditUserInfoAccess`, a `bool`) applies to tenants and
   clients, defaults to `false`, and is not discoverable. It merges with `Or`: once a tenant turns it on, none of its
   clients can turn it off, so a client cannot leave its tenant's trail; a tenant that audits only some clients turns
   it on for those clients.
2. **The event.** `UserInfoAccessedAuditEvent` (`OpenIdAuditEventNames.UserInfoAccessed`, `userinfo.accessed`) has the
   category `data_access` (`AuditEventCategories.DataAccess`), a category for the disclosure of an end user's personal
   data to a caller, distinct from `authentication` and `token`, so a retention or export policy can select the
   disclosures alone. Its severity is `info`.
3. **A disclosure is recorded once the response is built.** `IAuditEventRecorder.RecordUserInfoAccessedAsync` publishes
   it with the outcome `success`, the subject as `subject_id`, the client as `client_id` and as the `client` actor, the
   request, and `details.claim_names`: the distinct names of the claims in the response, in ordinal order. The endpoint
   records it after it builds the JSON or the signed or encrypted JWT, so a response that cannot be protected is not
   recorded as a disclosure. No claim value and no token is written: the names say what kind of data was disclosed,
   and the values are already in the account store.
4. **A refusal after the token authenticated is recorded as `denied`.** An access token that authenticates but names no
   client, or one the tenant does not serve or has disabled, discloses nothing and answers `invalid_token`; when the
   tenant's effective value is on, `RecordUserInfoAccessDeniedAsync` publishes the event with the outcome `denied`, the
   error code `invalid_token`, the `client_id` the token names, if any (the actor is `anonymous` without one), and no
   details. The tenant's value decides because no client's settings can be read. A request whose token does not
   authenticate is already audited as `subject.authentication`, so it is not recorded again.

## Options considered

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

## Consequences

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
- [ADR-1030](1030-userinfo-endpoint-and-subject-authentication-seam.md),
  [ADR-1047](1047-auditing-and-events-observation-seam.md),
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md),
  [ADR-1283](1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md).
- [Audit records](../audit-records.md).
- Code: `NCode.Identity.OpenId.Authentication.Auditing.UserInfoAccessedAuditEvent`,
  `NCode.Identity.OpenId.Authentication.Auditing.IAuditEventRecorder`,
  `NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.DefaultUserInfoEndpointProvider`,
  `NCode.Identity.Events.Audit.AuditEventCategories`.
