# 208. A lost concurrency race surfaces as a store-neutral exception

- **Status:** Accepted (amended by [ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md):
  refresh-token rotation; [ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md): management writes;
  [ADR-0215](0215-a-revocation-wins-a-concurrency-race.md): a revocation retries a lost race)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A single-use grant is spent by `IPersistedGrantService.ConsumeOnceOrDefault`: it reads the grant, refuses one that is
unknown, expired, or already consumed, stamps `ConsumedWhen`, and saves. The grant row's concurrency token makes the
spend atomic across replicas without a shared-state adapter
([ADR-0192](0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)): of two requests that
spend one grant at the same moment, at most one save succeeds, because the `UPDATE` of the other carries a stale token
([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)).

Entity Framework reports the lost race as a `DbUpdateConcurrencyException`, and nothing caught it. A review (issue
[#257](https://github.com/NCodeGroup/Identity/issues/257)) found the consequence: the request that lost the race reached its client as `500`, where a sequential second
spend of the same grant is answered with its protocol error. The exception is also the type of one store technology.
`NCode.Identity.OpenId.Authentication` does not reference Entity Framework, and a store need not be built on it, so a
caller that wanted to handle the race would have to depend on the technology of the store.

## Decision

1. **A lost race is a `StoreConcurrencyException`.** `NCode.Persistence.Stores.StoreConcurrencyException`
   (`NCode.Persistence.Abstractions`, a `[PublicAPI] public class` with the three standard exception constructors) is
   thrown when a unit of work writes an entity that another unit of work changed after it was read, as the concurrency
   token of the entity shows. The caller lost the race and must not retry the same write.
2. **The store manager translates a failed save.** `EntityStoreManager.SaveChangesAsync` catches Entity Framework's
   `DbUpdateConcurrencyException`, the failure of the `[ConcurrencyCheck]` in the `UPDATE` clause, and throws a
   `StoreConcurrencyException` that has it as its inner exception. Any other failure of the save propagates unchanged.
3. **A store's own token check throws it too.** Every Entity Framework store that compares the token that its caller
   presents with the stored one (`ServerStore`, `TenantStore`, `ClientStore`, `ClientGrantStore`, `GrantStore`, and
   `ResourceServerStore`) throws a `StoreConcurrencyException` when they differ.
4. **`ConsumeOnceOrDefault` answers the loser as a second spend.** It catches `StoreConcurrencyException` around the
   update and the save, and returns `null`, as it does for a grant that is unknown, expired, or already consumed. Every
   caller answers `null` as a second spend, so each grant answers a concurrent second spend in one way, the same as a
   sequential one:
   - an authorization code: `invalid_grant` (`400`), and the replay revokes the grants of the end-user session
     (RFC 6749 §4.1.2, [ADR-0112](0112-authorization-code-replay-revokes-session.md));
   - a device code: `invalid_grant` (`400`);
   - a CIBA `auth_req_id`: `invalid_grant` (`400`);
   - a pushed authorization request: `invalid_request_uri` (`400`), as for any `request_uri` that is unknown, expired,
     or used;
   - an initial access token: `invalid_token` (`401`), and nothing is stored;
   - the `state` of a paused authorization flow at the continue endpoint: `400`.

## Options considered

- **A conditional `ExecuteUpdateAsync` that spends the row.** Rejected: the Entity Framework InMemory provider does not
  support it, and it bypasses the interceptor-managed concurrency token
  ([ADR-0012](0012-interceptor-managed-concurrency-tokens.md)).
- **Catch `DbUpdateConcurrencyException` in `NCode.Identity.OpenId.Authentication`.** Rejected: it couples the protocol
  layer to one store technology, and another store would have to throw the Entity Framework type to be handled.
- **Record the spend in an `IExpiringKeySet`.** Rejected: the grant already has a row, and a second record would have to
  agree with it
  ([ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).

## Consequences

- Of two requests that spend one grant at the same moment, one receives the grant and the other receives the error of a
  second spend. No request reaches its client as `500` for it.
- A store that is not built on Entity Framework reports a lost race by throwing `StoreConcurrencyException`, and a
  caller handles it without a reference to the store technology.
- A management write that loses a race is answered `412`
  ([ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md)).
- Refresh-token rotation spends the presented token with its successor in one unit of work, and a concurrent second
  spend is answered as a reuse ([ADR-0211](0211-a-rotated-refresh-token-is-spent-with-its-successor.md)).
- **Breaking:** an Entity Framework store reports a stale token as `StoreConcurrencyException`, not as
  `DbUpdateConcurrencyException`. A caller that catches the Entity Framework type around a store method or around
  `IStoreManager.SaveChangesAsync` catches the store-neutral type instead. A caller that saves through a `DbContext`
  directly still receives Entity Framework's type.
- This implements issue [#257](https://github.com/NCodeGroup/Identity/issues/257).

## References

- [ADR-0012](0012-interceptor-managed-concurrency-tokens.md) — the interceptor-managed concurrency tokens that detect
  a lost race.
- [ADR-0112](0112-authorization-code-replay-revokes-session.md) — a second redemption of an authorization code revokes
  the session.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — single-use state that has no
  row of its own.
- [ADR-0192](0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md) — an initial access
  token is spent through the grant row.
- Code: `NCode.Persistence.Stores.StoreConcurrencyException`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Stores.EntityStoreManager`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`.
