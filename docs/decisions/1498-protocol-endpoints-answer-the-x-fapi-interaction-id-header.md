# 1498. Protocol endpoints answer the x-fapi-interaction-id header

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

FAPI 1.0 Part 1 §6.2.1-11 has a server answer a request that carries an `x-fapi-interaction-id` header with the same
value in the response, and a server that receives none answers with a value of its own. The value is a UUID (RFC 4122)
that a client and a server use to correlate one call in both their logs. The OpenID Foundation conformance suite checks
the header on a resource response (`CheckForFAPIInteractionIdInResourceResponse`), so a server that omits it fails the
FAPI plans.

- **An endpoint cannot be left out.** A client correlates every call it makes, whether it is a token request, a
  userinfo request or a discovery request, and the suite checks the header on a resource response and on the
  authorization server's responses alike.
- **The value is client input.** A client sends an arbitrary string, and a server that writes a header from it reaches
  its logs and every downstream system with that string.

## Decision

**Every OpenID protocol endpoint answers a request with an `x-fapi-interaction-id` response header.**

1. **A UUID is echoed as written.** When the request's `x-fapi-interaction-id` header is a UUID in its canonical
   hyphenated form (RFC 4122), the response carries the same text, in the same letter case.
2. **Anything else is replaced.** When the header is absent, empty or not a UUID, the response carries a new UUID.
3. **The header is set before the endpoint runs.** `FapiInteractionIdEndpointFilter` sets it before it calls the next
   delegate, so a success and an error result alike carry it.
4. **One filter covers the protocol endpoints.** The filter is registered on the OpenID endpoint group, just inside the
   metrics filter, so every endpoint of the group carries the header, including a request the environment or the rate
   limiter refuses.

## Options considered

- **Echo any value.** Rejected: an arbitrary client string reaches logs and downstream systems.
- **Only the userinfo endpoint.** Rejected: a client correlates every call it makes, and the suite checks more than one
  endpoint.

## Consequences

- A client that sends a UUID reads it back on every protocol response, and a client that sends none reads a UUID the
  server created.
- A value that is not a UUID never leaves the request.
- The management API and the interactive pages are outside the OpenID endpoint group and do not carry the header.

## References

- Issue [#124](https://github.com/NCodeGroup/Identity/issues/124)
- [FAPI 1.0 Part 1: Baseline](https://openid.net/specs/openid-financial-api-part-1-1_0.html)
- [RFC 4122](https://www.rfc-editor.org/rfc/rfc4122)
