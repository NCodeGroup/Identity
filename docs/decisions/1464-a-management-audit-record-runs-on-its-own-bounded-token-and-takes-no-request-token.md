# 1464. A management audit record runs on its own bounded token and takes no request token

- **Status:** Accepted (amends [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md), [ADR-1061](1061-local-account-management-is-an-opt-in-capability.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0141](project-template/0141-side-effects-after-a-commit-go-through-an-outbox.md)

## Context

Every `IManagementAuditRecorder` call follows a committed change: the handler saves, revokes or rotates, and then
records the change. A record resolves the tenant's environment type
([ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md)) through the store and then
publishes the event.

With the request's token on the lookup, a client that disconnects after the commit cancels the lookup.
`DefaultManagementTenantEnvironmentResolver` lets the `OperationCanceledException` through, so the event is never
published and a caller that records several events stops at the first. The change is committed and its audit record is
lost ([#621](https://github.com/NCodeGroup/identity/issues/621)).

## Decision

**A management audit record runs on its own bounded token, and no `IManagementAuditRecorder` method takes the request's
token.**

1. **One bounded step covers the whole record.** The environment lookup and the publish run in one
   `PostCommitWork.RunAsync` step on one 30-second token. The request's token reaches neither.
2. **No recorder method takes a `CancellationToken`.** No `IManagementAuditRecorder` method, and no helper whose only
   work is recording, takes one. This is the exception to csharp-production §1's rule that every async method of ours
   takes a `CancellationToken`, for a method whose whole work follows a commit on its own bounded token.
3. **A failed record is logged, never thrown.** A lookup or publish that throws, or a bound that elapses, is logged as
   `AuditPublishFailed`. A committed change never answers an error, and a caller that records several events records
   the rest.

## Options considered

- **Passing `CancellationToken.None` at every call site.** Every caller must remember it, and the next call site that
  passes the request's token drops a record again. The interface would still invite the token. Rejected.
- **Keeping a token parameter the implementation ignores.** A parameter that has no effect misleads the caller and a
  custom recorder can honor it. Rejected.
- **Bounding the lookup and the publish separately.** Two bounds allow twice the wait for the same record and leave a
  seam between them where one step can fail while the other has run. Rejected.

## Consequences

- A client that disconnects after a commit never drops a management audit record.
- A hung store adds at most 30 seconds to the request that records.
- An implementation of `IManagementAuditRecorder` owns its bound.
- A custom recorder is registered in place of `DefaultManagementAuditRecorder`.

## References

- `DefaultManagementAuditRecorder.cs`, `PostCommitWork.cs`
- [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) — a tenant declares its
  environment type
- [ADR-1351](1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) — a committed change is
  applied and announced on its own bounded token
