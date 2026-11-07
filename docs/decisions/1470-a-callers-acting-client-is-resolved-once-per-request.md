# 1470. A caller's acting client is resolved once per request

- **Status:** Accepted (amends [ADR-1450](1450-a-client-credentials-access-token-names-the-clients-principal-as-its-subject.md) decisions 2, 4 and 5)
- **Date:** 2026-11-07
- **Deciders:** NCode Group

## Context

`IClientPrincipalResolver` decides whether a caller is a client acting for itself
([ADR-1450](1450-a-client-credentials-access-token-names-the-clients-principal-as-its-subject.md)) by reading the
client its token's `client_id` names. One request asks it several times: the management API's authorization resolves
the caller's principal through `IPrincipalResolver`, which consults it, and the audit recorder asks it for the actor of
every change it records.

The recorder asks after the change is committed
([ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md)). A machine
client that deletes itself is gone from the store when it asks, the read finds no client, and the record names an
administrator, not the client ([#632](https://github.com/NCodeGroup/Identity/issues/632)). Each of those callers also
reads the same client again ([#633](https://github.com/NCodeGroup/Identity/issues/633)).

UserInfo and token exchange both refuse a client's own token, and a host that registers its own
`IClientPrincipalResolver` must get one answer at both
([#634](https://github.com/NCodeGroup/Identity/issues/634)).

## Decision

**`IClientPrincipalResolver` answers each caller once per request, the management API resolves its caller's acting
client when it authenticates the caller, and every endpoint that refuses a client acting for itself asks the
resolver.**

1. **One answer per caller per request.** A caller is the context's tenant, its subject and its `client_id`. The first
   answer, a client or none, holds for the rest of the request. `DefaultClientPrincipalResolver` keeps it in
   `HttpContext.Items` and neither reads the store nor opens a store manager for a caller it has answered.
2. **The management API resolves the acting client when it authenticates the caller.**
   `ManagementAuthenticationEndpointFilter` asks the resolver as soon as the bearer token authenticates, before the
   endpoint runs. Authorization and the audit recorder reuse that answer, so a machine client that deletes itself is
   recorded as the `client` actor.
3. **Every refusal of a client acting for itself asks the resolver.** Token exchange and UserInfo refuse such a token
   when the resolver names an acting client for it. No endpoint compares a subject with a client's principal itself.

## Options considered

- **Reading the actor from the token alone.** A token does not say whether its subject is the client's principal, and
  ADR-1450 rejects a grant-type claim. Rejected.
- **Resolving the actor in each handler that deletes a client.** Every handler that removes or changes a client must
  remember to do it, and the next one that does not records the wrong actor. Rejected.
- **A memo inside the audit recorder.** Authorization and the recorder still read separately, and the recorder's first
  read can follow the commit. Rejected.
- **UserInfo comparing the subject with its resolved client's principal.** A host's own resolver gives another answer
  at token exchange than at UserInfo. Rejected.

## Consequences

- A request reads each caller's client at most once.
- A self-deleting machine client is audited as the `client` actor.
- Every authenticated management request reads its caller's client once, also when it records nothing.
- UserInfo reads the client once more for a token whose client does not act for itself.
- An implementation of `IClientPrincipalResolver` answers each caller once per request.

## References

- `IClientPrincipalResolver.cs`, `DefaultClientPrincipalResolver.cs`, `ActingClientMemo.cs`,
  `ManagementAuthenticationEndpointFilter.cs`, `DefaultManagementAuditRecorder.cs`,
  `DefaultUserInfoEndpointProvider.cs`
- [ADR-1450](1450-a-client-credentials-access-token-names-the-clients-principal-as-its-subject.md) — a
  client-credentials access token names the client's principal as its subject
- [ADR-1464](1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md) — a management
  audit record runs on its own bounded token and takes no request token
- [#632](https://github.com/NCodeGroup/Identity/issues/632), [#633](https://github.com/NCodeGroup/Identity/issues/633),
  [#634](https://github.com/NCodeGroup/Identity/issues/634)
