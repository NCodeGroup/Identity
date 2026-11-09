# 1487. The BFF forwards a remote API through a named HTTP client a host can configure

- **Status:** Accepted (amends [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md) decision 7)
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

[ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
decision 7 forwards a remote API through YARP's `IHttpForwarder` and fixes the properties of its transport: it follows
no redirect, sends or stores no cookie, uses no proxy and decompresses no response. The forces:

- **A host needs handlers of its own on that path.** Outbound telemetry, a resilience pipeline or a test server's
  handler belong on the requests that leave the BFF for a remote API.
- **The protocol client has such a seam.** It sends through the `IHttpClientFactory` client named
  `OpenIdClientDefaults.HttpClientName`, which hosts configure.
- **YARP-configured routes have their own client.** They use YARP's `IForwarderHttpClientFactory`
  ([ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
  decision 8).
- **A DPoP proof names the URI it is sent to.** The provider compares the proof's `htu` with the tenant's published
  origin
  ([ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md)),
  so a test cannot relay the call through a listener of its own: the proof would name the listener.

## Decision

**The BFF sends the requests of its remote APIs through a named `IHttpClientFactory` client that a host configures.**

1. **`MapBffRemoteApi` and `Bff:RemoteApis` send through `BffDefaults.RemoteApiHttpClientName`.** The client is named
   `NCode.Identity.Bff.RemoteApis`. A transport handler asks `IHttpMessageHandlerFactory` for the client's handler on
   every request, so the factory's handler rotation applies.
2. **The BFF registers the client's primary handler.** It is a `SocketsHttpHandler` with the properties of
   [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
   decision 7 (no redirect, no cookie, no proxy, no decompression), a 15-second connect timeout, several HTTP/2
   connections and YARP's trace-header propagator. The BFF removes the factory's loggers from the client, because the
   forwarder logs its own requests and the factory's loggers would log each forwarded URI with its query.
3. **A host adds handlers to the client or sets a primary handler of its own by configuring the client of that name
   after the BFF's registration.** The host then owns the properties it changes. `BffAccessTokenHandler` stays outside
   the client, so the token and its proof are attached to every request whatever handlers a host adds.

## Options considered

- **A private `SocketsHttpHandler`.** Rejected: no host can add a handler, and a test must relay the call through a
  listener that its proof then names.
- **A public handler-source interface of the BFF's own.** Rejected: `IHttpClientFactory` is the framework's seam for
  the handlers of an outbound client, and the protocol client uses it.

## Consequences

- A host configures the handlers of every remote API with `AddHttpClient(BffDefaults.RemoteApiHttpClientName)`.
- A test routes the BFF's remote API to a test server with the client's primary handler, and the DPoP proof names the
  provider's published origin.
- `BffDefaults.RemoteApiHttpClientName` is public API of `NCode.Identity.Bff.Protocol.Abstractions`.
- YARP-configured routes keep YARP's `IForwarderHttpClientFactory`.

## References

- [ADR-1381](1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md):
  the BFF forwards a remote API through YARP.
- [ADR-1387](1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md): the
  DPoP `htu` is compared with the tenant's published URLs.
- [ADR-1484](1484-a-management-routes-caller-is-the-principal-its-management-access-token-authenticates.md): a
  management route's caller is the principal its management access token authenticates.
- [#716](https://github.com/NCodeGroup/Identity/issues/716)
