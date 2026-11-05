# 1422. A management problem that asks the caller to wait carries the wait in retryAfterSeconds

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md),
  decision 13 (a rekey too soon after the last rotation answers 409 with the wait in the problem's `retryAfterSeconds`
  as well as in `Retry-After`);
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md), decisions 11 and 12 (the 409
  and 503 problems of a root key change too soon and of a root key that is unavailable carry the wait in
  `retryAfterSeconds` as well as in `Retry-After`)

## Context

Three problems of the Management API ask the caller to wait: a rekey too soon after the last rotation, a root key
change too soon, and an owner whose root key is unavailable. Each carries the wait in the `Retry-After` header. A
browser fetch layer, RTK Query's `fetchBaseQuery`, hands a failed response's body to the caller and drops its headers,
so the console cannot show the wait. RFC 9457 allows a problem to carry extension members, and every client of the API
already reads the body it parses.

## Decision

**A problem that asks the caller to wait carries the wait as a `retryAfterSeconds` member beside `Retry-After`, and the
console shows it.**

1. **One writer answers such a problem.** `RetryAfterProblem.Create(httpContext, wait, problem)` sets `Retry-After` to
   the wait in whole seconds, rounded up and at least one, sets the problem's `retryAfterSeconds` member to the same
   number, and returns the problem. A rekey too soon, a root key change too soon and an unavailable root key are all
   answered through it.
2. **`ManagementProblemMembers` names the members.** The public constants `RetryAfterSeconds`, `OwnerType`, `OwnerId`,
   `Provider`, `KeyReference`, `SettingsScope` and `DocumentError` are the names of the extension members the
   management API writes, and every writer and test uses them.
3. **The rekey-too-soon detail is stable.** It reads "The last rotation of this master key has not reached every
   replica yet."; the wait is in the member, not in the prose.
4. **The console appends the wait.** `apiErrorMessage` appends " Try again in N seconds." when the problem carries a
   positive whole `retryAfterSeconds`: below 60 seconds the wait is in seconds, below an hour it is in whole minutes
   rounded up, otherwise it is in whole hours rounded up, and the unit is singular for one. `ManagementProblem` in
   `@ncode/api` mirrors the problem shape, so every surface that shows an API error shows the wait.

## Options considered

- **A base-query wrapper that reads the header.** Rejected: only the console gains the wait, and any other client that
  cannot read headers still lacks it.
- **The seconds in the detail prose.** Rejected: the wait is not machine-readable and every change of wording breaks
  the clients that parse it.

## Consequences

- Any client reads the wait from the body, whether or not its fetch layer exposes headers.
- A new problem that asks the caller to wait uses `RetryAfterProblem.Create`, so the header and the member agree.
- A member name that the management API writes is a constant of `ManagementProblemMembers`, not a string literal.

## References

- [ADR-1407](1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md),
  [ADR-1414](1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md)
- RFC 9457 (problem details), RFC 9110 §10.2.3 (`Retry-After`)
- `src/NCode.Identity.OpenId.Management/Endpoints/RetryAfterProblem.cs`,
  `src/frontend/packages/store/src/apiError.ts`
- Issue [#571](https://github.com/NCodeGroup/Identity/issues/571)
