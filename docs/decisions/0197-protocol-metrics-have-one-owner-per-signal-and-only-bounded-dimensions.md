# 197. Protocol metrics have one owner per signal and only bounded dimensions

- **Status:** Accepted
- **Date:** 2026-10-21
- **Deciders:** NCode Group

## Context

The platform is observable through OpenTelemetry ([ADR-0134](0134-cloud-native-operating-principles.md) §8), but the
protocol endpoints have almost no metrics of their own. The family has one meter today: `DefaultEventMetricsHandler`
counts every published event ([ADR-0047](0047-auditing-and-events-observation-seam.md)). Nothing measures endpoint
latency by protocol meaning, the rate limiter's refusals
([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)), or the health of the shared
state those refusals depend on. The reference host does not wire OpenTelemetry at all.

Four forces shape the design:

- **Signals must not be counted twice.** ASP.NET Core already records `http.server.request.duration` for every request,
  `System.Net.Http` records `http.client.request.duration` for every outbound call, and the event seam already counts
  every audited outcome. A second counter of the same fact drifts from the first and doubles the series a deployment
  pays for.
- **Cardinality is bounded by the platform, not by its customers.** The hosted platform's metrics backend allows 10,000
  active series on its free tier ([hosting analysis](../hosting-analysis.md)). Tenants, clients, subjects, and remote
  addresses are unbounded, so a series per tenant grows with the customer base until the backend drops data.
- **Request input must never name a series.** An attribute whose value comes from the request (an unsupported
  `grant_type`, a `client_id`) lets any caller create series at will.
- **Vocabularies are open** ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)): a package registers
  another grant type, client-authentication method, or endpoint, so a metric dimension cannot be a closed list either.

## Decision

**Every signal has exactly one owner, every metric attribute comes from a vocabulary the server controls, and the
protocol packages measure only what neither ASP.NET Core nor the event seam already measures.**

### 1. One owner per signal

| Signal                                                              | Owner                                                                                 |
| ------------------------------------------------------------------- | ------------------------------------------------------------------------------------- |
| Request count, latency, and status of every protocol endpoint       | ASP.NET Core's `http.server.request.duration`, enriched with protocol attributes (§3) |
| Outbound calls (`jwks_uri`, `request_uri`, `sector_identifier_uri`) | `System.Net.Http`'s `http.client.request.duration`, through named `HttpClient`s       |
| Domain outcomes (token issued, client authentication failed, …)     | The event seam: `DefaultEventMetricsHandler` counts each published event (§4)         |
| Mechanisms that are not domain events (rate limiting, shared state) | A family instrument, defined once in the package that owns the mechanism (§5)         |

A fact is an event when someone would audit it; its count derives from the event, never from a parallel counter at
the call site. A mechanism on a hot or flood path (a refused request, a counter-store call) is not an event — a flood
of refusals must not flood the event pipeline — so it gets a direct instrument.

### 2. Meters, names, and units

- **One meter per package that owns instruments**, named after the package (`NCode.Identity.OpenId.Authentication`,
  `NCode.Identity.Events`, `NCode.Identity.SharedState.Valkey`). The meter names are public constants, and every one
  starts with `NCode.Identity.`, so a host subscribes to the whole family with one wildcard and a package that adds a
  meter needs no change to the host.
- **Meters come from `IMeterFactory`**, never `new Meter(…)`: the factory scopes a meter to its service provider, so
  two hosts in one process (tests, the Aspire orchestrator) never share instruments, and tests read them with
  `MetricCollector<T>`. A package's instruments live in one internal singleton `…Metrics` class that the factory's
  meter creates once; call sites call methods on it, never `Meter` APIs. `DefaultEventMetricsHandler` moves to the
  factory as well.
- **Instrument and attribute names share the family namespace `ncode.identity.{area}.…`**, as ASP.NET Core's own
  instruments and their attributes share `aspnetcore.…`. An attribute that OpenTelemetry's semantic conventions already
  define (`error.type`, `http.route`, `http.response.status_code`) keeps its standard name. The event metric's
  attributes become `ncode.identity.event.type`, `ncode.identity.audit.action`, and `ncode.identity.audit.outcome`.
- **Units are UCUM**: durations are histograms in seconds (`s`) with explicit bucket advice; counts use an annotation
  (`{request}`, `{event}`, `{operation}`).

### 3. The protocol endpoints enrich the built-in HTTP instrument

The protocol endpoint group adds attributes to `http.server.request.duration` through `IHttpMetricsTagsFeature` rather
than recording a request instrument of its own:

- `ncode.identity.openid.endpoint` — the endpoint's registered name (`token_endpoint`, `authorization_endpoint`, …,
  from `OpenIdConstants.EndpointNames` or the name a package registers). It is stable when an operator remaps routes or
  switches between host- and path-based tenant routing, which change `http.route`.
- `ncode.identity.openid.error` — the OAuth error code of an error response (`invalid_grant`, `invalid_client`,
  `temporarily_unavailable`, …), set where the error response is written so every path — an endpoint, the exception
  filter, the rate-limit filter — tags it the same way. Two writers exist: the executor of JSON protocol results, and
  the authorization result executor for an error redirected back to the client. HTTP status alone cannot tell `invalid_grant` from
  `invalid_client`. Error codes are server-chosen, never echoed from the request.
- `ncode.identity.openid.grant_type` — on the token endpoint only, the grant type when it is a registered one, else
  `_OTHER` (OpenTelemetry's convention for an unrecognized value), because the grants differ greatly in cost. It is set
  where the grant handler is selected, the one place that knows the registered grant types, so a request refused
  earlier (client authentication, request validation) carries no grant type.

The rate limiter's `429` is therefore visible as `http.response.status_code=429` with
`ncode.identity.openid.error=temporarily_unavailable`, with no instrument of its own for the response.

### 4. Domain outcomes come from the event seam

`DefaultEventMetricsHandler` stays the single counter of domain outcomes (`ncode.identity.events.published`). An event
type may declare additional **bounded** metric attributes by implementing `ISupportMetricAttributes`, which the handler
reads, so the curated event catalog (ADR-0047) also governs the metric's dimensions: a `token.issued` event adds its
server-set token type (`ncode.identity.token.type`) and a client-authentication failure adds the registered method the
client attempted (`ncode.identity.client_authentication.method`). The grant type is not repeated on the event; it is
already an attribute of the token endpoint's requests (§3). An event never contributes its tenant, client, or
subject.

### 5. Mechanism instruments

- **Rate limiting** (`NCode.Identity.OpenId.Authentication`): counter `ncode.identity.rate_limiting.requests`
  (`{request}`) with `ncode.identity.rate_limiting.partition` (`tenant`, `address`, `client`, and the daily quotas'
  `daily_requests` and `daily_token_requests` of ADR-0198) and
  `ncode.identity.rate_limiting.result` (`admitted`, `rejected`, `store_unavailable`), recorded once per judged
  partition, mirroring ASP.NET Core's `aspnetcore.rate_limiting.requests`. `store_unavailable` makes the fail-open path
  of ADR-0196 alertable.
- **Shared state** (each networked adapter, in its own package's meter — `NCode.Identity.SharedState.Valkey`):
  histogram `ncode.identity.shared_state.operation.duration` (`s`) with `ncode.identity.shared_state.operation`
  (`key_set.try_add`, `key_set.contains`, `counter.increment`, `counter.get`), `ncode.identity.shared_state.adapter`
  (`valkey`, or the name another adapter declares), and `error.type` on failure, timing the server round trip only.
  Replay checks, DPoP nonces, and rate limits all depend on it, so its latency and failures are one view. The
  in-process adapter records nothing: a dictionary operation in memory has no latency or failure worth a series, and
  the abstractions package stays free of a meter it would only hand to adapters.

A future mechanism (key resolution, signing, the dispatcher) follows the same pattern in its own package.

### 6. Tenant, client, subject, and address are never metric attributes

They belong to traces and logs, where cardinality costs nothing per series: the request's span carries the tenant and
client, and a metric's exemplar links a slow bucket to that span. Per-tenant **usage** — what a tenant consumed, for
plans and billing — is a stored count with its own retention
([ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)), not a metric series.

### 7. Hosts opt in; the reference host wires OpenTelemetry

The library packages depend only on `System.Diagnostics.Metrics`; recording costs nothing until a listener subscribes.
`NCode.Identity.Server` provides `AddOpenTelemetryMetrics(configuration)`, which the reference host calls — host
composition is the server package's job, not `Program.cs`'s. It adds the OpenTelemetry SDK, the family's meters by the
`NCode.Identity.*` wildcard, ASP.NET Core's hosting, Kestrel, routing, and diagnostics meters, `System.Net.Http`, and
the .NET runtime, all of which are built into .NET, so no instrumentation package is needed. A view keeps only bounded
attributes on `System.Net.Http`'s instruments: their `server.address` and `server.port` name the hosts of
client-registered URIs, which are unbounded. The OTLP exporter is added to the metrics pipeline only, and only when
`OTEL_EXPORTER_OTLP_ENDPOINT` is configured, so a host without a collector does not retry a missing endpoint. It is
not OpenTelemetry's cross-signal `UseOtlpExporter`, which would also export every log record and enable tracing, and
cannot be combined with a host's own signal-specific exporters. The Aspire orchestrator
([ADR-0071](0071-aspire-local-dev-orchestrator.md)) sets that variable and shows the same metrics in its dashboard.

### 8. Every instrument is tested and catalogued

Each instrument has a unit test that reads it through `MetricCollector<T>` and asserts its name, unit, and attributes.
The instrument catalog — every meter, instrument, unit, and attribute with its allowed values — is
[`docs/metrics.md`](../metrics.md), so dashboards and alerts are built against a reviewed contract, and adding or
renaming an instrument or attribute is a deliberate change.

## Options considered

- **A family request counter and duration histogram per endpoint.** Rejected: it duplicates
  `http.server.request.duration`, doubles the series, and can disagree with it.
- **A counter per domain outcome at its call site.** Rejected: it re-plumbs instrumentation the event seam already
  provides and lets the two counts drift (ADR-0047).
- **A tenant attribute, opt-in or allow-listed.** Rejected: an operator who enables it on a multi-tenant platform
  creates a series per tenant per endpoint per status, which exhausts any bounded backend. Per-tenant views come from
  traces, logs, and the usage ledger of [ADR-0199](0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md).
- **Publishing rate-limit refusals as events.** Rejected: a flood of refused requests would become a flood through the
  event pipeline and its sinks, the load the limiter exists to shed.
- **Depending on the OpenTelemetry API in the library packages.** Rejected: `System.Diagnostics.Metrics` is the .NET
  instrumentation API that OpenTelemetry reads; a library that depends on the SDK forces it on every consumer.

## Consequences

- The design is implemented (issue [#119](https://github.com/NCodeGroup/Identity/issues/119)): the endpoint, error, and grant-type attributes on
  `http.server.request.duration`, the rate-limiting counter, the event metric's attributes, the Valkey shared-state
  histogram, the reference host's OpenTelemetry wiring, and the instrument catalog.
- An operator gets latency, traffic, and errors per endpoint and per OAuth error code from one histogram, and can build
  service-level indicators on it: availability as the share of protocol requests that are not `5xx`, and latency as a
  percentile of the token endpoint's duration.
- The series a deployment emits is bounded by its registered vocabularies, not by its tenant count.
- Renaming the event metric's attributes changes a consumer-visible contract; pre-release, it is made once, cleanly
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## References

- [OpenTelemetry semantic conventions for HTTP metrics](https://opentelemetry.io/docs/specs/semconv/http/http-metrics/)
- [ASP.NET Core built-in metrics](https://learn.microsoft.com/aspnet/core/log-mon/metrics/built-in) —
  `http.server.request.duration`, `aspnetcore.rate_limiting.requests`, and `IHttpMetricsTagsFeature`
- [.NET metrics instrumentation](https://learn.microsoft.com/dotnet/core/diagnostics/metrics-instrumentation) —
  `IMeterFactory` and `MetricCollector<T>`
- [ADR-0047](0047-auditing-and-events-observation-seam.md) — the event seam and its catalog
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the rate limiter
- `src/NCode.Identity.Events/DefaultEventMetricsHandler.cs`
