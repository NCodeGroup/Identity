# 1434. Open tabs learn that a BFF session ended from its next answer, not a pushed event

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A BFF session ends in four ways: the user logs out, the OP posts a back-channel logout
([ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)), a refresh
comes back `invalid_grant`, or the idle or absolute timeout passes. Only the first happens in the tab that is open. The
other tabs of the same browser keep showing a signed-in page until they make a call. The design leaves the question open
([`docs/backend-for-frontend.md`](../backend-for-frontend.md) §11 question 3, issue
[#523](https://github.com/NCodeGroup/Identity/issues/523)): should the BFF push the end of a session to open tabs, for
example through a server-sent-events endpoint, or is the next guarded call's `401` soon enough?

A push endpoint would have to keep every guarantee of the BFF's other endpoints:

- It is guarded by the session cookie, the anti-forgery header and Fetch Metadata
  ([ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md)). A browser's
  `EventSource` cannot send a custom header, so the endpoint needs `fetch` streaming or a second guard.
- It carries no token in the stream.
- It bounds the number of connections per session, because HTTP/1.1 allows six connections per origin and a few open
  streams starve the application's own calls.
- It serves from every replica, so the end of a session on one replica reaches the stream held by another.

The session record already ends on every replica the moment it is removed, no token is in the browser, and every
guarded call answers `401`. A push changes only how soon a stale page clears.

## Decision

**The BFF has no push endpoint. The `401` with the `session_required` problem type that a guarded call answers once the
session is gone is the signal, and a frontend that wants a prompt answer polls `GET /bff/user` and shares what it learns
between tabs.**

1. **No push endpoint.** The BFF maps no server-sent-events or WebSocket endpoint and adds no shared-state primitive for
   one.
2. **The signal.** A guarded API call or `GET /bff/user` without a live session answers `401` with the
   `session_required` problem type, never a redirect. That answer holds on every replica as soon as the session record
   ends, whichever way it ended.
3. **The frontend pattern.** A frontend that wants to clear a stale page promptly:
   - polls `GET /bff/user` with the anti-forgery header at an interval it chooses. `GET /bff/user` reads the session and
     never slides the idle window, so polling does not keep a session alive;
   - probes when the clock reaches `session.idleExpiresAt` and `session.expiresAt`, which `GET /bff/user` returns, and
     rereads them after every answer;
   - checks again on `visibilitychange` when a hidden tab becomes visible, because a hidden tab's timers are throttled;
   - tells its other tabs through a `BroadcastChannel` when a call answers `401` or when the user logs out, so one
     tab's answer clears every tab;
   - navigates to `/bff/login` when it learns that the session ended.
4. **Freshness is the frontend's choice.** The BFF sets no polling interval. A frontend that accepts a stale page until
   the next call polls nothing and handles the `401` of that call.

## Alternatives considered

- **Server-sent events.** Rejected: each tab holds a connection, and the six connections that HTTP/1.1 allows per origin
  starve the application when a few tabs are open. The endpoint needs a pub/sub primitive in SharedState, or per-stream
  polling of the session store, which costs as much as client polling. It needs a shared per-session connection counter
  to stay bounded, `EventSource` cannot send the anti-forgery header, and proxies that buffer or cut idle streams break
  it.
- **WebSockets.** Rejected: the same costs as server-sent events, plus a second protocol to guard and to proxy.
- **`BroadcastChannel` alone.** Rejected: it tells tabs of the same browser about a logout or a `401` that one of them
  saw, and never learns of a back-channel logout, an expired idle window or a failed refresh. It is part of the pattern
  and does not replace the poll.
- **The OpenID session-management iframe.** Rejected: it depends on third-party cookies, which browsers block
  ([ADR-1074](1074-session-management-and-logout.md)).

## Consequences

- The BFF adds no endpoint and no shared-state primitive, and a replica holds no connection per tab.
- A frontend chooses how fresh its signed-in state is: no polling, a probe at the expiry times, a poll at an interval, or
  any mix.
- The consequence of [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)
  holds: telling other open tabs that the session ended is not part of the BFF, and their next guarded call answers
  `401`.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.7, §5.8 and §11
- [ADR-1074](1074-session-management-and-logout.md),
  [ADR-1292](1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1293](1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md)
- Issue [#523](https://github.com/NCodeGroup/Identity/issues/523)
