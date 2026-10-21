# Metrics instrument catalog

Every meter, instrument, and attribute the NCode.Identity family emits, with each attribute's allowed values. It is the
reviewed contract that dashboards and alerts are built against: adding or renaming an instrument or attribute changes
this catalog in the same change ([ADR-0197](adr/0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)).

No attribute carries a tenant, client, subject, or network address. Those belong to traces and logs, where cardinality
costs nothing per series.

## Collecting the metrics

The library packages record through `System.Diagnostics.Metrics` and cost nothing until a listener subscribes. A host
opts in with `AddOpenTelemetryMetrics(configuration)` from `NCode.Identity.Server`. It adds:

- every meter whose name starts with `NCode.Identity.`;
- ASP.NET Core's built-in meters (`Microsoft.AspNetCore.Hosting`, `Microsoft.AspNetCore.Server.Kestrel`,
  `Microsoft.AspNetCore.Routing`, `Microsoft.AspNetCore.Diagnostics`), `System.Net.Http`, and `System.Runtime`;
- a view that keeps only bounded attributes on `System.Net.Http`'s instruments, because their `server.address` and
  `server.port` name the hosts of client-registered URIs;
- the OTLP exporter, when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.

## Meters

| Meter                                  | Package                                | Public name constant                    |
| -------------------------------------- | -------------------------------------- | --------------------------------------- |
| `NCode.Identity.OpenId.Authentication` | `NCode.Identity.OpenId.Authentication` | `OpenIdAuthenticationMetrics.MeterName` |
| `NCode.Identity.Events`                | `NCode.Identity.Events`                | `EventMetrics.MeterName`                |
| `NCode.Identity.SharedState.Valkey`    | `NCode.Identity.SharedState.Valkey`    | `ValkeyMetrics.MeterName`               |

## Protocol requests: `http.server.request.duration`

The protocol endpoints record no request instrument of their own. They enrich ASP.NET Core's
`http.server.request.duration` (meter `Microsoft.AspNetCore.Hosting`) through `IHttpMetricsTagsFeature`. The attribute
names are public constants on `OpenIdMetricAttributes`.

| Attribute                          | Present on                         | Values                                                                                                     |
| ---------------------------------- | ---------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `ncode.identity.openid.endpoint`   | Every protocol endpoint request    | The endpoint's registered name, such as `token_endpoint` or `authorization_endpoint`                       |
| `ncode.identity.openid.error`      | Every OAuth or OpenID error result | The error code the server wrote, such as `invalid_grant`, `invalid_client`, or `temporarily_unavailable`   |
| `ncode.identity.openid.grant_type` | Token endpoint requests            | A registered grant type, such as `authorization_code` or `refresh_token`; `_OTHER` for an unregistered one |

A rate-limited request is therefore `http.response.status_code=429` with
`ncode.identity.openid.error=temporarily_unavailable`.

## Instruments

### `ncode.identity.rate_limiting.requests`

Counter, unit `{request}`, meter `NCode.Identity.OpenId.Authentication`. Recorded once for each partition that judges a
protocol request ([ADR-0196](adr/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)).

| Attribute                                | Values                                                           |
| ---------------------------------------- | ---------------------------------------------------------------- |
| `ncode.identity.rate_limiting.partition` | `tenant`, `address`, `client`                                    |
| `ncode.identity.rate_limiting.result`    | `admitted`, `rejected`, `store_unavailable` (the fail-open path) |

### `ncode.identity.events.published`

Counter, unit `{event}`, meter `NCode.Identity.Events`. Recorded once for each event published through the event seam
([ADR-0047](adr/0047-auditing-and-events-observation-seam.md)).

| Attribute                                     | Present on                             | Values                                                                    |
| --------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------- |
| `ncode.identity.event.type`                   | Every event                            | The event's CLR type name, such as `TokenIssuedAuditEvent`                |
| `ncode.identity.audit.action`                 | Audit events                           | The catalogued action, such as `token.issued` or `client.authentication`  |
| `ncode.identity.audit.outcome`                | Audit events                           | `success`, `failure`, `denied`                                            |
| `ncode.identity.token.type`                   | `TokenIssuedAuditEvent`                | The issued token's server-set type, such as `id_token` or `refresh_token` |
| `ncode.identity.client_authentication.method` | `ClientAuthenticationFailedAuditEvent` | The attempted registered method, such as `private_key_jwt` or `none`      |

An event type adds bounded attributes of its own by implementing `ISupportMetricAttributes`; each one is listed here.

### `ncode.identity.shared_state.operation.duration`

Histogram, unit `s`, meter `NCode.Identity.SharedState.Valkey`. Recorded for each round trip to the shared-state
server. The in-process adapter records nothing: a dictionary operation in memory has no latency or failure to watch.

| Attribute                               | Values                                                                    |
| --------------------------------------- | ------------------------------------------------------------------------- |
| `ncode.identity.shared_state.operation` | `counter.increment`, `counter.get`, `key_set.try_add`, `key_set.contains` |
| `ncode.identity.shared_state.adapter`   | `valkey`, or the name another networked adapter declares                  |
| `error.type`                            | On failure only: the full type name of the exception                      |
