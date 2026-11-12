# Metrics instrument catalog

Every meter, instrument, and attribute the NCode.Identity family emits, with each attribute's allowed values. It is the
reviewed contract that dashboards and alerts are built against: adding or renaming an instrument or attribute changes
this catalog in the same change ([ADR-1197](decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)).

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
- the OTLP exporter on the metrics pipeline only, when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured; logs and traces
  are left to the host.

## Meters

| Meter                                  | Package                                | Public name constant                    |
| -------------------------------------- | -------------------------------------- | --------------------------------------- |
| `NCode.Identity.OpenId.Authentication` | `NCode.Identity.OpenId.Authentication` | `OpenIdAuthenticationMetrics.MeterName` |
| `NCode.Identity.Events`                | `NCode.Identity.Events`                | `EventMetrics.MeterName`                |
| `NCode.Identity.SharedState.Valkey`    | `NCode.Identity.SharedState.Valkey`    | `ValkeyMetrics.MeterName`               |
| `NCode.Identity.Bff`                   | `NCode.Identity.Bff`                   | `BffTelemetry.MeterName`                |
| `NCode.Identity.OpenId.Client`         | `NCode.Identity.OpenId.Client`         | `OpenIdClientMetrics.MeterName`         |

## Protocol requests: `http.server.request.duration`

The protocol endpoints record no request instrument of their own. They enrich ASP.NET Core's
`http.server.request.duration` (meter `Microsoft.AspNetCore.Hosting`) through `IHttpMetricsTagsFeature`. The attribute
names are public constants on `OpenIdMetricAttributes`.

| Attribute                          | Present on                                | Values                                                                                                     |
| ---------------------------------- | ----------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `ncode.identity.openid.endpoint`   | Every protocol endpoint request           | The endpoint's registered name, such as `token_endpoint` or `authorization_endpoint`                       |
| `ncode.identity.openid.error`      | Every OAuth or OpenID error result        | The error code the server wrote, such as `invalid_grant`, `invalid_client`, or `temporarily_unavailable`   |
| `ncode.identity.openid.grant_type` | Token requests that reach grant selection | A registered grant type, such as `authorization_code` or `refresh_token`; `_OTHER` for an unregistered one |

The error code is tagged both where a JSON error response is written and where an authorization error is redirected
back to the client. The grant type is tagged when the token endpoint selects a grant handler, after client
authentication and request validation, so a request refused before then carries no grant type.

A rate-limited request is therefore `http.response.status_code=429` with
`ncode.identity.openid.error=temporarily_unavailable`, and so is a client authentication refused at the limit of failed
client authentications, which `ncode.identity.rate_limiting.requests` does not count: its refusals are the
`client.authentication` audit events with the error code `attempt_limit_reached`
([ADR-1399](decisions/1399-failed-client-authentications-are-limited-per-remote-address-by-default.md)).

## Instruments

### `ncode.identity.rate_limiting.requests`

Counter, unit `{request}`, meter `NCode.Identity.OpenId.Authentication`. Recorded once for each registered rate-limit
descriptor a stage judges for a protocol request
([ADR-1246](decisions/1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)), including the daily quotas
([ADR-1198](decisions/1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md)) and the overage a tier admits past
them ([ADR-1198](decisions/1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md)).

| Attribute                                | Values                                                                                                                                                |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ncode.identity.rate_limiting.partition` | the `Name` of a registered `RateLimitDescriptor`: `address`, `tenant`, `client`, `daily_requests`, `daily_token_requests`, or one a package registers |
| `ncode.identity.rate_limiting.result`    | `admitted`, `overage`, `rejected`, `store_unavailable` (fail open)                                                                                    |
| `ncode.identity.tenant.environment_type` | The tenant's registered environment type, such as `production` or `development` (a bounded value, never the tenant's free-form `environment_name`)    |

### `ncode.identity.events.published`

Counter, unit `{event}`, meter `NCode.Identity.Events`. Recorded once for each event published through the event seam
([ADR-1047](decisions/1047-auditing-and-events-observation-seam.md)).

| Attribute                                     | Present on                                        | Values                                                                                                                                                                                    |
| --------------------------------------------- | ------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ncode.identity.event.type`                   | Every event                                       | The event's CLR type name, such as `TokenIssuedAuditEvent`                                                                                                                                |
| `ncode.identity.audit.event_name`             | Audit events                                      | The catalogued `event_name`, such as `token.issued`                                                                                                                                       |
| `ncode.identity.audit.event_category`         | Audit events                                      | The audit event's category: `authentication`, `authorization`, `token`, `session`, `protocol`, `management` or `data_access`; see [audit records](audit-records.md)                      |
| `ncode.identity.tenant.environment_type`      | Audit events that carry a tenant environment type | The tenant's registered environment type, such as `production` or `development` (a bounded value, never the tenant's free-form `environment_name`); see [audit records](audit-records.md) |
| `ncode.identity.audit.outcome`                | Audit events                                      | `success`, `failure`, `denied`                                                                                                                                                            |
| `ncode.identity.token.type`                   | `TokenIssuedAuditEvent`                           | The issued token's server-set type, such as `id_token` or `refresh_token`                                                                                                                 |
| `ncode.identity.client_authentication.method` | `ClientAuthenticationFailedAuditEvent`            | The attempted registered method, such as `private_key_jwt` or `none`                                                                                                                      |
| `ncode.identity.bff.reason`                   | `BffAuditEvent` with a reason                     | The BFF's reason constant, such as a `BffLoginFailureReasons`, `BffSessionEndReasons` or `BffTokenFailureReasons` value; never a value a caller supplies                                  |

An event type adds bounded attributes of its own by implementing `ISupportMetricAttributes`; each one is listed here.

The BFF's logins, session ends, refresh failures and rejections are audit events (`bff.login.succeeded`, `bff.session.ended`, …), so this counter is the one that counts them, by `ncode.identity.audit.event_name` and `ncode.identity.bff.reason`.

### `ncode.identity.shared_state.operation.duration`

Histogram, unit `s`, meter `NCode.Identity.SharedState.Valkey`. Recorded for each round trip to the shared-state
server. The in-process adapter records nothing: a dictionary operation in memory has no latency or failure to watch.

| Attribute                               | Values                                                                                                                                                                                                                                                                                                                                                                                |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ncode.identity.shared_state.operation` | `counter.increment`, `counter.increment_until_exceeded`, `counter.get`, `counter.remove`, `key_set.try_add`, `key_set.contains`, `broadcast.publish`, `value_set.try_add`, `value_set.get`, `value_set.try_replace`, `value_set.try_take`, `value_set.remove`, `value_set.try_remove`, `index_set.add_member`, `index_set.get_members`, `index_set.remove_member`, `index_set.remove` |
| `ncode.identity.shared_state.adapter`   | `valkey`, or the name another networked adapter declares                                                                                                                                                                                                                                                                                                                              |
| `error.type`                            | On failure only: the full type name of the exception                                                                                                                                                                                                                                                                                                                                  |

### `ncode.identity.bff.token.refresh.duration`

Histogram, unit `s`, meter `NCode.Identity.Bff`. Recorded once for each request that needed a refreshed user access
token, from the moment its cached token was found stale until it got a token or a refusal: the wait for the session's
lock inside the replica, the wait for another replica's refresh, and the token endpoint round trip included. A request
served from the cache records nothing. The token endpoint call itself is also `System.Net.Http`'s
`http.client.request.duration`.

| Attribute                           | Values                                                                                                                                                                                    |
| ----------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ncode.identity.bff.refresh.result` | `refreshed` (this request presented the refresh token), `joined` (another request or replica refreshed the session and this request used its token), `failed`                             |
| `error.type`                        | On `failed` only: the `BffTokenFailureReasons` value, such as `refresh_failed`, `provider_error`, `provider_unavailable` or `refresh_wait_timeout`, or the full type name of an exception |

### `ncode.identity.bff.session_store.operation.duration`

Histogram, unit `s`, meter `NCode.Identity.Bff`. Recorded for each operation the BFF performs against its
`IBffSessionStore`, whichever store is registered. The default store is built on shared state, whose networked adapters
also time each round trip (`ncode.identity.shared_state.operation.duration`); one session store operation may be
several round trips.

| Attribute                                    | Values                                                                                                                                                                                          |
| -------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ncode.identity.bff.session_store.operation` | `create`, `get`, `replace`, `remove`, `find_by_session_id`, `find_by_subject`, `find_clients`, `find_by_client`, `add_transaction`, `take_transaction`, `acquire_refresh_lease`, `is_refresh_lease_held`, `release_refresh_lease` |
| `error.type`                                 | On failure only: the full type name of the exception                                                                                                                                            |

### `ncode.identity.outbound_http.retries` and `ncode.identity.openid_client.retries`

Counters, unit `{retry}`. `ncode.identity.outbound_http.retries` (meter `NCode.Identity.OpenId.Authentication`) is
recorded each time the guarded outbound client sends a call to a caller-supplied URL again after a transient failure
([ADR-1315](decisions/1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md),
[ADR-1385](decisions/1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)), and
`ncode.identity.openid_client.retries` (meter `NCode.Identity.OpenId.Client`) each time the OpenID protocol client sends
a call to an OpenID Provider again
([ADR-1336](decisions/1336-the-protocol-client-bounds-every-call-with-a-polly-core-pipeline-and-retries-only-safe-requests.md)). Only a GET or HEAD
without a body (or, for the protocol client, a DPoP proof) is ever retried. Each attempt is also `System.Net.Http`'s
`http.client.request.duration`.

| Attribute    | Values                                                                                                                                                                                                                    |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `error.type` | The failure that is retried: the response's status code (`408`, `429` or a `5xx` code), `timeout` when the per-attempt timeout cancelled the attempt, or the `HttpRequestError` in snake case, such as `connection_error` |

### `ncode.identity.outbound_http.timeouts` and `ncode.identity.openid_client.timeouts`

Counters, unit `{timeout}`. Recorded each time the guarded outbound client (`ncode.identity.outbound_http.timeouts`,
meter `NCode.Identity.OpenId.Authentication`) or the OpenID protocol client (`ncode.identity.openid_client.timeouts`,
meter `NCode.Identity.OpenId.Client`) cancels an attempt or a whole call because a timeout passed. A call the caller
cancels records nothing.

| Attribute                                    | Values                                                                                               |
| -------------------------------------------- | ---------------------------------------------------------------------------------------------------- |
| `ncode.identity.outbound_http.timeout_scope` | On `ncode.identity.outbound_http.timeouts`: `attempt` (`AttemptTimeout`) or `total` (`TotalTimeout`) |
| `ncode.identity.openid_client.timeout_scope` | On `ncode.identity.openid_client.timeouts`: `attempt` (`AttemptTimeout`) or `total` (`TotalTimeout`) |

## Traces

A package that traces its own mechanisms does it through one `ActivitySource` named like its meter
([ADR-1197](decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md)). A
host adds them to its tracing pipeline, for example with OpenTelemetry's `AddSource("NCode.Identity.*")`.

| Activity source      | Spans                                                                                                                     | Public name constant              |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------- | --------------------------------- |
| `NCode.Identity.Bff` | `ncode.identity.bff.token.refresh`, `ncode.identity.bff.session_store.operation`, with the attributes of their histograms | `BffTelemetry.ActivitySourceName` |
