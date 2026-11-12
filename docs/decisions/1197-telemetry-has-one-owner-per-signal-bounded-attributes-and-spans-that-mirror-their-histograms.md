# 1197. Telemetry has one owner per signal, bounded attributes, and spans that mirror their histograms

- **Status:** Accepted
- **Date:** 2026-10-29
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0113](project-template/0113-metrics-have-one-owner-per-signal-and-bounded-dimensions.md), [project-template ADR-0114](project-template/0114-one-activity-source-named-like-its-meter.md)

## Context

[project-template ADR-0113](project-template/0113-metrics-have-one-owner-per-signal-and-bounded-dimensions.md) gives
every metric signal one owner and every metric attribute a value from a vocabulary the application controls.
[project-template ADR-0114](project-template/0114-one-activity-source-named-like-its-meter.md) has a project trace only
its own mechanisms, through one activity source named like its meter, with spans that mirror its duration histograms and
carry no secret or personal data. This record applies both to the family's packages.

The platform is observable through OpenTelemetry ([ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) §8). ASP.NET
Core already measures and traces each inbound request, and `System.Net.Http` each outbound call, so the protocol
endpoints measure only what neither of them nor the event seam
([ADR-1047](1047-auditing-and-events-observation-seam.md)) already measures. The rate limiter's refusals
([ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) and the health of the shared state
those refusals depend on are such mechanisms.

Two forces of the family's own add to those:

- **Cardinality is bounded by the platform, not by its customers.** The hosted platform's metrics backend allows 10,000
  active series on its free tier ([hosting analysis](../hosting-analysis.md)). Tenants, clients, subjects and remote
  addresses are unbounded, so a series per tenant grows with the customer base until the backend drops data.
- **Vocabularies are open** ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a package registers
  another grant type, client-authentication method or endpoint, so a metric dimension cannot be a closed list either.

The BFF ([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
[ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)) is the first package
whose latency hides inside a request: a token refresh may wait for the session's lock and for another replica's lease
before it calls the token endpoint, and every session read or write goes through a replaceable `IBffSessionStore`.
Without spans of its own, a slow `/api` request shows only the outbound call, not the waits around it.

## Decision

**Every signal has exactly one owner, every metric attribute comes from a vocabulary the server controls, and a package
that traces does so through one activity source whose spans mirror its duration histograms.**

### 1. One owner per signal

| Signal                                                              | Owner                                                                                 |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Request count, latency and status of every protocol endpoint        | ASP.NET Core's `http.server.request.duration`, enriched with protocol attributes (§3) |
| Outbound calls (`jwks_uri`, `request_uri`, `sector_identifier_uri`) | `System.Net.Http`'s `http.client.request.duration`, through named `HttpClient`s       |
| Domain outcomes (token issued, client authentication failed, …)     | The event seam: `DefaultEventMetricsHandler` counts each published event (§4)         |
| Mechanisms that are not domain events (rate limiting, shared state) | A family instrument, defined once in the package that owns the mechanism (§5)         |

A fact is an event when someone would audit it; its count derives from the event, never from a parallel counter. A
mechanism on a flood path (a refused request, a counter-store call) is not an event, because a flood of refusals must
not flood the event pipeline, so it gets a direct instrument.

### 2. Meters, names and units

- **One meter per package that owns instruments**, named after the package by a public `MeterName` constant that
  starts with `NCode.Identity.`: `NCode.Identity.OpenId.Authentication`, `NCode.Identity.Events`,
  `NCode.Identity.SharedState.Valkey`, `NCode.Identity.OpenId.Client` and `NCode.Identity.Bff`. A host subscribes to
  the whole family with one wildcard.
- **Meters come from `IMeterFactory`**, never `new Meter(…)`: the factory scopes a meter to its service provider, so
  two hosts in one process (tests, the Aspire orchestrator) never share instruments, and tests read them with
  `MetricCollector<T>`. A package's instruments live in one internal singleton `…Metrics` class that the factory's
  meter creates once; call sites call methods on it, never `Meter` APIs. `DefaultEventMetricsHandler` takes its meter
  from the factory too.
- **Instrument and attribute names share the family namespace `ncode.identity.{area}.…`**, as ASP.NET Core's own
  instruments and their attributes share `aspnetcore.…`. An attribute that OpenTelemetry's semantic conventions already
  define (`error.type`, `http.route`, `http.response.status_code`) keeps its standard name.
- **An unregistered value records as `_OTHER`**, OpenTelemetry's convention for an unrecognized value, so a caller
  never creates a series with a value it chose.

### 3. The protocol endpoints enrich the built-in HTTP instrument

The protocol endpoint group adds attributes to `http.server.request.duration` through `IHttpMetricsTagsFeature` rather
than recording a request instrument of its own:

- `ncode.identity.openid.endpoint` is the endpoint's registered name (`token_endpoint`, `authorization_endpoint`, …,
  from `OpenIdConstants.EndpointNames` or the name a package registers). It is stable when an operator remaps routes or
  switches between host- and path-based tenant routing, which change `http.route`.
- `ncode.identity.openid.error` is the OAuth error code of an error response (`invalid_grant`, `invalid_client`,
  `temporarily_unavailable`, …), set where the error response is written so every path tags it the same way. Two
  writers exist: the executor of JSON protocol results, and the authorization error-redirect executor for an error
  redirected back to the client. HTTP status alone cannot tell `invalid_grant` from `invalid_client`. Error codes are
  server-chosen, never echoed from the request.
- `ncode.identity.openid.grant_type`, on the token endpoint only, is the grant type when it is a registered one, else
  `_OTHER`, because the grants differ greatly in cost. It is set where the grant handler is selected, the one place
  that knows the registered grant types, so a request refused earlier (client authentication, request validation)
  carries no grant type.

The rate limiter's `429` is therefore visible as `http.response.status_code=429` with
`ncode.identity.openid.error=temporarily_unavailable`, with no instrument of its own for the response.

### 4. Domain outcomes come from the event seam

`DefaultEventMetricsHandler` is the single counter of domain outcomes (`ncode.identity.events.published`). Its
attributes are `ncode.identity.event.type`, `ncode.identity.audit.event_name`, `ncode.identity.audit.event_category` and
`ncode.identity.audit.outcome`. An event type may declare additional **bounded** metric attributes by implementing
`ISupportMetricAttributes`, which the handler reads, so the curated event catalog also governs the metric's dimensions:
a `token.issued` event adds its server-set token type (`ncode.identity.token.type`) and a client-authentication failure
adds the registered method the client attempted (`ncode.identity.client_authentication.method`). The grant type is not
repeated on the event; it is already an attribute of the token endpoint's requests (§3). An event never contributes its
tenant, client or subject.

### 5. Mechanism instruments

- **Rate limiting** (`NCode.Identity.OpenId.Authentication`): counter `ncode.identity.rate_limiting.requests`
  (`{request}`) with `ncode.identity.rate_limiting.partition` (`tenant`, `address`, `client`, and the daily quotas'
  `daily_requests` and `daily_token_requests` of
  [ADR-1198](1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md)) and
  `ncode.identity.rate_limiting.result` (`admitted`, `rejected`, `store_unavailable`), recorded once per judged
  partition, mirroring ASP.NET Core's `aspnetcore.rate_limiting.requests`. `store_unavailable` makes the fail-open path
  of [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) alertable.
- **Shared state** (each networked adapter, in its own package's meter, such as `NCode.Identity.SharedState.Valkey`):
  histogram `ncode.identity.shared_state.operation.duration` (`s`) with `ncode.identity.shared_state.operation`
  (`key_set.try_add`, `key_set.contains`, `counter.increment`, `counter.get`), `ncode.identity.shared_state.adapter`
  (`valkey`, or the name another adapter declares) and `error.type` on failure, timing the server round trip only.
  Replay checks, DPoP nonces and rate limits all depend on it, so its latency and failures are one view. The in-process
  adapter records nothing: a dictionary operation in memory has no latency or failure worth a series.

### 6. Tenant, client, subject and address are never metric attributes

They belong to traces and logs, where cardinality costs nothing per series: the request's span carries the tenant and
client, and a metric's exemplar links a slow bucket to that span. Per-tenant **usage**, what a tenant consumed for plans
and billing, is a stored count ([ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)), not a metric
series.

### 7. A package that traces has one activity source named like its meter

- **One `ActivitySource` per package that traces.** Its name is the package name, a public constant beside the meter's:
  `BffTelemetry.ActivitySourceName` is `"NCode.Identity.Bff"`, and `BffActivities.Source` uses it. A host adds the whole
  family with `AddSource("NCode.Identity.*")`. The source is static: unlike a meter it has no factory to scope it to a
  service provider, because .NET has no activity-source factory, and it costs nothing until a listener samples it.
- **A package traces only its own mechanisms.** It never adds a span for what ASP.NET Core or `System.Net.Http`
  already traces: the inbound request and the outbound call are theirs, and a package's span wraps the work around
  them, such as waits and store operations.
- **A span mirrors its histogram.** Each span measures what one of the package's duration histograms measures, is named
  after it without the `.duration` suffix (`ncode.identity.bff.token.refresh`,
  `ncode.identity.bff.session_store.operation`), and carries the same bounded attributes. A failure sets the span's
  status to `Error` and its `error.type`, as the histogram does, so one vocabulary describes both signals.
- **A span carries no secret and no personal data.** A span never carries a token, handle, cookie, key, subject, client
  id or path; correlation with a request comes from the parent span.

### 8. Hosts opt in

The library packages depend only on `System.Diagnostics.Metrics`; recording costs nothing until a listener subscribes.
`NCode.Identity.Server` provides `AddOpenTelemetryMetrics(configuration)`, which the reference host calls, because host
composition is the server package's job, not `Program.cs`'s. It adds:

- the OpenTelemetry SDK and the family's meters by the `NCode.Identity.*` wildcard;
- ASP.NET Core's hosting, Kestrel, routing and diagnostics meters, `System.Net.Http` and `System.Runtime`, all of which
  are built into .NET, so no instrumentation package is needed;
- a view that keeps only bounded attributes on `System.Net.Http`'s instruments, dropping `server.address`,
  `server.port` and `network.peer.address`, which name the hosts of client-registered URIs and are unbounded;
- the OTLP exporter, to the metrics pipeline only and only when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured, so a host
  without a collector does not retry a missing endpoint. It is not `UseOtlpExporter`, which would also export every log
  record and enable tracing.

`AddOpenTelemetryMetrics` configures metrics only; a host that collects traces adds `AddSource("NCode.Identity.*")` to
its own tracing pipeline. The Aspire orchestrator ([ADR-1071](1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md)) sets the OTLP
variable.

### 9. Every instrument is tested and catalogued

Each instrument has a unit test that reads it through `MetricCollector<T>` and asserts its name, unit and attributes.
[`docs/metrics.md`](../metrics.md) catalogs every meter, instrument, span, unit and attribute with its allowed values,
so dashboards and alerts are built against a reviewed contract, and adding or changing an instrument, span or attribute
is a deliberate change that updates the catalog.

## Options considered

- **A counter per domain outcome at its call site.** Rejected: it duplicates the event seam and lets the counts drift.
- **A tenant attribute, opt-in or allow-listed.** Rejected: an operator who enables it on a multi-tenant platform
  creates a series per tenant per endpoint per status, which exhausts any bounded backend. Per-tenant views come from
  traces, logs and the usage ledger of [ADR-1199](1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md).
- **Publishing rate-limit refusals as events.** Rejected: a flood of refused requests would become a flood through the
  event pipeline and its sinks, the load the limiter exists to shed.
- **A span for every BFF endpoint.** Rejected: ASP.NET Core's request span already covers `/bff/login`,
  `/bff/callback`, `/bff/user` and `/bff/logout`.
- **A span around each token endpoint call.** Rejected: `System.Net.Http` traces it.
- **A factory-scoped activity source, like the meter.** Rejected: .NET has no `ActivitySource` factory.
- **Richer span attributes (subject, client id).** Rejected: the BFF design forbids them on any telemetry.

## Consequences

- An operator gets latency, traffic and errors per endpoint and per OAuth error code from one histogram, and builds
  service-level indicators on it: availability as the share of requests that are not `5xx`, and latency as a percentile
  of the token endpoint's duration.
- A slow BFF request shows its refresh, its waits and its store operations as child spans, with the result of each.
- A package that adds a span adds its histogram, or names the one it mirrors, and lists both in
  [`docs/metrics.md`](../metrics.md).
- Smell: a tenant, client, subject or address on a metric, or a counter that repeats an event's count.

## References

- [OpenTelemetry HTTP metrics conventions](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/) and
  [recording errors](https://opentelemetry.io/docs/specs/semconv/general/recording-errors/)
- [ASP.NET Core built-in metrics](https://learn.microsoft.com/aspnet/core/log-mon/metrics/built-in):
  `http.server.request.duration`, `aspnetcore.rate_limiting.requests` and `IHttpMetricsTagsFeature`
- [.NET metrics instrumentation](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation):
  `IMeterFactory` and `MetricCollector<T>`
- Issue [#119](https://github.com/NCodeGroup/identity/issues/119)
- [ADR-1047](1047-auditing-and-events-observation-seam.md): the event seam and its catalog
- [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md): the rate limiter
- [`docs/backend-for-frontend.md` §5.13](../backend-for-frontend.md#513-observability): the BFF's observability design
- `src/NCode.Identity.Events/DefaultEventMetricsHandler.cs`
